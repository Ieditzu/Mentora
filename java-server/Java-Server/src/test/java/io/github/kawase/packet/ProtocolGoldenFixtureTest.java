package io.github.kawase.packet;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.github.kawase.packet.impl.auth.AuthPacket;
import io.github.kawase.packet.impl.auth.AuthResponsePacket;
import io.github.kawase.packet.impl.auth.BeginParentTotpEnrollmentPacket;
import io.github.kawase.packet.impl.auth.ConfirmParentTotpEnrollmentPacket;
import io.github.kawase.packet.impl.auth.DisableParentTotpPacket;
import io.github.kawase.packet.impl.auth.FetchParentSecurityStatusPacket;
import io.github.kawase.packet.impl.auth.ParentAuthSessionPacket;
import io.github.kawase.packet.impl.auth.ParentSecondFactorRequiredPacket;
import io.github.kawase.packet.impl.auth.ParentSecurityStatusPacket;
import io.github.kawase.packet.impl.auth.ParentTotpEnrollmentDetailsPacket;
import io.github.kawase.packet.impl.auth.ParentTotpEnrollmentResultPacket;
import io.github.kawase.packet.impl.auth.ResumeParentSessionPacket;
import io.github.kawase.packet.impl.auth.RevokeParentSessionPacket;
import io.github.kawase.packet.impl.auth.VerifyParentSecondFactorPacket;
import io.github.kawase.packet.impl.child.FetchChildrenResponsePacket;
import io.github.kawase.packet.impl.companion.CompanionVoiceAudioPacket;
import io.github.kawase.packet.impl.core.HandShakePacket;
import io.github.kawase.packet.impl.game.LiveSessionUpdatePacket;
import io.github.kawase.packet.impl.language.CodeWorldPythonResponsePacket;
import io.github.kawase.packet.impl.machinelearning.MachineLearningSubmissionResultPacket;
import io.github.kawase.packet.impl.machinelearning.SubmitMachineLearningSolutionPacket;
import io.github.kawase.packet.impl.qr.ChildAuthResponsePacket;
import io.github.kawase.packet.impl.qr.VerifySessionPacket;
import org.junit.jupiter.api.DynamicTest;
import org.junit.jupiter.api.TestFactory;

import javax.crypto.Cipher;
import javax.crypto.spec.IvParameterSpec;
import javax.crypto.spec.SecretKeySpec;
import java.io.InputStream;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.ArrayList;
import java.util.Base64;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;

class ProtocolGoldenFixtureTest {
    private static final String BASE_KEY = "CIOCLIKESKIDSIJIJSDJ1J2313J8123869699696";
    private static final List<String> EXPECTED_VECTOR_NAMES = List.of(
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
            "revoke_all_parent_sessions"
    );
    private final PacketManager packetManager = new PacketManager();

    @TestFactory
    List<DynamicTest> everyJavaPacketMatchesTheIndependentGoldenCorpus() throws Exception {
        final InputStream resource = getClass().getResourceAsStream("/protocol/v1/packets.json");
        assertNotNull(resource, "Canonical protocol fixture was not copied into test resources");
        final JsonNode root;
        try (resource) {
            root = new ObjectMapper().readTree(resource);
        }

        assertEquals(BASE_KEY, root.path("baseKeyUtf8").asText());
        assertEquals(1, root.path("schemaVersion").asInt());
        assertThat(root.path("vectors"))
                .hasSize(EXPECTED_VECTOR_NAMES.size())
                .extracting(vector -> vector.path("name").asText())
                .containsExactlyElementsOf(EXPECTED_VECTOR_NAMES);
        final List<DynamicTest> tests = new ArrayList<>();
        for (final JsonNode vector : root.path("vectors")) {
            tests.add(DynamicTest.dynamicTest(vector.path("name").asText(), () -> {
                final Packet expected = expectedPacket(vector.path("name").asText());
                final byte[] encryptedEnvelope = Base64.getDecoder().decode(
                        vector.path("encryptedEnvelopeBase64").asText()
                );
                final byte[] expectedPayload = Base64.getDecoder().decode(
                        vector.path("plainPayloadBase64").asText()
                );
                final Packet decoded = Packet.construct(ByteBuffer.wrap(encryptedEnvelope), packetManager);

                assertThat(decoded).usingRecursiveComparison().isEqualTo(expected);
                assertEquals(vector.path("packetId").asInt(), decoded.getId());
                assertArrayEquals(expectedPayload, decryptEnvelope(toBytes(expected.encode())));
            }));
        }
        return tests;
    }

