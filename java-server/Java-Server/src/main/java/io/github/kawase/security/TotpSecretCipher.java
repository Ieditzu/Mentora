package io.github.kawase.security;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import javax.crypto.Cipher;
import javax.crypto.spec.GCMParameterSpec;
import javax.crypto.spec.SecretKeySpec;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.SecureRandom;
import java.util.Base64;

@Component
public class TotpSecretCipher {
    private static final byte[] ASSOCIATED_DATA = "mentora-parent-totp-v1".getBytes(StandardCharsets.UTF_8);

    private final byte[] encryptionKey;
    private final SecureRandom secureRandom;

    public TotpSecretCipher(@Value("${mentora.security.totp-encryption-key:}") final String encodedEncryptionKey) {
        this(encodedEncryptionKey, new SecureRandom());
    }

    TotpSecretCipher(final String encodedEncryptionKey, final SecureRandom secureRandom) {
        encryptionKey = decodeKey(encodedEncryptionKey);
        this.secureRandom = secureRandom;
    }

    public boolean isConfigured() {
        return encryptionKey.length == 32;
    }

    public String encrypt(final String secret) {
        requireConfigured();
        try {
            final byte[] nonce = new byte[12];
            secureRandom.nextBytes(nonce);
            final Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, new SecretKeySpec(encryptionKey, "AES"), new GCMParameterSpec(128, nonce));
            cipher.updateAAD(ASSOCIATED_DATA);
            final byte[] ciphertext = cipher.doFinal(secret.getBytes(StandardCharsets.UTF_8));
            return "v1:" + Base64.getEncoder().encodeToString(
                    ByteBuffer.allocate(nonce.length + ciphertext.length).put(nonce).put(ciphertext).array()
            );
        } catch (Exception exception) {
            throw new IllegalStateException("Could not encrypt TOTP secret", exception);
        }
    }

    public String decrypt(final String encryptedSecret) {
        requireConfigured();
        if (encryptedSecret == null || !encryptedSecret.startsWith("v1:"))
            throw new IllegalArgumentException("Unsupported encrypted TOTP secret");

        try {
            final byte[] stored = Base64.getDecoder().decode(encryptedSecret.substring(3));
            if (stored.length < 29)
                throw new IllegalArgumentException("Invalid encrypted TOTP secret");
            final byte[] nonce = new byte[12], ciphertext = new byte[stored.length - nonce.length];
            System.arraycopy(stored, 0, nonce, 0, nonce.length);
            System.arraycopy(stored, nonce.length, ciphertext, 0, ciphertext.length);
            final Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, new SecretKeySpec(encryptionKey, "AES"), new GCMParameterSpec(128, nonce));
            cipher.updateAAD(ASSOCIATED_DATA);
            return new String(cipher.doFinal(ciphertext), StandardCharsets.UTF_8);
        } catch (IllegalArgumentException exception) {
            throw exception;
        } catch (Exception exception) {
            throw new IllegalStateException("Could not decrypt TOTP secret", exception);
        }
    }

    private byte[] decodeKey(final String encodedEncryptionKey) {
        if (encodedEncryptionKey == null || encodedEncryptionKey.isBlank()) return new byte[0];

        try {
            final byte[] decoded = Base64.getDecoder().decode(encodedEncryptionKey.trim());
            if (decoded.length != 32)
                throw new IllegalArgumentException("TOTP encryption key must contain exactly 32 bytes");
            return decoded;
        } catch (IllegalArgumentException exception) {
            throw new IllegalArgumentException("MENTORA_TOTP_ENCRYPTION_KEY must be Base64-encoded AES-256 key", exception);
        }
    }

    private void requireConfigured() {
        if (!isConfigured())
            throw new IllegalStateException("TOTP enrollment is unavailable because no encryption key is configured");
    }
}
