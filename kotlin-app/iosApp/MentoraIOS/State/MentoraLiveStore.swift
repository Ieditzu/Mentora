import Foundation
import Combine
import MentoraShared

enum MentoraAuthenticationState: Equatable {
    case idle
    case waitingForConnection
    case signingIn
    case creatingAccount
    case resumingSession
    case awaitingSecondFactor
    case verifyingSecondFactor

    var isPending: Bool {
        switch self {
        case .waitingForConnection, .signingIn, .creatingAccount, .resumingSession, .verifyingSecondFactor:
            return true
        case .idle, .awaitingSecondFactor:
            return false
        }
    }
}

struct MentoraSecondFactorChallenge: Equatable {
    let challengeID: String
    let expiresInSeconds: Int32
    let recoveryAllowed: Bool
}

struct MentoraTotpEnrollmentDetails: Equatable {
    let enrollmentID: String
    let secretBase32: String
    let otpAuthURI: String
}

struct MentoraAuthenticationAttempt: Equatable {
    enum Mode: Equatable {
        case signIn
        case register
    }

    let mode: Mode
    let normalizedEmail: String
    private(set) var normalizedEmailHash: String
    private(set) var legacyExactEmailHash: String?
    private(set) var passwordHash: String
    private(set) var isUsingLegacyHash = false

    init(
        emailInput: String,
        password: String,
        mode: Mode,
        hash: (String) -> String
    ) {
        let normalizedEmail = emailInput
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased()
        let normalizedHash = hash(normalizedEmail)
        let exactHash = hash(emailInput)
        self.mode = mode
        self.normalizedEmail = normalizedEmail
        self.normalizedEmailHash = normalizedHash
        self.legacyExactEmailHash = mode == .signIn && exactHash != normalizedHash
            ? exactHash
            : nil
        self.passwordHash = hash(password)
    }

    var currentEmailHash: String {
        isUsingLegacyHash ? (legacyExactEmailHash ?? "") : normalizedEmailHash
    }

    mutating func retryWithLegacyEmailHash() -> String? {
        guard mode == .signIn, !isUsingLegacyHash, let legacyExactEmailHash else {
            return nil
        }
        isUsingLegacyHash = true
        return legacyExactEmailHash
    }

    mutating func clear() {
        normalizedEmailHash = ""
        legacyExactEmailHash = nil
        passwordHash = ""
        isUsingLegacyHash = false
    }
}

enum MentoraAuthenticationTransition {
    static func shouldReconnectAfterChallengeCancellation(hadChallenge: Bool) -> Bool {
        hadChallenge
    }

    static func shouldClearAuthenticatedState(
        eventType: String,
        requestPacketID: Int32
    ) -> Bool {
        eventType == "parentSession" || requestPacketID == 91
    }
}

/// Converts Kotlin/Native byte arrays into the binary payloads expected by URLSession.
///
/// The shared framework deliberately owns encryption and packet framing; Swift only
/// transports the resulting bytes.
extension KotlinByteArray {
    func mentoraData() -> Data {
        var bytes = [UInt8]()
        bytes.reserveCapacity(Int(size))
        for index in 0..<size {
            bytes.append(UInt8(bitPattern: get(index: index)))
        }
        return Data(bytes)
    }
}

extension Data {
    func mentoraKotlinByteArray() -> KotlinByteArray {
        let result = KotlinByteArray(size: Int32(count))
        for (index, byte) in enumerated() {
            result.set(index: Int32(index), value: Int8(bitPattern: byte))
        }
        return result
    }
}

/// Live, server-backed state for the native iOS client.
///
/// `IosMentoraClientBridge` owns protocol encryption and state reduction. This store owns
/// the WebSocket lifecycle and exposes its immutable snapshots to SwiftUI. It intentionally
/// contains no preview/sample data.
@MainActor
final class MentoraLiveStore: ObservableObject {
    @Published private(set) var snapshot: IosMentoraSnapshot
    @Published private(set) var connectionState: MentoraWebSocketState = .disconnected
    @Published private(set) var serverURL: URL?
    @Published private(set) var lastEvent: IosMentoraEvent?
    @Published private(set) var lastErrorMessage: String?
    @Published private(set) var authenticationState: MentoraAuthenticationState = .idle
    @Published private(set) var secondFactorChallenge: MentoraSecondFactorChallenge?
    @Published private(set) var totpEnrollmentDetails: MentoraTotpEnrollmentDetails?
    @Published private(set) var recoveryCodes: [String] = []
    @Published private(set) var isSecurityRequestPending = false
    @Published private(set) var isSigningOut = false
    @Published var selectedChildID: Int64?

