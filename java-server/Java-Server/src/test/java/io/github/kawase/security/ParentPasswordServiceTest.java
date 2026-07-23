package io.github.kawase.security;

import io.github.kawase.database.entity.Parent;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class ParentPasswordServiceTest {
    private final ParentPasswordService passwordService = new ParentPasswordService();

    @Test
    void newCredentialsAreStoredAsSaltedAdaptiveHashes() {
        final String storedCredential = passwordService.encode("client-sha256-credential");
        final Parent parent = parentWithPassword(storedCredential);

        assertNotEquals("client-sha256-credential", storedCredential);
        assertTrue(storedCredential.startsWith("{bcrypt}$2"));
        assertTrue(passwordService.matchesAndUpgrade(parent, "client-sha256-credential"));
        assertFalse(passwordService.matchesAndUpgrade(parent, "wrong-credential"));
    }

    @Test
    void successfulLegacyLoginUpgradesTheReplayableCredential() {
        final Parent parent = parentWithPassword("legacy-client-sha256");

        assertTrue(passwordService.matchesAndUpgrade(parent, "legacy-client-sha256"));
        assertTrue(parent.getPasswordHash().startsWith("{bcrypt}$2"));
        assertNotEquals("legacy-client-sha256", parent.getPasswordHash());
        assertTrue(passwordService.matchesAndUpgrade(parent, "legacy-client-sha256"));
    }

    @Test
    void failedLegacyLoginDoesNotRewriteTheStoredCredential() {
        final Parent parent = parentWithPassword("legacy-client-sha256");

        assertFalse(passwordService.matchesAndUpgrade(parent, "wrong-credential"));
        assertEquals("legacy-client-sha256", parent.getPasswordHash());
    }

    private Parent parentWithPassword(final String storedCredential) {
        final Parent parent = new Parent();
        parent.setPasswordHash(storedCredential);
        return parent;
    }
}
