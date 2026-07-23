package io.github.kawase.security;

import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.repository.ParentRepository;
import org.junit.jupiter.api.Test;

import java.security.SecureRandom;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneId;
import java.time.ZoneOffset;
import java.util.Base64;
import java.util.Optional;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class ParentAuthenticationServiceTest {
    private static final String TEST_KEY = Base64.getEncoder().encodeToString(new byte[32]);

    @Test
    void passwordOnlyLoginAlwaysIssuesAPersistedSession() {
        final MutableClock clock = new MutableClock(Instant.ofEpochSecond(59));
        final ParentRepository parentRepository = mock(ParentRepository.class);
        final ParentSessionService sessionService = mock(ParentSessionService.class);
        final Parent parent = new Parent();
        parent.setId(9L);
        parent.setPasswordHash("password-hash");
        when(parentRepository.findByEmail("email-hash")).thenReturn(Optional.of(parent));
        when(sessionService.issue(9L, null)).thenReturn(
                new ParentSessionService.SessionToken(9L, "", "persisted-session", 10_000L)
        );
        final ParentAuthenticationService service = service(
                parentRepository,
                sessionService,
                new TotpCodeService(clock, new SecureRandom()),
                new TotpSecretCipher(TEST_KEY, new SecureRandom()),
                clock
        );

        final var result = service.authenticatePassword(
                "email-hash",
                "password-hash",
                "legacy-socket",
                null
        );

        assertTrue(result.success());
        assertEquals("persisted-session", result.session().rawToken());
        verify(sessionService).issue(9L, null);
    }

    @Test
    void passwordDoesNotAuthenticateUntilSecondFactorAndCodeCannotReplay() {
        final MutableClock clock = new MutableClock(Instant.ofEpochSecond(59));
        final ParentRepository parentRepository = mock(ParentRepository.class);
        final ParentSessionService sessionService = mock(ParentSessionService.class);
        final TotpCodeService totpCodeService = new TotpCodeService(clock, new SecureRandom());
        final TotpSecretCipher cipher = new TotpSecretCipher(TEST_KEY, new SecureRandom());
        final Parent parent = totpParent(cipher);
        when(parentRepository.findByEmail("email-hash")).thenReturn(Optional.of(parent));
        when(parentRepository.findByIdForUpdate(9L)).thenReturn(Optional.of(parent));
        when(sessionService.issue(9L, "phone-1")).thenReturn(
                new ParentSessionService.SessionToken(9L, "", "session", 10_000L)
        );
        final ParentAuthenticationService service = service(
                parentRepository,
                sessionService,
                totpCodeService,
                cipher,
                clock
        );

        final var passwordResult = service.authenticatePassword(
                "email-hash",
                "password-hash",
                "socket-1",
                "phone-1"
        );

        assertFalse(passwordResult.success());
        assertTrue(passwordResult.secondFactorRequired());

        final String code = totpCodeService.generateCurrentCode("JBSWY3DPEHPK3PXP");
        final var verified = service.verifySecondFactor(
                passwordResult.challengeId(),
                code,
                "socket-1",
                "phone-1"
        );

        assertTrue(verified.success());
        assertEquals("session", verified.session().rawToken());

        final var replayChallenge = service.authenticatePassword(
                "email-hash",
                "password-hash",
                "socket-1",
                "phone-1"
        );
        assertFalse(service.verifySecondFactor(
                replayChallenge.challengeId(),
                code,
                "socket-1",
                "phone-1"
        ).success());
    }

    @Test
    void challengeExpiresAndAttemptLimitInvalidatesIt() {
        final MutableClock clock = new MutableClock(Instant.ofEpochSecond(59));
        final ParentRepository parentRepository = mock(ParentRepository.class);
        final ParentSessionService sessionService = mock(ParentSessionService.class);
        final TotpCodeService totpCodeService = new TotpCodeService(clock, new SecureRandom());
        final TotpSecretCipher cipher = new TotpSecretCipher(TEST_KEY, new SecureRandom());
        final Parent parent = totpParent(cipher);
        when(parentRepository.findByEmail("email-hash")).thenReturn(Optional.of(parent));
        when(parentRepository.findByIdForUpdate(9L)).thenReturn(Optional.of(parent));
        final ParentAuthenticationService service = service(
                parentRepository,
                sessionService,
                totpCodeService,
                cipher,
                clock
        );

        final var expiring = service.authenticatePassword("email-hash", "password-hash", "socket", "phone");
        clock.advanceSeconds(301);
        assertFalse(service.verifySecondFactor(expiring.challengeId(), "000000", "socket", "phone").success());

        final var limited = service.authenticatePassword("email-hash", "password-hash", "socket", "phone");
        for (int attempt = 0; attempt < 5; attempt++)
            assertFalse(service.verifySecondFactor(limited.challengeId(), "000000", "socket", "phone").success());
        assertFalse(service.verifySecondFactor(
                limited.challengeId(),
                totpCodeService.generateCurrentCode("JBSWY3DPEHPK3PXP"),
                "socket",
                "phone"
        ).success());
    }

    @Test
    void requestingFreshChallengesCannotResetTheAccountFailureBudget() {
        final MutableClock clock = new MutableClock(Instant.ofEpochSecond(59));
        final ParentRepository parentRepository = mock(ParentRepository.class);
        final ParentSessionService sessionService = mock(ParentSessionService.class);
        final TotpCodeService totpCodeService = new TotpCodeService(clock, new SecureRandom());
        final TotpSecretCipher cipher = new TotpSecretCipher(TEST_KEY, new SecureRandom());
        final Parent parent = totpParent(cipher);
        when(parentRepository.findByEmail("email-hash")).thenReturn(Optional.of(parent));
        when(parentRepository.findByIdForUpdate(9L)).thenReturn(Optional.of(parent));
        final ParentAuthenticationService service = service(
                parentRepository,
                sessionService,
                totpCodeService,
                cipher,
                clock
        );

        for (int attempt = 0; attempt < 5; attempt++) {
            final String binding = "socket-" + attempt;
            final var challenge = service.authenticatePassword(
                    "email-hash",
                    "password-hash",
                    binding,
                    "phone"
            );
            assertTrue(challenge.secondFactorRequired());
            assertFalse(service.verifySecondFactor(
                    challenge.challengeId(),
                    "000000",
                    binding,
                    "phone"
            ).success());
        }

        final var blocked = service.authenticatePassword(
                "email-hash",
                "password-hash",
                "another-socket",
                "phone"
        );
        assertFalse(blocked.success());
        assertFalse(blocked.secondFactorRequired());
        assertTrue(blocked.message().contains("Too many"));

        clock.advanceSeconds(301);
        assertTrue(service.authenticatePassword(
                "email-hash",
                "password-hash",
                "another-socket",
                "phone"
        ).secondFactorRequired());
    }

    @Test
    void enrollmentEncryptsSecretAndCreatesOneUseRecoveryCodes() {
        final MutableClock clock = new MutableClock(Instant.ofEpochSecond(1_000));
        final ParentRepository parentRepository = mock(ParentRepository.class);
        final ParentSessionService sessionService = mock(ParentSessionService.class);
        final TotpCodeService totpCodeService = new TotpCodeService(clock, new SecureRandom());
        final TotpSecretCipher cipher = new TotpSecretCipher(TEST_KEY, new SecureRandom());
        final Parent parent = new Parent();
        parent.setId(9L);
        parent.setPasswordHash("password-hash");
        when(parentRepository.findByIdForUpdate(9L)).thenReturn(Optional.of(parent));
        final ParentAuthenticationService service = service(
                parentRepository,
                sessionService,
                totpCodeService,
                cipher,
                clock
        );

        final var enrollment = service.beginEnrollment(9L, "password-hash");
        final var confirmed = service.confirmEnrollment(
                9L,
                enrollment.enrollmentId(),
                totpCodeService.generateCurrentCode(enrollment.secretBase32())
        );

        assertTrue(confirmed.success());
        assertEquals(10, confirmed.recoveryCodes().size());
        assertTrue(parent.getTotpEnabled());
        assertNotEquals(enrollment.secretBase32(), parent.getTotpSecretEncrypted());
        assertEquals(enrollment.secretBase32(), cipher.decrypt(parent.getTotpSecretEncrypted()));
        assertFalse(parent.getTotpRecoveryCodeHashes().contains(confirmed.recoveryCodes().getFirst()));
        verify(sessionService).revokeAll(9L);
        verify(parentRepository).save(any(Parent.class));
    }

    private ParentAuthenticationService service(
            final ParentRepository parentRepository,
            final ParentSessionService sessionService,
            final TotpCodeService totpCodeService,
            final TotpSecretCipher cipher,
            final Clock clock) {
        return new ParentAuthenticationService(
                parentRepository,
                sessionService,
                totpCodeService,
                cipher,
                new ParentPasswordService(),
                clock,
                new SecureRandom(),
                300,
                5,
                600
        );
    }

    private Parent totpParent(final TotpSecretCipher cipher) {
        final Parent parent = new Parent();
        parent.setId(9L);
        parent.setEmail("email-hash");
        parent.setPasswordHash("password-hash");
        parent.setTotpEnabled(true);
        parent.setTotpSecretEncrypted(cipher.encrypt("JBSWY3DPEHPK3PXP"));
        return parent;
    }

    private static final class MutableClock extends Clock {
        private Instant instant;

        private MutableClock(final Instant instant) {
            this.instant = instant;
        }

        @Override
        public ZoneId getZone() {
            return ZoneOffset.UTC;
        }

        @Override
        public Clock withZone(final ZoneId zone) {
            return this;
        }

        @Override
        public Instant instant() {
            return instant;
        }

        private void advanceSeconds(final long seconds) {
            instant = instant.plusSeconds(seconds);
        }
    }
}