    private let client: IosMentoraClientBridge
    private let transport: MentoraWebSocketTransport
    private let deviceID: String?
    private var sessionToken: String?
    private var credentialStoreError: String?
    private var pendingAuthentication: MentoraAuthenticationAttempt?
    private var authenticationTimeout: DispatchWorkItem?
    private var securityTimeout: DispatchWorkItem?
    private var signOutTimeout: DispatchWorkItem?

    init(
        languageTag: String = Locale.preferredLanguages.first ?? "en",
        transport: MentoraWebSocketTransport = MentoraWebSocketTransport()
    ) {
        var storedDeviceID: String? = nil
        var storedSessionToken: String? = nil
        var credentialError: String? = nil
        do {
            storedDeviceID = try MentoraCredentialStore.deviceID()
        } catch {
            credentialError = error.localizedDescription
        }
        do {
            storedSessionToken = try MentoraCredentialStore.loadSessionToken()
        } catch {
            credentialError = error.localizedDescription
        }
        self.deviceID = storedDeviceID
        self.sessionToken = storedSessionToken
        self.credentialStoreError = credentialError
        let bridge = IosMentoraClientBridge(languageTag: languageTag)
        self.client = bridge
        self.transport = transport
        self.snapshot = bridge.snapshot()
        self.lastErrorMessage = credentialError
        configureTransportCallbacks()
    }

    var isConnected: Bool { connectionState == .connected }
    var isLoggedIn: Bool { snapshot.isLoggedIn }

    func connect(to url: URL) {
        guard url.scheme?.lowercased() == "ws" || url.scheme?.lowercased() == "wss" else {
            lastErrorMessage = "Mentora needs a ws:// or wss:// server URL."
            return
        }
        guard deviceID != nil else {
            lastErrorMessage = "Secure device identity is unavailable. Check Keychain access and restart Mentora."
            return
        }

        lastErrorMessage = credentialStoreError
        serverURL = url
        transport.connect(to: url)
    }

    func connect(to serverAddress: String) {
        let trimmed = serverAddress.trimmingCharacters(in: .whitespacesAndNewlines)
        let address = trimmed.contains("://") ? trimmed : "wss://\(trimmed)"
        guard let url = URL(string: address) else {
            lastErrorMessage = "That server address is not valid."
            return
        }
        connect(to: url)
    }

    func disconnect() {
        transport.disconnect()
    }

    func login(email: String, password: String) {
        guard discardSavedSession() else { return }
        beginAuthentication(email: email, password: password, mode: .signIn)
    }

    func register(email: String, password: String) {
        guard discardSavedSession() else { return }
        beginAuthentication(email: email, password: password, mode: .register)
    }

    func submitSecondFactor(_ code: String) {
        guard let challenge = secondFactorChallenge else { return }
        let code = code.trimmingCharacters(in: .whitespacesAndNewlines)
        guard isConnected, !code.isEmpty else {
            failAuthentication("Connect to the Mentora server before verifying the code.")
            return
        }

        authenticationState = .verifyingSecondFactor
        guard send(client.verifySecondFactor(challengeId: challenge.challengeID, code: code)) else {
            failAuthentication("The verification request could not be sent.", reconnect: true)
            return
        }
        scheduleAuthenticationTimeout()
    }

    func cancelSecondFactor() {
        let hadChallenge = secondFactorChallenge != nil
        secondFactorChallenge = nil
        clearPendingAuthentication()
        if MentoraAuthenticationTransition.shouldReconnectAfterChallengeCancellation(
            hadChallenge: hadChallenge
        ) {
            reconnectToResetPendingAuthentication()
        }
    }

    func fetchParentSecurityStatus() {
        guard beginSecurityRequest() else { return }
        guard send(client.fetchParentSecurityStatus()) else {
            failSecurityRequest("Security status could not be loaded.")
            return
        }
        scheduleSecurityTimeout()
    }

    func beginTotpEnrollment(password: String) {
        guard isLoggedIn, !password.isEmpty, beginSecurityRequest() else { return }
        recoveryCodes = []
        lastErrorMessage = nil
        guard send(client.beginTotpEnrollment(password: password)) else {
            failSecurityRequest("Two-factor setup could not be started.")
            return
        }
        scheduleSecurityTimeout()
    }

