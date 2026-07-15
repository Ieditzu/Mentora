import Foundation
import Combine
import MentoraShared

enum MentoraAuthenticationState: Equatable {
    case idle
    case waitingForConnection
    case signingIn
    case creatingAccount

    var isPending: Bool { self != .idle }
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
    @Published var selectedChildID: Int64?

    private let client: IosMentoraClientBridge
    private let transport: MentoraWebSocketTransport
    private var pendingAuthentication: PendingAuthentication?
    private var authenticationTimeout: DispatchWorkItem?

    init(
        languageTag: String = Locale.preferredLanguages.first ?? "en",
        transport: MentoraWebSocketTransport = MentoraWebSocketTransport()
    ) {
        let bridge = IosMentoraClientBridge(languageTag: languageTag)
        self.client = bridge
        self.transport = transport
        self.snapshot = bridge.snapshot()
        configureTransportCallbacks()
    }

    var isConnected: Bool { connectionState == .connected }
    var isLoggedIn: Bool { snapshot.isLoggedIn }

    func connect(to url: URL) {
        guard url.scheme?.lowercased() == "ws" || url.scheme?.lowercased() == "wss" else {
            lastErrorMessage = "Mentora needs a ws:// or wss:// server URL."
            return
        }

        lastErrorMessage = nil
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
        beginAuthentication(email: email, password: password, mode: .signIn)
    }

    func register(email: String, password: String) {
        beginAuthentication(email: email, password: password, mode: .register)
    }

    func setLanguage(_ languageTag: String) {
        _ = client.setLanguage(languageTag: languageTag)
        snapshot = client.snapshot()
    }

    func loadDashboard() {
        client.initialDashboardRequests().forEach(send)
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
                    self.lastErrorMessage = nil
                    self.send(self.client.handshake(clientFingerprint: "ios_client;lang=\(self.snapshot.languageTag)"))
                    self.flushPendingAuthentication()
                } else if self.pendingAuthentication != nil {
                    self.authenticationState = .waitingForConnection
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
                self?.lastErrorMessage = error.localizedDescription
            }
        }
    }

    private func send(_ frame: KotlinByteArray) {
        guard isConnected else {
            lastErrorMessage = "Connect to the Mentora server before sending a request."
            return
        }
        transport.send(frame.mentoraData())
    }

    private func receive(_ data: Data) {
        let event = client.receive(frame: data.mentoraKotlinByteArray())
        snapshot = event.snapshot
        lastEvent = event

        if !event.success {
            if event.type == "authentication" || event.requestPacketId == 3 {
                clearPendingAuthentication()
            }
            lastErrorMessage = event.message
            return
        }

        // The Java server acknowledges registration with ActionResponsePacket rather than
        // AuthResponsePacket. Authenticate once registration succeeds to obtain the parent
        // session/profile and transition SwiftUI into the signed-in state.
        if event.type == "action", event.requestPacketId == 3,
           let pendingAuthentication, pendingAuthentication.mode == .register {
            authenticationState = .signingIn
            send(client.authenticate(email: pendingAuthentication.email, password: pendingAuthentication.password))
            scheduleAuthenticationTimeout()
            return
        }

        if event.type == "authentication" {
            lastErrorMessage = nil
            clearPendingAuthentication()
            loadDashboard()
        }
    }

    private func beginAuthentication(email: String, password: String, mode: PendingAuthentication.Mode) {
        let trimmedEmail = email.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmedEmail.isEmpty, !password.isEmpty else {
            lastErrorMessage = "Enter both an email address and password."
            return
        }

        lastErrorMessage = nil
        pendingAuthentication = PendingAuthentication(email: trimmedEmail, password: password, mode: mode)
        guard isConnected else {
            authenticationState = .waitingForConnection
            if let serverURL {
                transport.connect(to: serverURL)
            }
            return
        }
        flushPendingAuthentication()
    }

    private func flushPendingAuthentication() {
        guard isConnected, let pendingAuthentication else { return }
        switch pendingAuthentication.mode {
        case .signIn:
            authenticationState = .signingIn
            send(client.authenticate(email: pendingAuthentication.email, password: pendingAuthentication.password))
        case .register:
            authenticationState = .creatingAccount
            send(client.register(email: pendingAuthentication.email, password: pendingAuthentication.password))
        }
        scheduleAuthenticationTimeout()
    }

    private func clearPendingAuthentication() {
        authenticationTimeout?.cancel()
        authenticationTimeout = nil
        pendingAuthentication = nil
        authenticationState = .idle
    }

    private func scheduleAuthenticationTimeout() {
        authenticationTimeout?.cancel()
        let workItem = DispatchWorkItem { [weak self] in
            guard let self, self.pendingAuthentication != nil else { return }
            self.lastErrorMessage = "The server did not respond. Check the connection and try again."
            self.clearPendingAuthentication()
        }
        authenticationTimeout = workItem
        DispatchQueue.main.asyncAfter(deadline: .now() + 12, execute: workItem)
    }
}

private struct PendingAuthentication {
    enum Mode: Equatable {
        case signIn
        case register
    }

    let email: String
    let password: String
    let mode: Mode
}
