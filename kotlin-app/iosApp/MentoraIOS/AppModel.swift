import Foundation
import Combine
import MentoraShared

@MainActor
final class AppModel: ObservableObject {
    @Published var selectedLanguagePreference = "system"
    @Published private(set) var resolvedLanguageTag = "en"
    @Published private(set) var languageOptions: [IosLanguageOption] = []
    @Published var isAuthenticated = false
    @Published var email = ""
    let liveStore: MentoraLiveStore

    private let session = IosSessionBridge(deviceLanguageTags: Locale.preferredLanguages)
    private let languagePreferenceKey = "mentora.languagePreference"
    private var cancellables = Set<AnyCancellable>()
    private var savedCredentials: MentoraSavedCredentials?
    private var submittedCredentials: MentoraSavedCredentials?

    init() {
        selectedLanguagePreference = UserDefaults.standard.string(forKey: languagePreferenceKey) ?? "system"
        languageOptions = session.availableLanguageOptions()
        liveStore = MentoraLiveStore()
        refreshLanguage()
        savedCredentials = try? MentoraCredentialStore.load()
        email = savedCredentials?.email ?? ""
        liveStore.$snapshot
            .map(\.isLoggedIn)
            .removeDuplicates()
            .receive(on: RunLoop.main)
            .sink { [weak self] isLoggedIn in
                self?.isAuthenticated = isLoggedIn
            }
            .store(in: &cancellables)
        liveStore.$lastEvent
            .compactMap { $0 }
            .receive(on: RunLoop.main)
            .sink { [weak self] event in
                guard let self else { return }
                if event.type == "authentication" {
                    if event.success, let credentials = self.submittedCredentials {
                        self.saveCredentials(credentials)
                    } else if !event.success, self.submittedCredentials == nil {
                        try? MentoraCredentialStore.clear()
                        self.savedCredentials = nil
                    }
                    self.submittedCredentials = nil
                } else if event.requestPacketId == 3, !event.success {
                    self.submittedCredentials = nil
                }
            }
            .store(in: &cancellables)
        liveStore.$connectionState
            .removeDuplicates()
            .receive(on: RunLoop.main)
            .sink { [weak self] state in
                guard state == .connected,
                      let self,
                      self.submittedCredentials == nil,
                      let credentials = self.savedCredentials else { return }
                DispatchQueue.main.async { [weak self] in
                    guard let self,
                          self.liveStore.isConnected,
                          self.submittedCredentials == nil,
                          self.savedCredentials == credentials else { return }
                    self.liveStore.login(email: credentials.email, password: credentials.password)
                }
            }
            .store(in: &cancellables)
        liveStore.connect(to: "wss://neuro.serenityutils.club")
    }

    func applyLanguage(_ preferenceTag: String) {
        selectedLanguagePreference = preferenceTag
        UserDefaults.standard.set(preferenceTag, forKey: languagePreferenceKey)
        session.applyLanguagePreference(
            preferenceTag: preferenceTag,
            deviceLanguageTags: Locale.preferredLanguages
        )
        refreshLanguage()
        liveStore.setLanguage(resolvedLanguageTag)
    }

    func login(email: String, password: String) {
        self.email = email
        try? MentoraCredentialStore.clear()
        savedCredentials = nil
        submittedCredentials = MentoraSavedCredentials(email: email, password: password)
        liveStore.login(email: email, password: password)
    }

    func register(email: String, password: String) {
        self.email = email
        try? MentoraCredentialStore.clear()
        savedCredentials = nil
        submittedCredentials = MentoraSavedCredentials(email: email, password: password)
        liveStore.register(email: email, password: password)
    }

    func signOut() {
        liveStore.disconnect()
        try? MentoraCredentialStore.clear()
        savedCredentials = nil
        submittedCredentials = nil
        email = ""
        isAuthenticated = false
    }

    func refreshDeviceLanguage() {
        guard selectedLanguagePreference == "system" else { return }
        refreshLanguage()
    }

    private func refreshLanguage() {
        session.applyLanguagePreference(
            preferenceTag: selectedLanguagePreference,
            deviceLanguageTags: Locale.preferredLanguages
        )
        resolvedLanguageTag = session.resolvedLanguageTag()
        liveStore.setLanguage(resolvedLanguageTag)
    }

    private func saveCredentials(_ credentials: MentoraSavedCredentials) {
        do {
            try MentoraCredentialStore.save(email: credentials.email, password: credentials.password)
            savedCredentials = credentials
        } catch {
            savedCredentials = nil
        }
    }
}