    func confirmTotpEnrollment(code: String) {
        guard let details = totpEnrollmentDetails else { return }
        let code = code.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !code.isEmpty, beginSecurityRequest() else { return }
        guard send(client.confirmTotpEnrollment(enrollmentId: details.enrollmentID, code: code)) else {
            failSecurityRequest("The authenticator code could not be verified.")
            return
        }
        scheduleSecurityTimeout()
    }

    func disableTotp(password: String, code: String) {
        let code = code.trimmingCharacters(in: .whitespacesAndNewlines)
        guard isLoggedIn, !password.isEmpty, !code.isEmpty, beginSecurityRequest() else { return }
        guard send(client.disableTotp(password: password, code: code)) else {
            failSecurityRequest("Two-factor authentication could not be disabled.")
            return
        }
        scheduleSecurityTimeout()
    }

    func clearTotpEnrollmentResult() {
        let resetPendingEnrollment = totpEnrollmentDetails != nil
        totpEnrollmentDetails = nil
        recoveryCodes = []
        clearSecurityRequest()
        if resetPendingEnrollment {
            reconnectToResetPendingAuthentication()
        }
    }

    func signOut() {
        guard !isSigningOut else { return }
        guard let sessionToken, isConnected else {
            finishSignOut()
            return
        }
        isSigningOut = true
        scheduleSignOutTimeout()
        let frame = client
            .revokeParentSession(sessionToken: sessionToken, revokeAll: false)
            .mentoraData()
        transport.send(frame) { [weak self] result in
            guard case .failure = result else { return }
            DispatchQueue.main.async {
                self?.finishSignOut()
            }
        }
    }

    private func finishSignOut() {
        signOutTimeout?.cancel()
        signOutTimeout = nil
        isSigningOut = false
        _ = discardSavedSession()
        clearPendingAuthentication()
        clearSecurityRequest()
        secondFactorChallenge = nil
        totpEnrollmentDetails = nil
        recoveryCodes = []
        selectedChildID = nil
        client.clearParentSession()
        snapshot = client.snapshot()
        transport.disconnect()
    }

    func setLanguage(_ languageTag: String) {
        let frame = client.setLanguage(languageTag: languageTag)
        snapshot = client.snapshot()
        guard isConnected else { return }
        send(frame)
    }

    func loadDashboard() {
        client.initialDashboardRequests().forEach { _ = send($0) }
    }

    func selectChild(_ childID: Int64?) {
        selectedChildID = childID
        guard let childID else { return }
        loadChildDetails(for: childID)
    }

    func loadChildDetails(for childID: Int64) {
        send(client.fetchGoals(childId: childID))
        send(client.fetchCompletedTasks(childId: childID))
        send(client.fetchChildProfile(childId: childID))
        send(client.fetchWeeklyReport(childId: childID))
        send(client.subscribeLiveSession(childId: childID))
    }

    func addChild(named name: String) {
        let name = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !name.isEmpty else { return }
        send(client.addChild(name: name))
    }

    func removeChild(id childID: Int64) {
        if selectedChildID == childID {
            selectedChildID = nil
        }
        send(client.removeChild(childId: childID))
    }

    func completeTask(childID: Int64, taskID: Int64) {
        send(client.completeTask(childId: childID, taskId: taskID))
    }

    func addGoal(
        childID: Int64,
        title: String,
        reward: String,
        requiredPoints: Int32,
        requiredTaskID: Int64
    ) {
        let title = title.trimmingCharacters(in: .whitespacesAndNewlines)
        let reward = reward.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !title.isEmpty, !reward.isEmpty, requiredPoints >= 0 else { return }
        send(client.addGoal(
            childId: childID,
            title: title,
            reward: reward,
            requiredPoints: requiredPoints,
            requiredTaskId: requiredTaskID
        ))
    }

    func sendChallenge(to childID: Int64, message: String) {
        let message = message.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !message.isEmpty else { return }
        send(client.sendParentChallenge(childId: childID, message: message))
    }

    func claimQRLogin(token: String, for childID: Int64) {
        let token = token.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !token.isEmpty else { return }
        send(client.claimQrLogin(token: token, childId: childID))
    }

