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

    init() {
        selectedLanguagePreference = UserDefaults.standard.string(forKey: languagePreferenceKey) ?? "system"
        languageOptions = session.availableLanguageOptions()
        liveStore = MentoraLiveStore()
        refreshLanguage()
        liveStore.$snapshot
            .map(\.isLoggedIn)
            .removeDuplicates()
            .receive(on: RunLoop.main)
            .sink { [weak self] isLoggedIn in
                self?.isAuthenticated = isLoggedIn
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
        liveStore.login(email: email, password: password)
    }

    func register(email: String, password: String) {
        self.email = email
        liveStore.register(email: email, password: password)
    }

    func signOut() {
        liveStore.disconnect()
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
}
