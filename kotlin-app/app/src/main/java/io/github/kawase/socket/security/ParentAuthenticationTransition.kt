package io.github.kawase.socket.security

internal object ParentAuthenticationTransition {
    fun shouldReconnectAfterChallengeCancellation(hadChallenge: Boolean): Boolean = hadChallenge

    fun shouldClearAuthenticatedState(
        requestPacketId: Int? = null,
        parentSessionFailed: Boolean = false
    ): Boolean = parentSessionFailed || requestPacketId == 91
}
