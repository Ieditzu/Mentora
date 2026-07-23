package io.github.kawase.socket

import io.github.kawase.shared.protocol.PacketFactory
import io.github.kawase.shared.protocol.PacketFrameCodec
import io.github.kawase.socket.packet.Packet
import io.github.kawase.socket.packet.PacketManager
import org.json.JSONObject
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Test
import java.nio.ByteBuffer
import java.util.Base64

class ProtocolGoldenFixtureIntegrationTest {
    private val supportedPacketIds = setOf(1, 2, 10, 16, 65) + (81..92)
    private val expectedVectorNames = setOf(
        "handshake_v1_legacy_unicode",
        "handshake_v2_device_metadata",
        "parent_auth_request",
        "parent_auth_response_failure",
        "children_response_repeated_records",
        "child_auth_success_unicode",
        "verify_child_session_unicode",
        "companion_voice_audio_binary",
        "live_session_update_full",
        "codeworld_response_multiline",
        "submit_ml_solution_multiline",
        "ml_submission_result_json",
        "second_factor_required",
        "verify_second_factor_totp",
        "begin_totp_enrollment",
        "totp_enrollment_details",
        "confirm_totp_enrollment",
        "totp_enrollment_result_with_recovery_codes",
        "disable_totp",
        "totp_status_request",
        "totp_status_enabled",
        "parent_auth_session_success",
        "resume_parent_session",
        "revoke_all_parent_sessions",
    )

    @Test
    fun `golden encrypted frames decode and re-encode through both mobile codecs`() {
        val fixture = checkNotNull(javaClass.getResourceAsStream("/packets.json")) {
            "The canonical protocol fixture was not added to the Android test resources."
        }.bufferedReader().use { JSONObject(it.readText()) }
        val frameCodec = PacketFrameCodec(fixture.getString("baseKeyUtf8"))
        val vectors = fixture.getJSONArray("vectors")
        assertEquals(24, vectors.length())
        assertEquals(
            expectedVectorNames,
            (0 until vectors.length()).map { vectors.getJSONObject(it).getString("name") }.toSet(),
        )
        var verifiedVectorCount = 0

        for (index in 0 until vectors.length()) {
            val vector = vectors.getJSONObject(index)
            val packetId = vector.getInt("packetId")
            if (packetId !in supportedPacketIds) continue

            val expectedPayload = Base64.getDecoder().decode(vector.getString("plainPayloadBase64"))
            val encryptedEnvelope = Base64.getDecoder().decode(vector.getString("encryptedEnvelopeBase64"))

            val sharedPacket = PacketFactory.decode(frameCodec.decode(encryptedEnvelope))
            assertEquals(vector.getString("name"), packetId, sharedPacket.id)
            assertArrayEquals(vector.getString("name"), expectedPayload, PacketFactory.encode(sharedPacket))

            val legacyPacket = Packet.construct(ByteBuffer.wrap(encryptedEnvelope), PacketManager())
            assertEquals(vector.getString("name"), packetId, legacyPacket.id)
            assertArrayEquals(
                vector.getString("name"),
                expectedPayload,
                frameCodec.decode(legacyPacket.encode().remainingBytes())
            )
            verifiedVectorCount++
        }

        assertEquals(18, verifiedVectorCount)
    }

    private fun ByteBuffer.remainingBytes(): ByteArray {
        return ByteArray(remaining()).also(::get)
    }
}
