package io.github.kawase.security;

import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;
import java.security.SecureRandom;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class TotpCodeServiceTest {
    private static final String RFC_SECRET = Base32Codec.encode("12345678901234567890".getBytes(StandardCharsets.US_ASCII));

    @Test
    void matchesRfc6238Sha1Vector() {
        final TotpCodeService service = new TotpCodeService(
                Clock.fixed(Instant.ofEpochSecond(59), ZoneOffset.UTC),
                new SecureRandom()
        );

        assertEquals("94287082", service.generateCode(RFC_SECRET, 1L, 8));
        assertEquals("287082", service.generateCurrentCode(RFC_SECRET));
    }

    @Test
    void acceptsOnlyOneAdjacentTimeStep() {
        final TotpCodeService service = new TotpCodeService(
                Clock.fixed(Instant.ofEpochSecond(59), ZoneOffset.UTC),
                new SecureRandom()
        );

        assertTrue(service.findMatchingStep(RFC_SECRET, service.generateCode(RFC_SECRET, 0L, 6)).isPresent());
        assertTrue(service.findMatchingStep(RFC_SECRET, service.generateCode(RFC_SECRET, 1L, 6)).isPresent());
        assertTrue(service.findMatchingStep(RFC_SECRET, service.generateCode(RFC_SECRET, 2L, 6)).isPresent());
        assertFalse(service.findMatchingStep(RFC_SECRET, service.generateCode(RFC_SECRET, 3L, 6)).isPresent());
        assertFalse(service.findMatchingStep(RFC_SECRET, "12345").isPresent());
    }
}
