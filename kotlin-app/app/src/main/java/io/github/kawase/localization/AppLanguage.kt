package io.github.kawase.localization

import android.content.res.Configuration
import java.util.Locale

data class AppLanguage(val tag: String, val nativeName: String)

object AppLanguages {
    const val SYSTEM_DEFAULT = "system"

    val supported = listOf(
        AppLanguage("en", "English"),
        AppLanguage("ro", "Română"),
        AppLanguage("es", "Español"),
        AppLanguage("fr", "Français"),
        AppLanguage("de", "Deutsch"),
        AppLanguage("it", "Italiano"),
        AppLanguage("pt-BR", "Português (Brasil)"),
        AppLanguage("pl", "Polski"),
        AppLanguage("tr", "Türkçe"),
        AppLanguage("uk", "Українська")
    )

    fun resolve(preference: String, configuration: Configuration): String {
        if (preference != SYSTEM_DEFAULT && supported.any { it.tag == preference }) return preference

        val deviceLanguage = configuration.locales[0]
        return supported.firstOrNull { it.tag.equals(deviceLanguage.toLanguageTag(), ignoreCase = true) }
            ?.tag
            ?: supported.firstOrNull { it.tag.substringBefore('-') == deviceLanguage.language }
                ?.tag
            ?: "en"
    }
}
