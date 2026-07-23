#!/usr/bin/env python3
"""Generate deterministic Mentora protocol vectors with an independent implementation."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import struct
import sys
from pathlib import Path
from typing import Any

try:
    from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
    from cryptography.hazmat.primitives.padding import PKCS7
except ImportError as exception:
    raise SystemExit(
        "The fixture generator requires the `cryptography` Python package."
    ) from exception


BASE_KEY = "CIOCLIKESKIDSIJIJSDJ1J2313J8123869699696"
FIXED_SEED = 1_700_000_000_123_456_789
SEED_IV = bytes(range(0x00, 0x10))
PAYLOAD_IV = bytes(range(0x10, 0x20))
OUTPUT_PATH = Path(__file__).with_name("packets.json")


def int32(value: int) -> bytes:
    return struct.pack(">i", value)


def int64(value: int) -> bytes:
    return struct.pack(">q", value)


def boolean(value: bool) -> bytes:
    return bytes((1 if value else 0,))


def string(value: str) -> bytes:
    encoded = value.encode("utf-8")
    return int32(len(encoded)) + encoded


def binary(value: bytes) -> bytes:
    return int32(len(value)) + value


def field(name: str, kind: str, value: Any) -> dict[str, Any]:
    if isinstance(value, bool):
        rendered = "true" if value else "false"
    else:
        rendered = str(value)
    return {"name": name, "kind": kind, "value": rendered}


def aes_cbc_encrypt(data: bytes, password: str, iv: bytes) -> bytes:
    key = hashlib.sha256(password.encode("utf-8")).digest()
    padder = PKCS7(128).padder()
    padded = padder.update(data) + padder.finalize()
    encryptor = Cipher(algorithms.AES(key), modes.CBC(iv)).encryptor()
    return iv + encryptor.update(padded) + encryptor.finalize()


def make_vector(
    name: str,
    packet_id: int,
    direction: str,
    body: bytes,
    fields: list[dict[str, Any]],
    *,
    unity_decode: bool,
    unity_encode: bool,
) -> dict[str, Any]:
    payload = int32(packet_id) + body
    encrypted_seed = aes_cbc_encrypt(int64(FIXED_SEED), BASE_KEY, SEED_IV)
    encrypted_payload = aes_cbc_encrypt(
        payload, str(FIXED_SEED), PAYLOAD_IV
    )
    envelope = int32(len(encrypted_seed)) + encrypted_seed + encrypted_payload

    return {
        "name": name,
        "packetId": packet_id,
        "direction": direction,
        "unityDecode": unity_decode,
        "unityEncode": unity_encode,
        "fields": fields,
        "plainPayloadBase64": base64.b64encode(payload).decode("ascii"),
        "encryptedEnvelopeBase64": base64.b64encode(envelope).decode("ascii"),
        "plainSha256": hashlib.sha256(payload).hexdigest(),
        "envelopeSha256": hashlib.sha256(envelope).hexdigest(),
    }


def build_vectors() -> list[dict[str, Any]]:
    child_id = 0x0102030405060708
    pcm16 = bytes((0x00, 0xFF, 0x7F, 0x80, 0x01, 0xFE, 0x34, 0x12))
    source_code = (
        "def solve(train, test):\n"
        "    # șir 🎯\n"
        "    return [13, 15, 17]\n"
    )
    commands = (
        "cube beacon 220 33 526 1 3 1\n"
        "color beacon red"
    )
    result_json = (
        '{"problemSlug":"easy-line-of-best-fit","passed":true,'
        '"score":0.95,"feedback":"Foarte bine 🎉",'
        '"infrastructureError":false}'
    )

    vectors = [
        make_vector(
            "handshake_v1_legacy_unicode",
            1,
            "client-to-backend",
            string("unity_game/școlar-🎮"),
            [field("hostId", "string", "unity_game/școlar-🎮")],
            unity_decode=True,
            unity_encode=True,
        ),
        make_vector(
            "handshake_v2_device_metadata",
            1,
            "client-to-backend",
            string("android_parent")
            + int32(2)
            + string("android:pixel-9-pro/🎮"),
            [
                field("fingerprint", "string", "android_parent"),
                field("protocolVersion", "int32", 2),
                field(
                    "deviceId",
                    "string",
                    "android:pixel-9-pro/🎮",
                ),
            ],
            unity_decode=True,
            unity_encode=False,
        ),
        make_vector(
            "parent_auth_request",
            2,
            "client-to-backend",
            string("email-hash-α") + string("password-hash-β"),
            [
                field("emailHash", "string", "email-hash-α"),
                field("passwordHash", "string", "password-hash-β"),
            ],
            unity_decode=True,
            unity_encode=True,
        ),
        make_vector(
            "parent_auth_response_failure",
            10,
            "backend-to-client",
            boolean(False)
            + int64(-1)
            + string("Invalid credentials – încearcă din nou")
            + string(""),
            [
                field("success", "bool", False),
                field("parentId", "int64", -1),
                field(
                    "message",
                    "string",
                    "Invalid credentials – încearcă din nou",
                ),
                field("parentPfp", "string", ""),
            ],
            unity_decode=True,
            unity_encode=True,
        ),
        make_vector(
            "children_response_repeated_records",
            16,
            "backend-to-client",
            int32(2)
            + int64(1)
            + string("Ana 🧠")
            + int32(1250)
            + boolean(True)
            + string("")
            + int64(child_id)
            + string("Ștefan")
            + int32(-25)
            + boolean(False)
            + string("data:image/png;base64,AAEC"),
            [
                field("children.count", "int32", 2),
                field("children[0].id", "int64", 1),
                field("children[0].name", "string", "Ana 🧠"),
                field("children[0].totalPoints", "int32", 1250),
                field("children[0].isOnline", "bool", True),
                field("children[0].profilePicture", "string", ""),
                field("children[1].id", "int64", child_id),
                field("children[1].name", "string", "Ștefan"),
                field("children[1].totalPoints", "int32", -25),
                field("children[1].isOnline", "bool", False),
                field(
                    "children[1].profilePicture",
                    "string",
                    "data:image/png;base64,AAEC",
                ),
            ],
            unity_decode=True,
            unity_encode=False,
        ),
        make_vector(
            "child_auth_success_unicode",
            22,
            "backend-to-client",
            boolean(True)
            + int64(child_id)
            + string("Ștefan 🎓")
            + string("tok-abc-123"),
            [
                field("success", "bool", True),
                field("childId", "int64", child_id),
                field("childName", "string", "Ștefan 🎓"),
                field("sessionToken", "string", "tok-abc-123"),
            ],
            unity_decode=True,
            unity_encode=True,
        ),
        make_vector(
            "verify_child_session_unicode",
            25,
            "client-to-backend",
            int64(child_id) + string("sess-școală-🎮"),
            [
                field("childId", "int64", child_id),
                field("sessionToken", "string", "sess-școală-🎮"),
            ],
            unity_decode=True,
            unity_encode=True,
        ),
        make_vector(
            "companion_voice_audio_binary",
            59,
            "client-to-backend",
            int32(16_000)
            + binary(pcm16)
            + string("Rudolf context: bună!"),
            [
                field("sampleRate", "int32", 16_000),
                field("pcm16", "hex", pcm16.hex()),
                field("context", "string", "Rudolf context: bună!"),
            ],
            unity_decode=True,
            unity_encode=True,
        ),
        make_vector(
            "live_session_update_full",
            65,
            "client-to-backend",
            int64(child_id)
            + string("Ana")
            + boolean(True)
            + string("Python Pad 🐍")
            + string("print('bună')\n")
            + int32(3)
            + boolean(True)
            + string("Running attempt")
            + string("2026-07-23T09:10:11Z"),
            [
                field("childId", "int64", child_id),
                field("childName", "string", "Ana"),
                field("online", "bool", True),
                field("padName", "string", "Python Pad 🐍"),
                field("codeText", "string", "print('bună')\n"),
                field("attemptCount", "int32", 3),
                field("hintRequested", "bool", True),
                field("status", "string", "Running attempt"),
                field("updatedAt", "string", "2026-07-23T09:10:11Z"),
            ],
            unity_decode=True,
            unity_encode=True,
        ),
        make_vector(
            "codeworld_response_multiline",
            75,
            "backend-to-client",
            string("req-cw-0001")
            + string(commands)
            + string("gata ✓")
            + string(""),
            [
                field("requestId", "string", "req-cw-0001"),
                field("commandsText", "string", commands),
                field("output", "string", "gata ✓"),
                field("error", "string", ""),
            ],
            unity_decode=True,
            unity_encode=False,
        ),
        make_vector(
            "submit_ml_solution_multiline",
            79,
            "client-to-backend",
            string("req-ml-0001")
            + string("easy-line-of-best-fit")
            + string(source_code),
            [
                field("requestId", "string", "req-ml-0001"),
                field(
                    "problemSlug",
                    "string",
                    "easy-line-of-best-fit",
                ),
                field("sourceCode", "string", source_code),
            ],
            unity_decode=True,
            unity_encode=True,
        ),
        make_vector(
            "ml_submission_result_json",
            80,
            "backend-to-client",
            string("req-ml-0001") + string(result_json),
            [
                field("requestId", "string", "req-ml-0001"),
                field("resultJson", "string", result_json),
            ],
            unity_decode=True,
            unity_encode=True,
        ),
        make_vector(
            "second_factor_required",
            81,
            "backend-to-client",
            string("2fa-challenge-0001")
            + int32(300)
            + boolean(True),
            [
                field("challengeId", "string", "2fa-challenge-0001"),
                field("expiresInSeconds", "int32", 300),
                field("recoveryAllowed", "bool", True),
            ],
            unity_decode=False,
            unity_encode=False,
        ),
        make_vector(
            "verify_second_factor_totp",
            82,
            "client-to-backend",
            string("2fa-challenge-0001") + string("123456"),
            [
                field("challengeId", "string", "2fa-challenge-0001"),
                field("code", "string", "123456"),
            ],
            unity_decode=False,
            unity_encode=False,
        ),
        make_vector(
            "begin_totp_enrollment",
            83,
            "client-to-backend",
            string("sha256:password-hash-α"),
            [
                field(
                    "passwordHash",
                    "string",
                    "sha256:password-hash-α",
                )
            ],
            unity_decode=False,
            unity_encode=False,
        ),
        make_vector(
            "totp_enrollment_details",
            84,
            "backend-to-client",
            string("enroll-0001")
            + string("JBSWY3DPEHPK3PXP")
            + string(
                "otpauth://totp/Mentora:test%40example.com"
                "?secret=JBSWY3DPEHPK3PXP&issuer=Mentora"
            ),
            [
                field("enrollmentId", "string", "enroll-0001"),
                field(
                    "secretBase32",
                    "string",
                    "JBSWY3DPEHPK3PXP",
                ),
                field(
                    "otpAuthUri",
                    "string",
                    "otpauth://totp/Mentora:test%40example.com"
                    "?secret=JBSWY3DPEHPK3PXP&issuer=Mentora",
                ),
            ],
            unity_decode=False,
            unity_encode=False,
        ),
        make_vector(
            "confirm_totp_enrollment",
            85,
            "client-to-backend",
            string("enroll-0001") + string("654321"),
            [
                field("enrollmentId", "string", "enroll-0001"),
                field("code", "string", "654321"),
            ],
            unity_decode=False,
            unity_encode=False,
        ),
        make_vector(
            "totp_enrollment_result_with_recovery_codes",
            86,
            "backend-to-client",
            boolean(True)
            + string("TOTP enabled – păstrează codurile")
            + int32(3)
            + string("ALPHA-1111")
            + string("BRAVO-2222")
            + string("ȘCOALA-3333"),
            [
                field("success", "bool", True),
                field(
                    "message",
                    "string",
                    "TOTP enabled – păstrează codurile",
                ),
                field("recoveryCodes.count", "int32", 3),
                field(
                    "recoveryCodes[0]",
                    "string",
                    "ALPHA-1111",
                ),
                field(
                    "recoveryCodes[1]",
                    "string",
                    "BRAVO-2222",
                ),
                field(
                    "recoveryCodes[2]",
                    "string",
                    "ȘCOALA-3333",
                ),
            ],
            unity_decode=False,
            unity_encode=False,
        ),
        make_vector(
            "disable_totp",
            87,
            "client-to-backend",
            string("sha256:password-hash-β") + string("123456"),
            [
                field(
                    "passwordHash",
                    "string",
                    "sha256:password-hash-β",
                ),
                field("code", "string", "123456"),
            ],
            unity_decode=False,
            unity_encode=False,
        ),
        make_vector(
            "totp_status_request",
            88,
            "client-to-backend",
            b"",
            [],
            unity_decode=False,
            unity_encode=False,
        ),
        make_vector(
            "totp_status_enabled",
            89,
            "backend-to-client",
            boolean(True) + int32(7),
            [
                field("totpEnabled", "bool", True),
                field("recoveryCodesRemaining", "int32", 7),
            ],
            unity_decode=False,
            unity_encode=False,
        ),
        make_vector(
            "parent_auth_session_success",
            90,
            "backend-to-client",
            boolean(True)
            + int64(child_id)
            + string("Login reușit")
            + string("data:image/png;base64,AAEC")
            + string("parent-session-token-0001")
            + int64(1_800_000_000),
            [
                field("success", "bool", True),
                field("parentId", "int64", child_id),
                field("message", "string", "Login reușit"),
                field(
                    "parentPfp",
                    "string",
                    "data:image/png;base64,AAEC",
                ),
                field(
                    "sessionToken",
                    "string",
                    "parent-session-token-0001",
                ),
                field(
                    "expiresAtEpochSeconds",
                    "int64",
                    1_800_000_000,
                ),
            ],
            unity_decode=False,
            unity_encode=False,
        ),
        make_vector(
            "resume_parent_session",
            91,
            "client-to-backend",
            string("parent-session-token-0001")
            + string("android:pixel-9-pro/🎮"),
            [
                field(
                    "sessionToken",
                    "string",
                    "parent-session-token-0001",
                ),
                field(
                    "deviceId",
                    "string",
                    "android:pixel-9-pro/🎮",
                ),
            ],
            unity_decode=False,
            unity_encode=False,
        ),
        make_vector(
            "revoke_all_parent_sessions",
            92,
            "client-to-backend",
            string("parent-session-token-0001") + boolean(True),
            [
                field(
                    "sessionToken",
                    "string",
                    "parent-session-token-0001",
                ),
                field("revokeAll", "bool", True),
            ],
            unity_decode=False,
            unity_encode=False,
        ),
    ]

    packet_ids = [vector["packetId"] for vector in vectors]
    if packet_ids != sorted(packet_ids):
        raise AssertionError("Protocol vectors must remain sorted by packet ID.")
    return vectors


def build_corpus() -> dict[str, Any]:
    return {
        "schemaVersion": 1,
        "protocolVersion": 1,
        "baseKeyUtf8": BASE_KEY,
        "fixedSeedDecimal": str(FIXED_SEED),
        "seedIvHex": SEED_IV.hex(),
        "payloadIvHex": PAYLOAD_IV.hex(),
        "vectors": build_vectors(),
    }


def render_corpus() -> str:
    return json.dumps(
        build_corpus(),
        ensure_ascii=False,
        indent=2,
    ) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument(
        "--write",
        action="store_true",
        help="write packets.json beside this script",
    )
    mode.add_argument(
        "--check",
        action="store_true",
        help="verify packets.json exactly matches generated output",
    )
    arguments = parser.parse_args()
    rendered = render_corpus()

    if arguments.write:
        OUTPUT_PATH.write_text(rendered, encoding="utf-8", newline="\n")
        print(f"Wrote {OUTPUT_PATH}")
        return 0

    if arguments.check:
        if not OUTPUT_PATH.exists():
            print(f"Missing canonical fixture: {OUTPUT_PATH}", file=sys.stderr)
            return 1
        existing = OUTPUT_PATH.read_text(encoding="utf-8")
        if existing != rendered:
            print(
                "packets.json is stale; run generate_vectors.py --write",
                file=sys.stderr,
            )
            return 1
        print(f"Verified {OUTPUT_PATH}")
        return 0

    sys.stdout.write(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
