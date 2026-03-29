using System;
using System.IO;
using System.Text;

namespace Mentora.Network
{
    public abstract class Packet
    {
        public const string BaseKey = "CIOCLIKESKIDSIJIJSDJ1J2313J8123869699696";
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

                byte[] encryptedPayload = EncryptionUtility.EncryptBytes(payloadBytes, dynamicSeed.ToString());

                using (var finalStream = new MemoryStream())
                using (var finalWriter = new BinaryWriter(finalStream))
                {
                    // Write seed length as BigEndian
                    byte[] seedLengthBytes = IntToBigEndian(encryptedSeed.Length);
                    finalWriter.Write(seedLengthBytes);
                    finalWriter.Write(encryptedSeed);
                    finalWriter.Write(encryptedPayload);
                    return finalStream.ToArray();
                }
            }
        }

        public static Packet Decode(byte[] bytes, PacketManager manager)
        {
            int seedLength = BigEndianToInt(bytes, 0);
            byte[] encryptedSeed = new byte[seedLength];
            Buffer.BlockCopy(bytes, 4, encryptedSeed, 0, seedLength);

            long dynamicSeed = EncryptionUtility.DecryptLong(encryptedSeed, BaseKey);

            int payloadOffset = 4 + seedLength;
            int payloadLength = bytes.Length - payloadOffset;
            byte[] encryptedPayload = new byte[payloadLength];
            Buffer.BlockCopy(bytes, payloadOffset, encryptedPayload, 0, payloadLength);

            byte[] decryptedPayload = EncryptionUtility.DecryptBytes(encryptedPayload, dynamicSeed.ToString());

            using (var stream = new MemoryStream(decryptedPayload))
            using (var reader = new BinaryReader(stream))
            {
                int packetId = BigEndianToInt(decryptedPayload, 0);
                stream.Position = 4;
                
                Packet packet = manager.CreatePacket(packetId);
                packet.Read(reader);
                return packet;
            }
        }

        protected void PutString(BinaryWriter writer, string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            WriteInt32BigEndian(writer, bytes.Length);
            writer.Write(bytes);
        }

        protected string ReadString(BinaryReader reader)
        {
            int length = ReadInt32BigEndian(reader);
            byte[] bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        // Helper methods for BigEndian (since Java uses it)
        public static void WriteInt32BigEndian(BinaryWriter writer, int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            writer.Write(bytes);
        }

        public static int ReadInt32BigEndian(BinaryReader reader)
        {
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
            byte[] data = new byte[4];
            Buffer.BlockCopy(bytes, offset, data, 0, 4);
            if (BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToInt32(data, 0);
        }
    }
}
