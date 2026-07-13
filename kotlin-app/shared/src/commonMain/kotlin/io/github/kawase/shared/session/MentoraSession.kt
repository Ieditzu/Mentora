package io.github.kawase.shared.session

import io.github.kawase.shared.localization.LanguagePreference
import io.github.kawase.shared.localization.MentoraLanguages
import io.github.kawase.shared.localization.SupportedLanguage
import io.github.kawase.shared.model.Child
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

enum class ConnectionStatus { DISCONNECTED, CONNECTING, CONNECTED }

data class MentoraSessionState(
    val connectionStatus: ConnectionStatus = ConnectionStatus.DISCONNECTED,
    val parentEmail: String? = null,
    val children: List<Child> = emptyList(),
    val selectedChildId: Long? = null,
    val languagePreference: LanguagePreference = LanguagePreference.System,
    val resolvedLanguage: SupportedLanguage = SupportedLanguage.ENGLISH
)

class MentoraSession(deviceLanguageTags: List<String>) {
    private val _state = MutableStateFlow(
        MentoraSessionState(
            resolvedLanguage = MentoraLanguages.resolve(LanguagePreference.System, deviceLanguageTags)
        )
    )

    val state: StateFlow<MentoraSessionState> = _state.asStateFlow()

    fun updateConnectionStatus(connectionStatus: ConnectionStatus) {
        _state.value = _state.value.copy(connectionStatus = connectionStatus)
    }

    fun updateLanguage(preference: LanguagePreference, deviceLanguageTags: List<String>) {
        _state.value = _state.value.copy(
            languagePreference = preference,
            resolvedLanguage = MentoraLanguages.resolve(preference, deviceLanguageTags)
        )
    }

    fun updateParent(email: String?) {
        _state.value = _state.value.copy(parentEmail = email)
    }

    fun updateChildren(children: List<Child>) {
        val selectedChildId = _state.value.selectedChildId?.takeIf { id -> children.any { it.id == id } }
        _state.value = _state.value.copy(children = children, selectedChildId = selectedChildId)
    }

    fun selectChild(childId: Long?) {
        _state.value = _state.value.copy(selectedChildId = childId)
    }
}