    private Packet expectedPacket(final String name) {
        final long childId = 0x0102030405060708L;
        return switch (name) {
            case "handshake_v1_legacy_unicode" -> new HandShakePacket("unity_game/școlar-🎮");
            case "handshake_v2_device_metadata" ->
                    new HandShakePacket("android_parent", 2, "android:pixel-9-pro/🎮");
            case "parent_auth_request" -> new AuthPacket("email-hash-α", "password-hash-β");
            case "parent_auth_response_failure" -> new AuthResponsePacket(
                    false, -1, "Invalid credentials – încearcă din nou", ""
            );
            case "children_response_repeated_records" -> new FetchChildrenResponsePacket(List.of(
                    new FetchChildrenResponsePacket.ChildDto(1, "Ana 🧠", 1250, true, ""),
                    new FetchChildrenResponsePacket.ChildDto(
                            childId, "Ștefan", -25, false, "data:image/png;base64,AAEC"
                    )
            ));
            case "child_auth_success_unicode" ->
                    new ChildAuthResponsePacket(true, childId, "Ștefan 🎓", "tok-abc-123");
            case "verify_child_session_unicode" ->
                    new VerifySessionPacket(childId, "sess-școală-🎮");
            case "companion_voice_audio_binary" -> new CompanionVoiceAudioPacket(
                    16_000,
                    new byte[] { 0x00, (byte) 0xFF, 0x7F, (byte) 0x80, 0x01, (byte) 0xFE, 0x34, 0x12 },
                    "Rudolf context: bună!"
            );
            case "live_session_update_full" -> new LiveSessionUpdatePacket(
                    childId,
                    "Ana",
                    true,
                    "Python Pad 🐍",
                    "print('bună')\n",
                    3,
                    true,
                    "Running attempt",
                    "2026-07-23T09:10:11Z"
            );
            case "codeworld_response_multiline" -> new CodeWorldPythonResponsePacket(
                    "req-cw-0001",
                    "cube beacon 220 33 526 1 3 1\ncolor beacon red",
                    "gata ✓",
                    ""
            );
            case "submit_ml_solution_multiline" -> new SubmitMachineLearningSolutionPacket(
                    "req-ml-0001",
                    "easy-line-of-best-fit",
                    "def solve(train, test):\n    # șir 🎯\n    return [13, 15, 17]\n"
            );
            case "ml_submission_result_json" -> new MachineLearningSubmissionResultPacket(
                    "req-ml-0001",
                    "{\"problemSlug\":\"easy-line-of-best-fit\",\"passed\":true,\"score\":0.95,"
                            + "\"feedback\":\"Foarte bine 🎉\",\"infrastructureError\":false}"
            );
            case "second_factor_required" ->
                    new ParentSecondFactorRequiredPacket("2fa-challenge-0001", 300, true);
            case "verify_second_factor_totp" ->
                    new VerifyParentSecondFactorPacket("2fa-challenge-0001", "123456");
            case "begin_totp_enrollment" ->
                    new BeginParentTotpEnrollmentPacket("sha256:password-hash-α");
            case "totp_enrollment_details" -> new ParentTotpEnrollmentDetailsPacket(
                    "enroll-0001",
                    "JBSWY3DPEHPK3PXP",
                    "otpauth://totp/Mentora:test%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=Mentora"
            );
            case "confirm_totp_enrollment" ->
                    new ConfirmParentTotpEnrollmentPacket("enroll-0001", "654321");
            case "totp_enrollment_result_with_recovery_codes" ->
                    new ParentTotpEnrollmentResultPacket(
                            true,
                            "TOTP enabled – păstrează codurile",
                            List.of("ALPHA-1111", "BRAVO-2222", "ȘCOALA-3333")
                    );
            case "disable_totp" ->
                    new DisableParentTotpPacket("sha256:password-hash-β", "123456");
            case "totp_status_request" -> new FetchParentSecurityStatusPacket();
            case "totp_status_enabled" -> new ParentSecurityStatusPacket(true, 7);
            case "parent_auth_session_success" -> new ParentAuthSessionPacket(
                    true,
                    childId,
                    "Login reușit",
                    "data:image/png;base64,AAEC",
                    "parent-session-token-0001",
                    1_800_000_000L
            );
            case "resume_parent_session" ->
                    new ResumeParentSessionPacket("parent-session-token-0001", "android:pixel-9-pro/🎮");
            case "revoke_all_parent_sessions" ->
                    new RevokeParentSessionPacket("parent-session-token-0001", true);
            default -> throw new IllegalArgumentException("Fixture has no Java packet mapping: " + name);
        };
    }

    private byte[] decryptEnvelope(final byte[] frame) throws Exception {
        final ByteBuffer envelope = ByteBuffer.wrap(frame);
        final int encryptedSeedLength = envelope.getInt();
        final byte[] encryptedSeed = new byte[encryptedSeedLength];
        envelope.get(encryptedSeed);
        final long dynamicSeed = ByteBuffer.wrap(decryptAesCbc(encryptedSeed, BASE_KEY)).getLong();
        final byte[] encryptedPayload = new byte[envelope.remaining()];
        envelope.get(encryptedPayload);
        return decryptAesCbc(encryptedPayload, Long.toString(dynamicSeed));
    }

    private byte[] decryptAesCbc(final byte[] encrypted, final String password) throws Exception {
        final byte[] iv = new byte[16], ciphertext = new byte[encrypted.length - iv.length];
        System.arraycopy(encrypted, 0, iv, 0, iv.length);
        System.arraycopy(encrypted, iv.length, ciphertext, 0, ciphertext.length);
        final Cipher cipher = Cipher.getInstance("AES/CBC/PKCS5Padding");
        cipher.init(
                Cipher.DECRYPT_MODE,
                new SecretKeySpec(
                        MessageDigest.getInstance("SHA-256").digest(password.getBytes(StandardCharsets.UTF_8)),
                        "AES"
                ),
                new IvParameterSpec(iv)
        );
        return cipher.doFinal(ciphertext);
    }

    private byte[] toBytes(final ByteBuffer buffer) {
        final byte[] bytes = new byte[buffer.remaining()];
        buffer.get(bytes);
        return bytes;
    }
}
