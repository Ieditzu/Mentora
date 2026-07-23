package io.github.kawase.shared.ios

import io.github.kawase.shared.protocol.AuthPacket
import io.github.kawase.shared.protocol.ActionResponsePacket
import io.github.kawase.shared.protocol.BeginParentTotpEnrollmentPacket
import io.github.kawase.shared.protocol.HandshakePacket
import io.github.kawase.shared.protocol.MentoraPacket
import io.github.kawase.shared.protocol.PacketFactory
import io.github.kawase.shared.protocol.PacketFrameCodec
import io.github.kawase.shared.protocol.ParentAuthSessionPacket
import io.github.kawase.shared.protocol.ParentSecondFactorRequiredPacket
import io.github.kawase.shared.protocol.ParentSecurityStatusPacket
import io.github.kawase.shared.protocol.ParentTotpEnrollmentDetailsPacket
import io.github.kawase.shared.protocol.ParentTotpEnrollmentResultPacket
import io.github.kawase.shared.protocol.RegisterParentPacket
import io.github.kawase.shared.protocol.ResumeParentSessionPacket
import io.github.kawase.shared.protocol.VerifyParentSecondFactorPacket
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class IosParentSecurityIntegrationTest {
    private val frameCodec = PacketFrameCodec()

    @Test
    fun `v2 commands preserve device challenge and enrollment fields`() {
        val bridge = IosMentoraClientBridge()

        assertEquals(
            HandshakePacket("ios_parent", 2, "device-123"),
            decodeCommand(bridge.handshakeV2("ios_parent", "device-123"))
        )
        assertEquals(
            VerifyParentSecondFactorPacket("challenge-1", "004207"),
            decodeCommand(bridge.verifySecondFactor("challenge-1", "004207"))
        )
        assertEquals(
            ResumeParentSessionPacket("session-token", "device-123"),
            decodeCommand(bridge.resumeParentSession("session-token", "device-123"))
        )
        assertEquals(
            BeginParentTotpEnrollmentPacket(MentoraSha256.hex("correct horse")),
            decodeCommand(bridge.beginTotpEnrollment("correct horse"))
        )
        assertEquals(
            AuthPacket(
                MentoraSha256.hex("parent@example.com"),
                MentoraSha256.hex("password")
            ),
            decodeCommand(bridge.authenticate(" Parent@Example.COM ", "password"))
        )
        assertEquals(
            RegisterParentPacket(
                MentoraSha256.hex("parent@example.com"),
                MentoraSha256.hex("password")
            ),
            decodeCommand(bridge.register(" Parent@Example.COM ", "password"))
        )
        assertEquals(
            AuthPacket(
                MentoraSha256.hex(" Parent@Example.COM "),
                MentoraSha256.hex("password")
            ),
            decodeCommand(
                bridge.authenticateHashed(
                    bridge.sha256(" Parent@Example.COM "),
                    bridge.sha256("password")
                )
            )
        )
    }

    @Test
    fun `server security sequence reduces to a logged in protected snapshot`() {
        val bridge = IosMentoraClientBridge()

        val challengeEvent = bridge.receive(
            serverFrame(ParentSecondFactorRequiredPacket("challenge-1", 300, true))
        )
        assertEquals("secondFactorRequired", challengeEvent.type)
        assertEquals("challenge-1", challengeEvent.challengeId)
        assertEquals(300, challengeEvent.expiresInSeconds)
        assertTrue(challengeEvent.recoveryAllowed)
        assertFalse(challengeEvent.snapshot.isLoggedIn)

        val sessionEvent = bridge.receive(
            serverFrame(
                ParentAuthSessionPacket(
                    true,
                    42,
                    "Welcome",
                    "picture",
                    "rotated-token",
                    1_800_000_000
                )
            )
        )
        assertEquals("parentSession", sessionEvent.type)
        assertTrue(sessionEvent.snapshot.isLoggedIn)
        assertEquals(42, sessionEvent.snapshot.parentId)
        assertEquals("rotated-token", bridge.takeSessionToken())
        assertEquals("", bridge.takeSessionToken())
        assertEquals(1_800_000_000, bridge.takeSessionExpiryEpochSeconds())

        bridge.receive(serverFrame(ParentSecurityStatusPacket(false, 0)))
        val detailsEvent = bridge.receive(
            serverFrame(
                ParentTotpEnrollmentDetailsPacket(
                    "enrollment-1",
                    "JBSWY3DPEHPK3PXP",
                    "otpauth://totp/Mentora:test"
                )
            )
        )
        assertEquals("totpEnrollmentDetails", detailsEvent.type)
        assertEquals("JBSWY3DPEHPK3PXP", detailsEvent.secretBase32)

        val resultEvent = bridge.receive(
            serverFrame(
                ParentTotpEnrollmentResultPacket(
                    true,
                    "Enabled",
                    listOf("ALPHA-1111", "BRAVO-2222")
                )
            )
        )
        assertTrue(resultEvent.snapshot.twoFactorEnabled)
        assertEquals(2, resultEvent.snapshot.recoveryCodesRemaining)
        assertEquals(listOf("ALPHA-1111", "BRAVO-2222"), resultEvent.recoveryCodes)
    }

    @Test
    fun `failed session resume clears every authenticated bridge field`() {
        val bridge = IosMentoraClientBridge()
        bridge.receive(
            serverFrame(
                ParentAuthSessionPacket(
                    true,
                    42,
                    "Welcome",
                    "picture",
                    "rotated-token",
                    1_800_000_000
                )
            )
        )
        assertTrue(bridge.snapshot().isLoggedIn)

        val failure = bridge.receive(
            serverFrame(ActionResponsePacket(91, false, "Session expired", -1))
        )

        assertFalse(failure.snapshot.isLoggedIn)
        assertEquals(-1, failure.snapshot.parentId)
        assertEquals("", failure.snapshot.parentProfilePicture)
        assertEquals(emptyList(), failure.snapshot.children)
        assertEquals("", bridge.takeSessionToken())
    }

    private fun decodeCommand(frame: ByteArray): MentoraPacket {
        return PacketFactory.decode(frameCodec.decode(frame))
    }

    private fun serverFrame(packet: MentoraPacket): ByteArray {
        return frameCodec.encode(PacketFactory.encode(packet), 123_456_789)
    }
}
