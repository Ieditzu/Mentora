package io.github.kawase.shared.localization

enum class SupportedLanguage(val tag: String, val nativeName: String) {
    ENGLISH("en", "English"),
    ROMANIAN("ro", "Română"),
    SPANISH("es", "Español"),
    FRENCH("fr", "Français"),
    GERMAN("de", "Deutsch"),
    ITALIAN("it", "Italiano"),
    BRAZILIAN_PORTUGUESE("pt-BR", "Português (Brasil)"),
    POLISH("pl", "Polski"),
    TURKISH("tr", "Türkçe"),
    UKRAINIAN("uk", "Українська")
}

sealed interface LanguagePreference {
    data object System : LanguagePreference
    data class Explicit(val languageTag: String) : LanguagePreference
}

object MentoraLanguages {
    fun resolve(preference: LanguagePreference, deviceLanguageTags: List<String>): SupportedLanguage {
        val explicitLanguage = (preference as? LanguagePreference.Explicit)
            ?.let { find(it.languageTag) }
        if (explicitLanguage != null) return explicitLanguage

        return deviceLanguageTags.firstNotNullOfOrNull(::find) ?: SupportedLanguage.ENGLISH
    }

    fun find(languageTag: String?): SupportedLanguage? {
        if (languageTag == null) return null

        return SupportedLanguage.entries.firstOrNull { it.tag.equals(languageTag, ignoreCase = true) }
            ?: SupportedLanguage.entries.firstOrNull {
                it.tag.substringBefore('-').equals(languageTag.substringBefore('-'), ignoreCase = true)
            }
    }
}
