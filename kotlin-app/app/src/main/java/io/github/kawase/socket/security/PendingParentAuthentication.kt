package io.github.kawase.socket.security

import io.github.kawase.socket.utility.HashUtility
import java.util.Locale

internal enum class ParentAuthenticationMode {
    SIGN_IN,
    REGISTER
}

/**
 * Keeps only short-lived credential hashes while a parent authentication exchange is active.
 *
 * Sign-in tries the normalized email first, then the exact user input once for compatibility
 * with accounts created by older clients. Registration always uses the normalized email.
 */
internal class PendingParentAuthentication private constructor(
    val mode: ParentAuthenticationMode,
    val normalizedEmail: String,
    emailHashes: List<String>,
    passwordHash: String
) {
    private var remainingEmailHashes = emailHashes
    private var emailHashIndex = 0
    private var transientPasswordHash = passwordHash

    val currentEmailHash: String?
        get() = remainingEmailHashes.getOrNull(emailHashIndex)

    val passwordHash: String?
        get() = transientPasswordHash.takeIf(String::isNotEmpty)

    fun retryWithLegacyEmailHash(): String? {
        if (mode != ParentAuthenticationMode.SIGN_IN) return null
        emailHashIndex += 1
        return currentEmailHash
    }

    fun clear() {
        remainingEmailHashes = emptyList()
        transientPasswordHash = ""
        emailHashIndex = 0
    }

    companion object {
        fun signIn(emailInput: String, password: String): PendingParentAuthentication {
            val normalizedEmail = emailInput.trim().lowercase(Locale.ROOT)
            return PendingParentAuthentication(
                mode = ParentAuthenticationMode.SIGN_IN,
                normalizedEmail = normalizedEmail,
                emailHashes = listOf(
                    HashUtility.hash(normalizedEmail),
                    HashUtility.hash(emailInput)
                ).distinct(),
                passwordHash = HashUtility.hash(password)
            )
        }

        fun register(emailInput: String, password: String): PendingParentAuthentication {
            val normalizedEmail = emailInput.trim().lowercase(Locale.ROOT)
            return PendingParentAuthentication(
                mode = ParentAuthenticationMode.REGISTER,
                normalizedEmail = normalizedEmail,
                emailHashes = listOf(HashUtility.hash(normalizedEmail)),
                passwordHash = HashUtility.hash(password)
            )
        }
    }
}
