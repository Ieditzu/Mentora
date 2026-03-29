package io.github.kawase.utility;

import javax.crypto.Cipher;
import javax.crypto.SecretKey;
import javax.crypto.spec.IvParameterSpec;
import javax.crypto.spec.SecretKeySpec;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;

public class EncryptionUtility {
    private static final String ALGORITHM = "AES/CBC/PKCS5Padding";
    private static final int IV_LENGTH_BYTE = 16;

    private static SecretKey deriveKey(final String password) throws Exception {
        final MessageDigest sha = MessageDigest.getInstance("SHA-256");
        byte[] key = sha.digest(password.getBytes(StandardCharsets.UTF_8));
        return new SecretKeySpec(key, "AES");
    }

    public static byte[] encryptLong(long value, final String key) throws Exception {
        byte[] bytes = new byte[Long.BYTES];
        for (int i = 7; i >= 0; i--) {
            bytes[i] = (byte) (value & 0xFF);
            value >>= 8;
        }

        return encryptBytes(bytes, key);
    }

    public static long decryptLong(final byte[] encryptedData, final String encryptionKey) throws Exception {
        byte[] decryptedBytes = decryptBytes(encryptedData, encryptionKey);

        if (decryptedBytes.length != Long.BYTES) {
            throw new IllegalArgumentException("Decrypted data length mismatch.");
        }

        long value = 0;
        for (int i = 0; i < Long.BYTES; i++) {
            value = (value << 8) | (decryptedBytes[i] & 0xFF);
        }

        return value;
    }

    public static byte[] encryptBytes(final byte[] data, final String encryptionKey) throws Exception {
        final byte[] iv = new byte[IV_LENGTH_BYTE];
        new SecureRandom().nextBytes(iv);

        final SecretKey key = deriveKey(encryptionKey);
        final Cipher cipher = Cipher.getInstance(ALGORITHM);
        cipher.init(Cipher.ENCRYPT_MODE, key, new IvParameterSpec(iv));

        final byte[] cipherText = cipher.doFinal(data);

        return ByteBuffer.allocate(iv.length + cipherText.length)
                .put(iv)
                .put(cipherText)
                .array();
    }

    public static byte[] decryptBytes(final byte[] encryptedData, final String encryptionKey) throws Exception {
        final byte[] iv = new byte[IV_LENGTH_BYTE];
        System.arraycopy(encryptedData, 0, iv, 0, iv.length);

        final byte[] cipherText = new byte[encryptedData.length - IV_LENGTH_BYTE];
        System.arraycopy(encryptedData, IV_LENGTH_BYTE, cipherText, 0, cipherText.length);

        final SecretKey key = deriveKey(encryptionKey);
        final Cipher cipher = Cipher.getInstance(ALGORITHM);
        cipher.init(Cipher.DECRYPT_MODE, key, new IvParameterSpec(iv));

        return cipher.doFinal(cipherText);
    }
}
