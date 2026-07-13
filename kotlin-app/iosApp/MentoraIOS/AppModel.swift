import Foundation
import MentoraShared

@MainActor
final class AppModel: ObservableObject {
    @Published var selectedLanguagePreference = "system"
    @Published private(set) var resolvedLanguageTag = "en"
    @Published private(set) var languageOptions: [IosLanguageOption] = []
    @Published var isAuthenticated = false
    @Published var email = ""

    private let session = IosSessionBridge(deviceLanguageTags: Locale.preferredLanguages)
    private let languagePreferenceKey = "mentora.languagePreference"

    init() {
        selectedLanguagePreference = UserDefaults.standard.string(forKey: languagePreferenceKey) ?? "system"
        languageOptions = session.availableLanguageOptions()
        refreshLanguage()
    }

    func applyLanguage(_ preferenceTag: String) {
        selectedLanguagePreference = preferenceTag
        UserDefaults.standard.set(preferenceTag, forKey: languagePreferenceKey)
        session.applyLanguagePreference(
            preferenceTag: preferenceTag,
            deviceLanguageTags: Locale.preferredLanguages
        )
        refreshLanguage()
    }

    func enterPreview(email: String) {
        self.email = email
        isAuthenticated = true
    }

    func signOut() {
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
    }
}
