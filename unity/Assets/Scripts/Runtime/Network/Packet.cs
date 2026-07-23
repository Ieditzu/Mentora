using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Mentora.Network
{
    public sealed class PacketFormatException : Exception
    {
        public PacketFormatException(string message) : base(message)
        {
        }

        public PacketFormatException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public abstract class Packet
    {
        public const string BaseKey = "CIOCLIKESKIDSIJIJSDJ1J2313J8123869699696";
        public const int MaximumEncryptedFrameBytes = 2 * 1024 * 1024;
        public const int MaximumRawFrameBytes = 1024 * 1024;
        public const int MaximumStringByteLength = 1024 * 1024;
        public const int MaximumEncryptedSeedBytes = 1024;

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly int id;

        protected Packet(int id)
        {
            this.id = id;
        }

        public int Id => id;

        protected abstract void Write(BinaryWriter writer);
        protected abstract void Read(BinaryReader reader);

        public byte[] Encode()
        {
            long dynamicSeed = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds * 1000000; // NanoTime simulation
            byte[] encryptedSeed = EncryptionUtility.EncryptLong(dynamicSeed, BaseKey);

            using (var payloadStream = new MemoryStream())
            using (var writer = new BinaryWriter(payloadStream))
            {
                // Write ID as BigEndian Int
                WriteInt32BigEndian(writer, id);
                Write(writer);
                writer.Flush();
                byte[] payloadBytes = payloadStream.ToArray();
                ValidateRawFrameLength(payloadBytes.Length);

                byte[] encryptedPayload = EncryptionUtility.EncryptBytes(
                    payloadBytes,
                    dynamicSeed.ToString(CultureInfo.InvariantCulture));

                using (var finalStream = new MemoryStream())
                using (var finalWriter = new BinaryWriter(finalStream))
                {
                    // Write seed length as BigEndian
                    byte[] seedLengthBytes = IntToBigEndian(encryptedSeed.Length);
                    finalWriter.Write(seedLengthBytes);
                    finalWriter.Write(encryptedSeed);
                    finalWriter.Write(encryptedPayload);
                    byte[] frame = finalStream.ToArray();
                    ValidateEncryptedFrameLength(frame.Length);
                    return frame;
                }
            }
        }

        // Plain (unencrypted) encode for peer-to-peer multiplayer frames.
        // Format: [4-byte big-endian packet ID][field bytes...]
        public byte[] EncodeRaw()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteInt32BigEndian(writer, id);
                Write(writer);
                writer.Flush();
                byte[] frame = ms.ToArray();
                ValidateRawFrameLength(frame.Length);
                return frame;
            }
        }

        // Decode a plain (unencrypted) multiplayer frame produced by EncodeRaw().
        public static Packet DecodeRaw(byte[] frame, PacketManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            ValidateRawFrame(frame);
            return DecodePayload(frame, manager, "raw packet");
        }

        public static Packet Decode(byte[] bytes, PacketManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            ValidateEncryptedFrame(bytes);

            try
            {
                int seedLength = BigEndianToInt(bytes, 0);
                int payloadOffset = checked(4 + seedLength);
                int payloadLength = bytes.Length - payloadOffset;

                byte[] encryptedSeed = new byte[seedLength];
                Buffer.BlockCopy(bytes, 4, encryptedSeed, 0, seedLength);
                long dynamicSeed = EncryptionUtility.DecryptLong(encryptedSeed, BaseKey);

                byte[] encryptedPayload = new byte[payloadLength];
                Buffer.BlockCopy(bytes, payloadOffset, encryptedPayload, 0, payloadLength);
                byte[] decryptedPayload = EncryptionUtility.DecryptBytes(
                    encryptedPayload,
                    dynamicSeed.ToString(CultureInfo.InvariantCulture));

                ValidateRawFrame(decryptedPayload);
                return DecodePayload(decryptedPayload, manager, "encrypted packet");
            }
            catch (PacketFormatException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new PacketFormatException("The encrypted packet is malformed.", exception);
            }
        }

        protected void PutString(BinaryWriter writer, string data)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            byte[] bytes = StrictUtf8.GetBytes(data ?? string.Empty);
            if (bytes.Length > MaximumStringByteLength)
            {
                throw new PacketFormatException(
                    "String payload exceeds the " + MaximumStringByteLength + "-byte protocol limit.");
            }

            WriteInt32BigEndian(writer, bytes.Length);
            writer.Write(bytes);
        }

        protected string ReadString(BinaryReader reader)
        {
            int length = ReadInt32BigEndian(reader);
            if (length < 0)
            {
                throw new PacketFormatException("String payload has a negative byte length.");
            }

            if (length > MaximumStringByteLength)
            {
                throw new PacketFormatException(
                    "String payload exceeds the " + MaximumStringByteLength + "-byte protocol limit.");
            }

            EnsureRemaining(reader, length, "string payload");
            byte[] bytes = reader.ReadBytes(length);
            try
            {
                return StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new PacketFormatException("String payload is not valid UTF-8.", exception);
            }
        }

        // Helper methods for BigEndian (since Java uses it)
        public static void WriteInt32BigEndian(BinaryWriter writer, int value)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            writer.Write(bytes);
        }

        public static int ReadInt32BigEndian(BinaryReader reader)
        {
            EnsureRemaining(reader, 4, "32-bit integer");
            byte[] bytes = reader.ReadBytes(4);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        public static byte[] IntToBigEndian(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes;
        }

        public static int BigEndianToInt(byte[] bytes, int offset)
        {
            if (bytes == null)
            {
                throw new PacketFormatException("Packet buffer is missing.");
            }

            if (offset < 0 || offset > bytes.Length - 4)
            {
                throw new PacketFormatException("Packet does not contain a complete 32-bit integer.");
            }

            byte[] data = new byte[4];
            Buffer.BlockCopy(bytes, offset, data, 0, 4);
            if (BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToInt32(data, 0);
        }

        private static Packet DecodePayload(byte[] payload, PacketManager manager, string description)
        {
            try
            {
                int packetId = BigEndianToInt(payload, 0);
                Packet packet = manager.CreatePacket(packetId);
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream, StrictUtf8, false))
                {
                    stream.Position = 4;
                    packet.Read(reader);
                }

                return packet;
            }
            catch (PacketFormatException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new PacketFormatException("Failed to decode " + description + ".", exception);
            }
        }

        private static void ValidateEncryptedFrame(byte[] frame)
        {
            if (frame == null)
            {
                throw new PacketFormatException("Encrypted packet buffer is missing.");
            }

            ValidateEncryptedFrameLength(frame.Length);
            if (frame.Length < 4 + EncryptionUtility.MinimumEncryptedBytes * 2)
            {
                throw new PacketFormatException("Encrypted packet is shorter than the protocol envelope.");
            }

            int seedLength = BigEndianToInt(frame, 0);
            if (seedLength < EncryptionUtility.MinimumEncryptedBytes ||
                seedLength > MaximumEncryptedSeedBytes ||
                !EncryptionUtility.IsValidEncryptedLength(seedLength))
            {
                throw new PacketFormatException("Encrypted seed length is invalid.");
            }

            int maximumSeedLength = frame.Length - 4 - EncryptionUtility.MinimumEncryptedBytes;
            if (seedLength > maximumSeedLength)
            {
                throw new PacketFormatException("Encrypted seed exceeds the available packet bytes.");
            }

            int encryptedPayloadLength = frame.Length - 4 - seedLength;
            if (!EncryptionUtility.IsValidEncryptedLength(encryptedPayloadLength))
            {
                throw new PacketFormatException("Encrypted payload length is invalid.");
            }
        }

        private static void ValidateRawFrame(byte[] frame)
        {
            if (frame == null)
            {
                throw new PacketFormatException("Raw packet buffer is missing.");
            }

            ValidateRawFrameLength(frame.Length);
            if (frame.Length < 4)
            {
                throw new PacketFormatException("Raw packet is missing its packet ID.");
            }
        }

        private static void ValidateEncryptedFrameLength(int length)
        {
            if (length > MaximumEncryptedFrameBytes)
            {
                throw new PacketFormatException(
                    "Encrypted packet exceeds the " + MaximumEncryptedFrameBytes + "-byte protocol limit.");
            }
        }

        private static void ValidateRawFrameLength(int length)
        {
            if (length > MaximumRawFrameBytes)
            {
                throw new PacketFormatException(
                    "Raw packet exceeds the " + MaximumRawFrameBytes + "-byte protocol limit.");
            }
        }

        private static void EnsureRemaining(BinaryReader reader, int requiredBytes, string description)
        {
            if (reader == null)
            {
                throw new PacketFormatException("Packet reader is missing.");
            }

            if (requiredBytes < 0)
            {
                throw new PacketFormatException(description + " has a negative byte length.");
            }

            Stream stream = reader.BaseStream;
            if (stream == null || !stream.CanSeek)
            {
                throw new PacketFormatException("Packet stream cannot validate the " + description + " length.");
            }

            long remaining = stream.Length - stream.Position;
            if (remaining < requiredBytes)
            {
                throw new PacketFormatException(
                    description + " is truncated: expected " + requiredBytes + " byte(s), found " + remaining + ".");
            }
        }
    }
}
