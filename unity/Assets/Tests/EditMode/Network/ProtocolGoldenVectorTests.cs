using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mentora.Network;
using NUnit.Framework;
using UnityEngine;

namespace Mentora.Network.Tests
{
    [Category("Integration")]
    public sealed class ProtocolGoldenVectorTests
    {
        private const long SharedChildId = 0x0102030405060708;
        private const string SourceCode =
            "def solve(train, test):\n" +
            "    # șir 🎯\n" +
            "    return [13, 15, 17]\n";
        private const string Commands =
            "cube beacon 220 33 526 1 3 1\n" +
            "color beacon red";
        private const string ResultJson =
            "{\"problemSlug\":\"easy-line-of-best-fit\",\"passed\":true," +
            "\"score\":0.95,\"feedback\":\"Foarte bine 🎉\"," +
            "\"infrastructureError\":false}";

        private static readonly byte[] VoicePcm16 =
        {
            0x00, 0xFF, 0x7F, 0x80, 0x01, 0xFE, 0x34, 0x12
        };

        private ProtocolCorpus corpus;

        [SetUp]
        public void SetUp()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "test-fixtures",
                "protocol",
                "v1",
                "packets.json"));
            Assert.That(File.Exists(path), Is.True, "Missing shared protocol fixture: " + path);

            corpus = JsonUtility.FromJson<ProtocolCorpus>(File.ReadAllText(path, Encoding.UTF8));
            Assert.That(corpus, Is.Not.Null);
            Assert.That(corpus.vectors, Is.Not.Null);
        }

        [Test]
        public void CorpusContainsFrozenCrossClientPacketSet()
        {
            Assert.That(corpus.schemaVersion, Is.EqualTo(1));
            Assert.That(corpus.protocolVersion, Is.EqualTo(1));
            Assert.That(corpus.baseKeyUtf8, Is.EqualTo(Packet.BaseKey));
            Assert.That(corpus.vectors.Length, Is.EqualTo(24));

            string[] expectedNames =
            {
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
            };

            CollectionAssert.AreEquivalent(
                expectedNames,
                corpus.vectors.Select(vector => vector.name).ToArray());

            for (int packetId = 81; packetId <= 92; packetId++)
            {
                Assert.That(
                    corpus.vectors.Count(vector => vector.packetId == packetId),
                    Is.EqualTo(1),
                    "Expected one frozen vector for packet ID " + packetId + ".");
            }
        }

        [Test]
        public void IndependentDecoderValidatesEveryCanonicalEnvelope()
        {
            long expectedSeed = long.Parse(
                corpus.fixedSeedDecimal,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);

            foreach (ProtocolVector vector in corpus.vectors)
            {
                byte[] expectedPayload = Convert.FromBase64String(vector.plainPayloadBase64);
                byte[] envelope = Convert.FromBase64String(vector.encryptedEnvelopeBase64);
                ReferenceEnvelope decoded = ReferenceProtocol.DecodeEnvelope(
                    envelope,
                    corpus.baseKeyUtf8);

                Assert.That(
                    decoded.Seed,
                    Is.EqualTo(expectedSeed),
                    vector.name + " seed");
                CollectionAssert.AreEqual(
                    expectedPayload,
                    decoded.Payload,
                    vector.name + " plaintext");
                Assert.That(
                    ReferenceProtocol.Sha256Hex(expectedPayload),
                    Is.EqualTo(vector.plainSha256),
                    vector.name + " plaintext hash");
                Assert.That(
                    ReferenceProtocol.Sha256Hex(envelope),
                    Is.EqualTo(vector.envelopeSha256),
                    vector.name + " envelope hash");
                Assert.That(
                    ReferenceProtocol.ReadInt32(expectedPayload, 0),
                    Is.EqualTo(vector.packetId),
                    vector.name + " packet ID");
            }
        }

        [Test]
        public void ProductionDecoderReadsEveryUnitySupportedGoldenEnvelope()
        {
            var manager = new PacketManager();
            foreach (ProtocolVector vector in corpus.vectors.Where(item => item.unityDecode))
            {
                Packet packet = Packet.Decode(
                    Convert.FromBase64String(vector.encryptedEnvelopeBase64),
                    manager);

                Assert.That(packet.Id, Is.EqualTo(vector.packetId), vector.name);
                AssertDecodedSemantics(vector.name, packet);
            }
        }

        [Test]
        public void ProductionRawEncoderMatchesCanonicalPayloads()
        {
            foreach (ProtocolVector vector in corpus.vectors.Where(item => item.unityEncode))
            {
                Packet packet = CreatePacketForEncoding(vector.name);
                byte[] expectedPayload = Convert.FromBase64String(vector.plainPayloadBase64);
                CollectionAssert.AreEqual(
                    expectedPayload,
                    packet.EncodeRaw(),
                    vector.name);
            }
        }

        [Test]
        public void ProductionEncryptedEncoderMatchesCanonicalPayloadsThroughIndependentDecoder()
        {
            foreach (ProtocolVector vector in corpus.vectors.Where(item => item.unityEncode))
            {
                Packet packet = CreatePacketForEncoding(vector.name);
                byte[] expectedPayload = Convert.FromBase64String(vector.plainPayloadBase64);
                ReferenceEnvelope decoded = ReferenceProtocol.DecodeEnvelope(
                    packet.Encode(),
                    corpus.baseKeyUtf8);

                CollectionAssert.AreEqual(
                    expectedPayload,
                    decoded.Payload,
                    vector.name);
            }
        }

        [Test]
        public void ProductionEncryptedEncoderUsesInvariantSeedPassword()
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
                CultureInfo.CurrentUICulture = new CultureInfo("ar-SA");

                ProtocolVector vector = FindVector("verify_child_session_unicode");
                Packet packet = CreatePacketForEncoding(vector.name);
                ReferenceEnvelope decoded = ReferenceProtocol.DecodeEnvelope(
                    packet.Encode(),
                    corpus.baseKeyUtf8);

                CollectionAssert.AreEqual(
                    Convert.FromBase64String(vector.plainPayloadBase64),
                    decoded.Payload);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }

        private ProtocolVector FindVector(string name)
        {
            ProtocolVector vector = corpus.vectors.FirstOrDefault(
                candidate => candidate.name == name);
            Assert.That(vector, Is.Not.Null, "Missing vector " + name + ".");
            return vector;
        }

        private static Packet CreatePacketForEncoding(string name)
        {
            switch (name)
            {
                case "handshake_v1_legacy_unicode":
                    return new HandShakePacket("unity_game/școlar-🎮");
                case "parent_auth_request":
                    return new AuthPacket("email-hash-α", "password-hash-β");
                case "parent_auth_response_failure":
                    return new AuthResponsePacket
                    {
                        Success = false,
                        ParentId = -1,
                        Message = "Invalid credentials – încearcă din nou",
                        ParentPfp = string.Empty
                    };
                case "child_auth_success_unicode":
                    return new ChildAuthResponsePacket
                    {
                        Success = true,
                        ChildId = SharedChildId,
                        ChildName = "Ștefan 🎓",
                        SessionToken = "tok-abc-123"
                    };
                case "verify_child_session_unicode":
                    return new VerifySessionPacket(SharedChildId, "sess-școală-🎮");
                case "companion_voice_audio_binary":
                    return new CompanionVoiceAudioPacket(
                        16000,
                        (byte[])VoicePcm16.Clone(),
                        "Rudolf context: bună!");
                case "live_session_update_full":
                    return new LiveSessionUpdatePacket(
                        SharedChildId,
                        "Ana",
                        true,
                        "Python Pad 🐍",
                        "print('bună')\n",
                        3,
                        true,
                        "Running attempt",
                        "2026-07-23T09:10:11Z");
                case "submit_ml_solution_multiline":
                    return new SubmitMlSolutionPacket(
                        "req-ml-0001",
                        "easy-line-of-best-fit",
                        SourceCode);
                case "ml_submission_result_json":
                    return new MlSubmissionResultPacket("req-ml-0001", ResultJson);
                default:
                    throw new AssertionException(
                        "No Unity encoder fixture is defined for " + name + ".");
            }
        }

        private static void AssertDecodedSemantics(string name, Packet packet)
        {
            switch (name)
            {
                case "handshake_v1_legacy_unicode":
                    Assert.That(
                        ((HandShakePacket)packet).HostId,
                        Is.EqualTo("unity_game/școlar-🎮"));
                    break;
                case "handshake_v2_device_metadata":
                    Assert.That(
                        ((HandShakePacket)packet).HostId,
                        Is.EqualTo("android_parent"));
                    break;
                case "parent_auth_request":
                {
                    var auth = (AuthPacket)packet;
                    Assert.That(auth.EmailHash, Is.EqualTo("email-hash-α"));
                    Assert.That(auth.PasswordHash, Is.EqualTo("password-hash-β"));
                    break;
                }
                case "parent_auth_response_failure":
                {
                    var response = (AuthResponsePacket)packet;
                    Assert.That(response.Success, Is.False);
                    Assert.That(response.ParentId, Is.EqualTo(-1));
                    Assert.That(
                        response.Message,
                        Is.EqualTo("Invalid credentials – încearcă din nou"));
                    Assert.That(response.ParentPfp, Is.Empty);
                    break;
                }
                case "children_response_repeated_records":
                {
                    var response = (FetchChildrenResponsePacket)packet;
                    Assert.That(response.Children.Count, Is.EqualTo(2));
                    Assert.That(response.Children[0].Id, Is.EqualTo(1));
                    Assert.That(response.Children[0].Name, Is.EqualTo("Ana 🧠"));
                    Assert.That(response.Children[0].TotalPoints, Is.EqualTo(1250));
                    Assert.That(response.Children[0].IsOnline, Is.True);
                    Assert.That(response.Children[0].ProfilePicture, Is.Empty);
                    Assert.That(response.Children[1].Id, Is.EqualTo(SharedChildId));
                    Assert.That(response.Children[1].Name, Is.EqualTo("Ștefan"));
                    Assert.That(response.Children[1].TotalPoints, Is.EqualTo(-25));
                    Assert.That(response.Children[1].IsOnline, Is.False);
                    Assert.That(
                        response.Children[1].ProfilePicture,
                        Is.EqualTo("data:image/png;base64,AAEC"));
                    break;
                }
                case "child_auth_success_unicode":
                {
                    var response = (ChildAuthResponsePacket)packet;
                    Assert.That(response.Success, Is.True);
                    Assert.That(response.ChildId, Is.EqualTo(SharedChildId));
                    Assert.That(response.ChildName, Is.EqualTo("Ștefan 🎓"));
                    Assert.That(response.SessionToken, Is.EqualTo("tok-abc-123"));
                    break;
                }
                case "verify_child_session_unicode":
                {
                    var request = (VerifySessionPacket)packet;
                    Assert.That(request.ChildId, Is.EqualTo(SharedChildId));
                    Assert.That(request.SessionToken, Is.EqualTo("sess-școală-🎮"));
                    break;
                }
                case "companion_voice_audio_binary":
                {
                    var audio = (CompanionVoiceAudioPacket)packet;
                    Assert.That(audio.SampleRate, Is.EqualTo(16000));
                    CollectionAssert.AreEqual(VoicePcm16, audio.Pcm16);
                    Assert.That(audio.Context, Is.EqualTo("Rudolf context: bună!"));
                    break;
                }
                case "live_session_update_full":
                {
                    var update = (LiveSessionUpdatePacket)packet;
                    Assert.That(update.ChildId, Is.EqualTo(SharedChildId));
                    Assert.That(update.ChildName, Is.EqualTo("Ana"));
                    Assert.That(update.Online, Is.True);
                    Assert.That(update.PadName, Is.EqualTo("Python Pad 🐍"));
                    Assert.That(update.CodeText, Is.EqualTo("print('bună')\n"));
                    Assert.That(update.AttemptCount, Is.EqualTo(3));
                    Assert.That(update.HintRequested, Is.True);
                    Assert.That(update.Status, Is.EqualTo("Running attempt"));
                    Assert.That(update.UpdatedAt, Is.EqualTo("2026-07-23T09:10:11Z"));
                    break;
                }
                case "codeworld_response_multiline":
                {
                    var response = (CodeWorldPythonResponsePacket)packet;
                    Assert.That(response.RequestId, Is.EqualTo("req-cw-0001"));
                    Assert.That(response.CommandsText, Is.EqualTo(Commands));
                    Assert.That(response.Output, Is.EqualTo("gata ✓"));
                    Assert.That(response.Error, Is.Empty);
                    break;
                }
                case "submit_ml_solution_multiline":
                {
                    var submission = (SubmitMlSolutionPacket)packet;
                    Assert.That(submission.RequestId, Is.EqualTo("req-ml-0001"));
                    Assert.That(
                        submission.ProblemSlug,
                        Is.EqualTo("easy-line-of-best-fit"));
                    Assert.That(submission.SourceCode, Is.EqualTo(SourceCode));
                    break;
                }
                case "ml_submission_result_json":
                {
                    var result = (MlSubmissionResultPacket)packet;
                    Assert.That(result.RequestId, Is.EqualTo("req-ml-0001"));
                    Assert.That(result.ResultJson, Is.EqualTo(ResultJson));
                    break;
                }
                default:
                    throw new AssertionException(
                        "No Unity decoder assertion is defined for " + name + ".");
            }
        }
    }

    [Category("Integration")]
    public sealed class PacketMalformedFrameTests
    {
        private PacketManager manager;

        [SetUp]
        public void SetUp()
        {
            manager = new PacketManager();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("AA==")]
        [TestCase("AAAA")]
        public void DecodeRejectsMissingOrShortEncryptedEnvelope(string base64)
        {
            byte[] frame = base64 == null ? null : Convert.FromBase64String(base64);
            Assert.Throws<PacketFormatException>(() => Packet.Decode(frame, manager));
        }

        [Test]
        public void DecodeRejectsOversizedEncryptedEnvelope()
        {
            var frame = new byte[Packet.MaximumEncryptedFrameBytes + 1];
            Assert.Throws<PacketFormatException>(() => Packet.Decode(frame, manager));
        }

        [TestCase(0)]
        [TestCase(16)]
        [TestCase(1025)]
        public void DecodeRejectsInvalidEncryptedSeedLength(int seedLength)
        {
            var frame = new byte[4 + 32 + 32];
            WriteInt32(frame, 0, seedLength);
            Assert.Throws<PacketFormatException>(() => Packet.Decode(frame, manager));
        }

        [Test]
        public void DecodeRejectsSeedThatExceedsRemainingEnvelope()
        {
            var frame = new byte[4 + 32 + 32];
            WriteInt32(frame, 0, 64);
            Assert.Throws<PacketFormatException>(() => Packet.Decode(frame, manager));
        }

        [Test]
        public void DecodeRejectsTruncatedEncryptedPayloadBlock()
        {
            byte[] valid = LoadCanonicalEnvelope("verify_child_session_unicode");
            Array.Resize(ref valid, valid.Length - 1);
            Assert.Throws<PacketFormatException>(() => Packet.Decode(valid, manager));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("AA==")]
        [TestCase("AAAA")]
        public void DecodeRawRejectsMissingOrShortPacketId(string base64)
        {
            byte[] frame = base64 == null ? null : Convert.FromBase64String(base64);
            Assert.Throws<PacketFormatException>(() => Packet.DecodeRaw(frame, manager));
        }

        [Test]
        public void DecodeRawRejectsOversizedFrame()
        {
            var frame = new byte[Packet.MaximumRawFrameBytes + 1];
            Assert.Throws<PacketFormatException>(() => Packet.DecodeRaw(frame, manager));
        }

        [Test]
        public void DecodeRawRejectsUnknownPacketId()
        {
            byte[] frame = { 0x00, 0x00, 0x03, 0xE7 };
            Assert.Throws<PacketFormatException>(() => Packet.DecodeRaw(frame, manager));
        }

        [Test]
        public void DecodeRawRejectsNegativeStringLength()
        {
            byte[] frame =
            {
                0x00, 0x00, 0x00, 0x01,
                0xFF, 0xFF, 0xFF, 0xFF
            };
            Assert.Throws<PacketFormatException>(() => Packet.DecodeRaw(frame, manager));
        }

        [Test]
        public void DecodeRawRejectsStringLongerThanRemainingFrame()
        {
            byte[] frame =
            {
                0x00, 0x00, 0x00, 0x01,
                0x00, 0x00, 0x00, 0x05,
                0x61, 0x62
            };
            Assert.Throws<PacketFormatException>(() => Packet.DecodeRaw(frame, manager));
        }

        [Test]
        public void DecodeRawRejectsInvalidUtf8()
        {
            byte[] frame =
            {
                0x00, 0x00, 0x00, 0x01,
                0x00, 0x00, 0x00, 0x01,
                0xFF
            };
            Assert.Throws<PacketFormatException>(() => Packet.DecodeRaw(frame, manager));
        }

        [Test]
        public void EncryptionUtilityRejectsMissingIvOrPartialCipherBlock()
        {
            Assert.Throws<PacketFormatException>(
                () => EncryptionUtility.DecryptBytes(new byte[31], "test"));
            Assert.Throws<PacketFormatException>(
                () => EncryptionUtility.DecryptBytes(new byte[33], "test"));
        }

        private static byte[] LoadCanonicalEnvelope(string vectorName)
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "test-fixtures",
                "protocol",
                "v1",
                "packets.json"));
            ProtocolCorpus corpus = JsonUtility.FromJson<ProtocolCorpus>(
                File.ReadAllText(path, Encoding.UTF8));
            ProtocolVector vector = corpus.vectors.First(
                item => item.name == vectorName);
            return Convert.FromBase64String(vector.encryptedEnvelopeBase64);
        }

        private static void WriteInt32(byte[] target, int offset, int value)
        {
            target[offset] = (byte)(value >> 24);
            target[offset + 1] = (byte)(value >> 16);
            target[offset + 2] = (byte)(value >> 8);
            target[offset + 3] = (byte)value;
        }
    }

    [Serializable]
    internal sealed class ProtocolCorpus
    {
        public int schemaVersion;
        public int protocolVersion;
        public string baseKeyUtf8;
        public string fixedSeedDecimal;
        public string seedIvHex;
        public string payloadIvHex;
        public ProtocolVector[] vectors;
    }

    [Serializable]
    internal sealed class ProtocolVector
    {
        public string name;
        public int packetId;
        public string direction;
        public bool unityDecode;
        public bool unityEncode;
        public ExpectedField[] fields;
        public string plainPayloadBase64;
        public string encryptedEnvelopeBase64;
        public string plainSha256;
        public string envelopeSha256;
    }

    [Serializable]
    internal sealed class ExpectedField
    {
        public string name;
        public string kind;
        public string value;
    }

    internal sealed class ReferenceEnvelope
    {
        public long Seed;
        public byte[] Payload;
    }

    internal static class ReferenceProtocol
    {
        private const int IvLength = 16;

        public static ReferenceEnvelope DecodeEnvelope(byte[] envelope, string baseKey)
        {
            if (envelope == null || envelope.Length < 4)
            {
                throw new InvalidDataException("Reference envelope is missing.");
            }

            int seedLength = ReadInt32(envelope, 0);
            if (seedLength < IvLength || seedLength > envelope.Length - 4)
            {
                throw new InvalidDataException("Reference seed length is invalid.");
            }

            byte[] encryptedSeed = Slice(envelope, 4, seedLength);
            byte[] seedBytes = Decrypt(encryptedSeed, baseKey);
            if (seedBytes.Length != 8)
            {
                throw new InvalidDataException("Reference seed must contain eight bytes.");
            }

            long seed = ReadInt64(seedBytes, 0);
            int payloadOffset = 4 + seedLength;
            byte[] encryptedPayload = Slice(
                envelope,
                payloadOffset,
                envelope.Length - payloadOffset);
            byte[] payload = Decrypt(
                encryptedPayload,
                seed.ToString(CultureInfo.InvariantCulture));

            return new ReferenceEnvelope
            {
                Seed = seed,
                Payload = payload
            };
        }

        public static int ReadInt32(byte[] bytes, int offset)
        {
            ValidateRange(bytes, offset, 4);
            unchecked
            {
                return (bytes[offset] << 24) |
                       (bytes[offset + 1] << 16) |
                       (bytes[offset + 2] << 8) |
                       bytes[offset + 3];
            }
        }

        public static string Sha256Hex(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var result = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }
        }

        private static long ReadInt64(byte[] bytes, int offset)
        {
            ValidateRange(bytes, offset, 8);
            unchecked
            {
                long value = 0;
                for (int i = 0; i < 8; i++)
                {
                    value = (value << 8) | bytes[offset + i];
                }

                return value;
            }
        }

        private static byte[] Decrypt(byte[] encrypted, string password)
        {
            if (encrypted == null || encrypted.Length < IvLength + 16)
            {
                throw new InvalidDataException("Reference ciphertext is too short.");
            }

            byte[] iv = Slice(encrypted, 0, IvLength);
            byte[] ciphertext = Slice(
                encrypted,
                IvLength,
                encrypted.Length - IvLength);
            byte[] key;
            using (SHA256 sha = SHA256.Create())
            {
                key = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            }

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(
                        ciphertext,
                        0,
                        ciphertext.Length);
                }
            }
        }

        private static byte[] Slice(byte[] source, int offset, int count)
        {
            ValidateRange(source, offset, count);
            var result = new byte[count];
            Buffer.BlockCopy(source, offset, result, 0, count);
            return result;
        }

        private static void ValidateRange(byte[] bytes, int offset, int count)
        {
            if (bytes == null ||
                offset < 0 ||
                count < 0 ||
                offset > bytes.Length - count)
            {
                throw new InvalidDataException("Reference buffer is truncated.");
            }
        }
    }
}