    func updateProfilePicture(childID: Int64, base64Picture: String) {
        guard !base64Picture.isEmpty else { return }
        send(client.updateProfilePicture(childId: childID, base64Picture: base64Picture))
    }

    func askAI(question: String, context: String = "") {
        let question = question.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !question.isEmpty else { return }
        send(client.askAi(question: question, context: context))
    }

    func stopLiveUpdates(for childID: Int64) {
        send(client.unsubscribeLiveSession(childId: childID))
    }

    private func configureTransportCallbacks() {
        transport.onStateChange = { [weak self] state in
            DispatchQueue.main.async {
                guard let self else { return }
                self.connectionState = state
                if state == .connected {
                    self.lastErrorMessage = self.credentialStoreError
                    guard let deviceID = self.deviceID else {
                        self.lastErrorMessage = "Secure device identity is unavailable."
                        self.transport.disconnect()
                        return
                    }
                    self.send(self.client.handshakeV2(
                        clientFingerprint: "ios_parent",
                        deviceId: deviceID
                    ))
                    self.send(self.client.setLanguage(languageTag: self.snapshot.languageTag))
                    if self.pendingAuthentication != nil {
                        self.flushPendingAuthentication()
                    } else if let sessionToken = self.sessionToken {
                        self.authenticationState = .resumingSession
                        self.send(self.client.resumeParentSession(
                            sessionToken: sessionToken,
                            deviceId: deviceID
                        ))
                        self.scheduleAuthenticationTimeout()
                    }
                } else {
                    if self.isSigningOut {
                        self.finishSignOut()
                        return
                    }
                    if self.authenticationState == .resumingSession {
                        self.resetAuthenticatedState(
                            message: "The saved parent session could not be restored."
                        )
                    } else if self.pendingAuthentication != nil || self.secondFactorChallenge != nil {
                        self.failAuthentication(
                            "The authentication connection closed. Try signing in again."
                        )
                    }
                    if self.isSecurityRequestPending {
                        self.failSecurityRequest(
                            "The connection closed before the security request completed."
                        )
                    }
                }
            }
        }

        transport.onBinaryMessage = { [weak self] data in
            DispatchQueue.main.async {
                self?.receive(data)
            }
        }

        transport.onError = { [weak self] error in
            DispatchQueue.main.async {
                guard let self else { return }
                self.lastErrorMessage = error.localizedDescription
                if self.authenticationState.isPending {
                    self.failAuthentication(error.localizedDescription)
                }
                if self.isSecurityRequestPending {
                    self.failSecurityRequest(error.localizedDescription)
                }
            }
        }
    }

    @discardableResult
    private func send(_ frame: KotlinByteArray) -> Bool {
        guard isConnected else {
            lastErrorMessage = "Connect to the Mentora server before sending a request."
            return false
        }
        transport.send(frame.mentoraData())
        return true
    }

