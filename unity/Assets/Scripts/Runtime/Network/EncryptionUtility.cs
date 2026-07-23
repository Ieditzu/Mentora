using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Mentora.Network
{
    public static class EncryptionUtility
    {
        public const int IvLengthBytes = 16;
        public const int MinimumEncryptedBytes = IvLengthBytes + 16;

        private static byte[] DeriveKey(string password)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            }
        }

        public static byte[] EncryptLong(long value, string key)
        {
            byte[] bytes = new byte[8];
            for (int i = 7; i >= 0; i--)
            {
                bytes[i] = (byte)(value & 0xFF);
                value >>= 8;
            }
            return EncryptBytes(bytes, key);
        }

        public static long DecryptLong(byte[] encryptedData, string encryptionKey)
        {
            byte[] decryptedBytes = DecryptBytes(encryptedData, encryptionKey);
            if (decryptedBytes.Length != 8)
                throw new CryptographicException("Decrypted seed must contain exactly eight bytes.");

            long value = 0;
            for (int i = 0; i < 8; i++)
            {
                value = (value << 8) | ((long)decryptedBytes[i] & 0xFFL);
            }
            return value;
        }

        public static byte[] EncryptBytes(byte[] data, string encryptionKey)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            byte[] key = DeriveKey(encryptionKey);
            byte[] iv = new byte[IvLengthBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    ms.Write(iv, 0, iv.Length);
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                        cs.FlushFinalBlock();
                    }
                    return ms.ToArray();
                }
            }
        }

        public static byte[] DecryptBytes(byte[] encryptedData, string encryptionKey)
        {
            if (encryptedData == null)
            {
                throw new PacketFormatException("Encrypted data buffer is missing.");
            }

            if (!IsValidEncryptedLength(encryptedData.Length))
            {
                throw new PacketFormatException(
                    "Encrypted data must contain a 16-byte IV and complete AES blocks.");
            }

            byte[] key = DeriveKey(encryptionKey);
            byte[] iv = new byte[IvLengthBytes];
            Buffer.BlockCopy(encryptedData, 0, iv, 0, IvLengthBytes);

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(encryptedData, IvLengthBytes, encryptedData.Length - IvLengthBytes);
                        cs.FlushFinalBlock();
                    }
                    return ms.ToArray();
                }
            }
        }

        public static bool IsValidEncryptedLength(int length)
        {
            return length >= MinimumEncryptedBytes &&
                   (length - IvLengthBytes) % 16 == 0;
        }
    }
}
