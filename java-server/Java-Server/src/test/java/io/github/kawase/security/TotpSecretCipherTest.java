package io.github.kawase.security;

import org.junit.jupiter.api.Test;

import java.security.SecureRandom;
import java.util.Base64;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;

class TotpSecretCipherTest {
    private static final String TEST_KEY = Base64.getEncoder().encodeToString(new byte[32]);

    @Test
    void encryptsSecretWithAuthenticatedEncryption() {
        final TotpSecretCipher cipher = new TotpSecretCipher(TEST_KEY, new SecureRandom());

        final String encrypted = cipher.encrypt("JBSWY3DPEHPK3PXP");

        assertFalse(encrypted.contains("JBSWY3DPEHPK3PXP"));
        assertEquals("JBSWY3DPEHPK3PXP", cipher.decrypt(encrypted));
        assertThrows(IllegalStateException.class, () -> cipher.decrypt(
                encrypted.substring(0, encrypted.length() - 2) + "AA"
        ));
    }

    @Test
    void enrollmentCannotPersistPlaintextWithoutConfiguredKey() {
        final TotpSecretCipher cipher = new TotpSecretCipher("", new SecureRandom());

        assertFalse(cipher.isConfigured());
        assertThrows(IllegalStateException.class, () -> cipher.encrypt("secret"));
    }
}