    private func receive(_ data: Data) {
        let event = client.receive(frame: data.mentoraKotlinByteArray())
        snapshot = event.snapshot
        lastEvent = event

        if event.type == "action", event.requestPacketId == 92, isSigningOut {
            finishSignOut()
            return
        }

        if !event.success {
            if event.type == "authentication", retryLegacySignInIfAvailable() {
                return
            }
            if MentoraAuthenticationTransition.shouldClearAuthenticatedState(
                eventType: event.type,
                requestPacketID: event.requestPacketId
            ) {
                resetAuthenticatedState(
                    message: event.message.isEmpty
                        ? "Unable to restore the parent session."
                        : event.message
                )
                return
            }
            if event.type == "authentication" || event.requestPacketId == 3 {
                clearPendingAuthentication()
            }
            if event.requestPacketId == 82, secondFactorChallenge != nil {
                authenticationTimeout?.cancel()
                authenticationTimeout = nil
                authenticationState = .awaitingSecondFactor
            }
            if (83...89).contains(event.requestPacketId) ||
                event.type == "totpEnrollmentResult" {
                clearSecurityRequest()
            }
            lastErrorMessage = event.message
            return
        }

        if event.type == "secondFactorRequired" {
            authenticationTimeout?.cancel()
            authenticationTimeout = nil
            pendingAuthentication?.clear()
            pendingAuthentication = nil
            secondFactorChallenge = MentoraSecondFactorChallenge(
                challengeID: event.challengeId,
                expiresInSeconds: event.expiresInSeconds,
                recoveryAllowed: event.recoveryAllowed
            )
            authenticationState = .awaitingSecondFactor
            return
        }

        if event.type == "parentSession" {
            authenticationTimeout?.cancel()
            authenticationTimeout = nil
            let rotatedSessionToken = client.takeSessionToken()
            var sessionSaveError: String?
            if !rotatedSessionToken.isEmpty {
                do {
                    try MentoraCredentialStore.saveSessionToken(rotatedSessionToken)
                    sessionToken = rotatedSessionToken
                    credentialStoreError = nil
                } catch {
                    sessionToken = nil
                    sessionSaveError = error.localizedDescription
                    credentialStoreError = sessionSaveError
                }
            }
            secondFactorChallenge = nil
            clearPendingAuthentication()
            loadDashboard()
            fetchParentSecurityStatus()
            lastErrorMessage = sessionSaveError ?? credentialStoreError
            return
        }

        if event.type == "totpEnrollmentDetails" {
            clearSecurityRequest()
            totpEnrollmentDetails = MentoraTotpEnrollmentDetails(
                enrollmentID: event.enrollmentId,
                secretBase32: event.secretBase32,
                otpAuthURI: event.otpAuthUri
            )
            return
        }

        if event.type == "totpEnrollmentResult" {
            clearSecurityRequest()
            lastErrorMessage = event.message.isEmpty ? nil : event.message
            if event.success {
                let sessionWasCleared = discardSavedSession()
                totpEnrollmentDetails = nil
                recoveryCodes = event.recoveryCodes
                fetchParentSecurityStatus()
                if !sessionWasCleared {
                    lastErrorMessage = credentialStoreError
                }
            }
            return
        }

        // The Java server acknowledges registration with ActionResponsePacket rather than
        // AuthResponsePacket. Authenticate once registration succeeds to obtain the parent
        // session/profile and transition SwiftUI into the signed-in state.
        if event.type == "action", event.requestPacketId == 3,
           let pendingAuthentication, pendingAuthentication.mode == .register {
            authenticationState = .signingIn
            guard send(
                client.authenticateHashed(
                    emailHash: pendingAuthentication.currentEmailHash,
                    passwordHash: pendingAuthentication.passwordHash
                )
            ) else {
                failAuthentication("The account was created, but sign-in could not be sent.")
                return
            }
            scheduleAuthenticationTimeout()
            return
        }

        if event.type == "authentication" {
            lastErrorMessage = nil
            secondFactorChallenge = nil
            clearPendingAuthentication()
            loadDashboard()
            fetchParentSecurityStatus()
        } else if event.type == "action", event.requestPacketId == 87 {
            clearSecurityRequest()
            let sessionWasCleared = discardSavedSession()
            clearTotpEnrollmentResult()
            fetchParentSecurityStatus()
            if !sessionWasCleared {
                lastErrorMessage = credentialStoreError
            }
        } else if event.type == "parentSecurityStatus" {
            clearSecurityRequest()
        }
    }

    private func beginAuthentication(
        email: String,
        password: String,
        mode: MentoraAuthenticationAttempt.Mode
    ) {
        let trimmedEmail = email.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        guard !trimmedEmail.isEmpty, !password.isEmpty else {
            lastErrorMessage = "Enter both an email address and password."
            return
        }
        guard isConnected else {
            lastErrorMessage = "Connect to the Mentora server before signing in."
            authenticationState = .idle
            return
        }

        lastErrorMessage = nil
        let bridge = client
        pendingAuthentication = MentoraAuthenticationAttempt(
            emailInput: email,
            password: password,
            mode: mode,
            hash: { bridge.sha256(value: $0) }
        )
        flushPendingAuthentication()
    }

    private func flushPendingAuthentication() {
        guard isConnected, let pendingAuthentication else { return }
        switch pendingAuthentication.mode {
        case .signIn:
            authenticationState = .signingIn
            guard send(
                client.authenticateHashed(
                    emailHash: pendingAuthentication.currentEmailHash,
                    passwordHash: pendingAuthentication.passwordHash
                )
            ) else {
                failAuthentication("Sign-in could not be sent. Check the connection.")
                return
            }
        case .register:
            authenticationState = .creatingAccount
            guard send(
                client.registerHashed(
                    emailHash: pendingAuthentication.currentEmailHash,
                    passwordHash: pendingAuthentication.passwordHash
                )
            ) else {
                failAuthentication("Registration could not be sent. Check the connection.")
                return
            }
        }
        scheduleAuthenticationTimeout()
    }

