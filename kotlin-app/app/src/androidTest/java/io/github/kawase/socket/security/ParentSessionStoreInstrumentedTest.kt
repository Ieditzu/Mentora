package io.github.kawase.socket.security

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class ParentSessionStoreInstrumentedTest {

    @Test
    fun sessionTokenRoundTripsThroughAndroidKeyStore() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val store = ParentSessionStore(context)

        store.clearSessionToken()

        try {
            val deviceId = store.deviceId()

            assertTrue(deviceId.isNotBlank())
            assertEquals(deviceId, store.deviceId())

            store.saveSessionToken("test-session-token")

            assertEquals("test-session-token", store.loadSessionToken())

            store.clearSessionToken()

            assertNull(store.loadSessionToken())
        } finally {
            store.clearSessionToken()
        }
    }
}
