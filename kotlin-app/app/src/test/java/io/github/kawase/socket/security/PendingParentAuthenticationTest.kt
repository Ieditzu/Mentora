package io.github.kawase.socket.security

import io.github.kawase.socket.utility.HashUtility
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class PendingParentAuthenticationTest {

    @Test
    fun signInTriesNormalizedHashThenLegacyExactInputHash() {
        val attempt = PendingParentAuthentication.signIn(" Parent@Example.COM ", "secret")

        assertEquals("parent@example.com", attempt.normalizedEmail)
        assertEquals(HashUtility.hash("parent@example.com"), attempt.currentEmailHash)
        assertEquals(HashUtility.hash("secret"), attempt.passwordHash)
        assertEquals(HashUtility.hash(" Parent@Example.COM "), attempt.retryWithLegacyEmailHash())
        assertNull(attempt.retryWithLegacyEmailHash())
    }

    @Test
    fun normalizedInputDoesNotCreateARepeatedFallback() {
        val attempt = PendingParentAuthentication.signIn("parent@example.com", "secret")

        assertNull(attempt.retryWithLegacyEmailHash())
    }

    @Test
    fun registrationNeverFallsBackAndClearErasesTransientHashes() {
        val attempt = PendingParentAuthentication.register(" Parent@Example.COM ", "secret")

        assertEquals(HashUtility.hash("parent@example.com"), attempt.currentEmailHash)
        assertNull(attempt.retryWithLegacyEmailHash())

        attempt.clear()

        assertNull(attempt.currentEmailHash)
        assertNull(attempt.passwordHash)
        assertTrue(attempt.normalizedEmail.isNotEmpty())
    }

    @Test
    fun invalidResumeClearsAuthenticatedStateAndChallengeCancelReconnects() {
        assertTrue(
            ParentAuthenticationTransition.shouldClearAuthenticatedState(
                requestPacketId = 91
            )
        )
        assertTrue(
            ParentAuthenticationTransition.shouldClearAuthenticatedState(
                parentSessionFailed = true
            )
        )
        assertTrue(
            ParentAuthenticationTransition.shouldReconnectAfterChallengeCancellation(
                hadChallenge = true
            )
        )
        assertEquals(
            false,
            ParentAuthenticationTransition.shouldReconnectAfterChallengeCancellation(
                hadChallenge = false
            )
        )
    }
}
