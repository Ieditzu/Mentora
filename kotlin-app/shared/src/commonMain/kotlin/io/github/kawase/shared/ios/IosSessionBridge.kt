package io.github.kawase.shared.ios

import io.github.kawase.shared.localization.LanguagePreference
import io.github.kawase.shared.localization.SupportedLanguage
import io.github.kawase.shared.session.ConnectionStatus
import io.github.kawase.shared.session.MentoraSession

data class IosLanguageOption(val tag: String, val nativeName: String)

class IosSessionBridge(deviceLanguageTags: List<String>) {
    private val session = MentoraSession(deviceLanguageTags)

    fun availableLanguageOptions(): List<IosLanguageOption> {
        return SupportedLanguage.entries.map { IosLanguageOption(it.tag, it.nativeName) }
    }

    fun applyLanguagePreference(preferenceTag: String, deviceLanguageTags: List<String>) {
        val preference = if (preferenceTag == SYSTEM_LANGUAGE) {
            LanguagePreference.System
        } else {
            LanguagePreference.Explicit(preferenceTag)
        }
        session.updateLanguage(preference, deviceLanguageTags)
    }

    fun resolvedLanguageTag(): String = session.state.value.resolvedLanguage.tag

    fun updateConnectionStatus(status: String) {
        val connectionStatus = ConnectionStatus.entries.firstOrNull { it.name == status }
            ?: ConnectionStatus.DISCONNECTED
        session.updateConnectionStatus(connectionStatus)
    }

    companion object {
        const val SYSTEM_LANGUAGE = "system"
    }
}
