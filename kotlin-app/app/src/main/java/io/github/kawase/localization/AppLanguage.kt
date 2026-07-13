package io.github.kawase.localization

import android.content.res.Configuration
import io.github.kawase.shared.localization.LanguagePreference
import io.github.kawase.shared.localization.MentoraLanguages
import io.github.kawase.shared.localization.SupportedLanguage

object AppLanguages {
    const val SYSTEM_DEFAULT = "system"

    val supported: List<SupportedLanguage> = SupportedLanguage.entries

    fun resolve(preference: String, configuration: Configuration): String {
        val languagePreference = if (preference == SYSTEM_DEFAULT) {
            LanguagePreference.System
        } else {
            LanguagePreference.Explicit(preference)
        }
        val deviceLanguageTags = (0 until configuration.locales.size())
            .map { configuration.locales[it].toLanguageTag() }

        return MentoraLanguages.resolve(languagePreference, deviceLanguageTags).tag
    }
}