    private func clearPendingAuthentication() {
        authenticationTimeout?.cancel()
        authenticationTimeout = nil
        pendingAuthentication?.clear()
        pendingAuthentication = nil
        authenticationState = .idle
    }

    @discardableResult
    private func discardSavedSession() -> Bool {
        sessionToken = nil
        do {
            try MentoraCredentialStore.clearSessionToken()
            credentialStoreError = nil
            return true
        } catch {
            credentialStoreError = error.localizedDescription
            lastErrorMessage = credentialStoreError
            return false
        }
    }

    private func scheduleAuthenticationTimeout() {
        authenticationTimeout?.cancel()
        let workItem = DispatchWorkItem { [weak self] in
            guard let self, self.authenticationState.isPending else { return }
            if self.authenticationState == .resumingSession {
                self.resetAuthenticatedState(
                    message: "The saved parent session could not be restored."
                )
            } else {
                self.failAuthentication(
                    "The server did not respond. Check the connection and try again.",
                    reconnect: self.authenticationState == .verifyingSecondFactor
                )
            }
        }
        authenticationTimeout = workItem
        DispatchQueue.main.asyncAfter(deadline: .now() + 12, execute: workItem)
    }

    private func retryLegacySignInIfAvailable() -> Bool {
        guard var pendingAuthentication,
              let legacyEmailHash = pendingAuthentication.retryWithLegacyEmailHash(),
              !pendingAuthentication.passwordHash.isEmpty else {
            return false
        }
        self.pendingAuthentication = pendingAuthentication
        authenticationState = .signingIn
        lastErrorMessage = nil
        guard send(
            client.authenticateHashed(
                emailHash: legacyEmailHash,
                passwordHash: pendingAuthentication.passwordHash
            )
        ) else {
            failAuthentication("Legacy account sign-in could not be sent.")
            return true
        }
        scheduleAuthenticationTimeout()
        return true
    }

    private func failAuthentication(_ message: String, reconnect: Bool = false) {
        clearPendingAuthentication()
        secondFactorChallenge = nil
        lastErrorMessage = message
        if reconnect {
            reconnectToResetPendingAuthentication()
        }
    }

    private func resetAuthenticatedState(message: String) {
        let sessionWasCleared = discardSavedSession()
        let sessionCleanupError = credentialStoreError
        clearPendingAuthentication()
        clearSecurityRequest()
        secondFactorChallenge = nil
        totpEnrollmentDetails = nil
        recoveryCodes = []
        selectedChildID = nil
        client.clearParentSession()
        snapshot = client.snapshot()
        lastErrorMessage = sessionWasCleared
            ? message
            : "\(message) \(sessionCleanupError ?? "Saved session cleanup failed.")"
    }

    private func beginSecurityRequest() -> Bool {
        guard isLoggedIn, isConnected else {
            lastErrorMessage = "Connect to the Mentora server before changing security settings."
            isSecurityRequestPending = false
            return false
        }
        isSecurityRequestPending = true
        lastErrorMessage = nil
        return true
    }

    private func clearSecurityRequest() {
        securityTimeout?.cancel()
        securityTimeout = nil
        isSecurityRequestPending = false
    }

    private func failSecurityRequest(_ message: String) {
        clearSecurityRequest()
        lastErrorMessage = message
    }

    private func scheduleSecurityTimeout() {
        securityTimeout?.cancel()
        let workItem = DispatchWorkItem { [weak self] in
            guard let self, self.isSecurityRequestPending else { return }
            self.failSecurityRequest("The security request timed out. Try again.")
        }
        securityTimeout = workItem
        DispatchQueue.main.asyncAfter(deadline: .now() + 12, execute: workItem)
    }

    private func scheduleSignOutTimeout() {
        signOutTimeout?.cancel()
        let workItem = DispatchWorkItem { [weak self] in
            self?.finishSignOut()
        }
        signOutTimeout = workItem
        DispatchQueue.main.asyncAfter(deadline: .now() + 1, execute: workItem)
    }

    private func reconnectToResetPendingAuthentication() {
        guard let serverURL else {
            transport.disconnect()
            return
        }
        transport.connect(to: serverURL)
    }
}
