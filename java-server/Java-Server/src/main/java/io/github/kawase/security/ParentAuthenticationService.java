package io.github.kawase.security;

import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.repository.ParentRepository;
import io.github.kawase.utility.HashUtility;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.time.Clock;
import java.time.Instant;
import java.util.ArrayList;
import java.util.Base64;
import java.util.List;
import java.util.Map;
import java.util.OptionalLong;
import java.util.concurrent.ConcurrentHashMap;

@Service
public class ParentAuthenticationService {
    private final ParentRepository parentRepository;
    private final ParentSessionService sessionService;
    private final TotpCodeService totpCodeService;
    private final TotpSecretCipher secretCipher;
    private final ParentPasswordService parentPasswordService;
    private final Clock clock;
    private final SecureRandom secureRandom;
    private final int challengeTtlSeconds, challengeAttemptLimit, enrollmentTtlSeconds;
    private final Map<String, LoginChallenge> loginChallenges = new ConcurrentHashMap<>();
    private final Map<Long, LoginFailureBudget> loginFailureBudgets = new ConcurrentHashMap<>();
    private final Map<String, EnrollmentChallenge> enrollmentChallenges = new ConcurrentHashMap<>();

    public ParentAuthenticationService(
            final ParentRepository parentRepository,
            final ParentSessionService sessionService,
            final TotpCodeService totpCodeService,
            final TotpSecretCipher secretCipher,
            final ParentPasswordService parentPasswordService,
            @Value("${mentora.security.login-challenge-ttl-seconds:300}") final int challengeTtlSeconds,
            @Value("${mentora.security.login-challenge-attempt-limit:5}") final int challengeAttemptLimit,
            @Value("${mentora.security.enrollment-ttl-seconds:600}") final int enrollmentTtlSeconds) {
        this(
                parentRepository,
                sessionService,
                totpCodeService,
                secretCipher,
                parentPasswordService,
                Clock.systemUTC(),
                new SecureRandom(),
                challengeTtlSeconds,
                challengeAttemptLimit,
                enrollmentTtlSeconds
        );
    }

    ParentAuthenticationService(
            final ParentRepository parentRepository,
            final ParentSessionService sessionService,
            final TotpCodeService totpCodeService,
            final TotpSecretCipher secretCipher,
            final ParentPasswordService parentPasswordService,
            final Clock clock,
            final SecureRandom secureRandom,
            final int challengeTtlSeconds,
            final int challengeAttemptLimit,
            final int enrollmentTtlSeconds) {
        this.parentRepository = parentRepository;
        this.sessionService = sessionService;
        this.totpCodeService = totpCodeService;
        this.secretCipher = secretCipher;
        this.parentPasswordService = parentPasswordService;
        this.clock = clock;
        this.secureRandom = secureRandom;
        this.challengeTtlSeconds = Math.max(30, challengeTtlSeconds);
        this.challengeAttemptLimit = Math.max(1, challengeAttemptLimit);
        this.enrollmentTtlSeconds = Math.max(60, enrollmentTtlSeconds);
    }

    @Transactional
    public synchronized LoginResult authenticatePassword(
            final String emailHash,
            final String passwordHash,
            final String binding,
            final String deviceId) {
        removeExpiredChallenges();
        final Parent parent = parentRepository.findByEmail(emailHash).orElse(null);
        if (parent == null || !passwordMatches(parent, passwordHash))
            return LoginResult.failure("Invalid credentials");

        if (!Boolean.TRUE.equals(parent.getTotpEnabled()))
            return LoginResult.authenticated(sessionService.issue(parent.getId(), deviceId));
        if (!secretCipher.isConfigured())
            return LoginResult.failure("Two-factor authentication is unavailable because server encryption is not configured");

        final LoginFailureBudget failureBudget = loginFailureBudgets.get(parent.getId());
        if (failureBudget != null && failureBudget.failures >= challengeAttemptLimit)
            return LoginResult.failure("Too many invalid two-factor attempts. Try again later");

        loginChallenges.entrySet().removeIf(entry -> entry.getValue().parentId.equals(parent.getId()));
        final String challengeId = generateOpaqueToken();
        loginChallenges.put(challengeId, new LoginChallenge(
                parent.getId(),
                normalizeBinding(binding),
                clock.instant().plusSeconds(challengeTtlSeconds)
        ));
        return LoginResult.secondFactorRequired(challengeId, challengeTtlSeconds);
    }

    @Transactional
    public synchronized LoginResult verifySecondFactor(
            final String challengeId,
            final String code,
            final String binding,
            final String deviceId) {
        final LoginChallenge challenge = loginChallenges.get(challengeId);
        if (challenge == null)
            return LoginResult.failure("Invalid or expired two-factor challenge");
        if (!challenge.binding.equals(normalizeBinding(binding)))
            return LoginResult.failure("Two-factor challenge belongs to a different connection");
        if (!challenge.expiresAt.isAfter(clock.instant())) {
            loginChallenges.remove(challengeId);
            return LoginResult.failure("Two-factor challenge expired");
        }

        final Parent parent = parentRepository.findByIdForUpdate(challenge.parentId).orElse(null);
        if (parent != null && verifyAndConsumeFactor(parent, code)) {
            clearLoginAttemptState(challenge.parentId);
            parentRepository.save(parent);
            return LoginResult.authenticated(sessionService.issue(parent.getId(), deviceId));
        }

        final int attemptsRemaining = recordLoginFailure(challenge.parentId);
        if (attemptsRemaining <= 0)
            loginChallenges.entrySet().removeIf(entry -> entry.getValue().parentId.equals(challenge.parentId));
        return LoginResult.failure(attemptsRemaining <= 0
                ? "Too many invalid two-factor attempts. Try again later"
                : "Invalid two-factor code");
    }

    @Transactional
    public ParentSessionService.SessionToken issueSession(final Long parentId, final String deviceId) {
        return sessionService.issue(parentId, deviceId);
    }

    @Transactional
    public synchronized EnrollmentDetails beginEnrollment(final Long parentId, final String passwordHash) {
        removeExpiredChallenges();
        final Parent parent = parentRepository.findByIdForUpdate(parentId)
                .orElseThrow(() -> new RuntimeException("Parent not found"));
        if (!passwordMatches(parent, passwordHash))
            throw new RuntimeException("Invalid credentials");
        if (!secretCipher.isConfigured())
            throw new RuntimeException("TOTP enrollment is unavailable because no encryption key is configured");

        final String enrollmentId = generateOpaqueToken(), secret = totpCodeService.generateSecret();
        enrollmentChallenges.put(enrollmentId, new EnrollmentChallenge(
                parentId,
                secret,
                clock.instant().plusSeconds(enrollmentTtlSeconds),
                challengeAttemptLimit
        ));
        final String label = "Mentora:parent-" + parentId;
        final String otpAuthUri = "otpauth://totp/" + urlEncode(label)
                + "?secret=" + secret
                + "&issuer=Mentora&algorithm=SHA1&digits=6&period=30";
        return new EnrollmentDetails(enrollmentId, secret, otpAuthUri);
    }

    @Transactional
    public synchronized EnrollmentResult confirmEnrollment(
            final Long parentId,
            final String enrollmentId,
            final String code) {
        final EnrollmentChallenge challenge = enrollmentChallenges.get(enrollmentId);
        if (challenge == null || !challenge.parentId.equals(parentId))
            return EnrollmentResult.failure("Invalid or expired TOTP enrollment");
        if (!challenge.expiresAt.isAfter(clock.instant())) {
            enrollmentChallenges.remove(enrollmentId);
            return EnrollmentResult.failure("TOTP enrollment expired");
        }

        final OptionalLong matchingStep = totpCodeService.findMatchingStep(challenge.secret, code);
        if (matchingStep.isEmpty()) {
            challenge.attemptsRemaining--;
            if (challenge.attemptsRemaining <= 0)
                enrollmentChallenges.remove(enrollmentId);
            return EnrollmentResult.failure(challenge.attemptsRemaining <= 0
                    ? "TOTP enrollment invalidated after too many attempts"
                    : "Invalid authenticator code");
        }

        final Parent parent = parentRepository.findByIdForUpdate(parentId)
                .orElseThrow(() -> new RuntimeException("Parent not found"));
        final List<String> recoveryCodes = generateRecoveryCodes();
        parent.setTotpSecretEncrypted(secretCipher.encrypt(challenge.secret));
        parent.setTotpEnabled(true);
        parent.setTotpLastAcceptedStep(matchingStep.getAsLong());
        parent.setTotpRecoveryCodeHashes(recoveryCodes.stream()
                .map(this::hashRecoveryCode)
                .reduce((first, second) -> first + "\n" + second)
                .orElse(""));
        parentRepository.save(parent);
        enrollmentChallenges.remove(enrollmentId);
        sessionService.revokeAll(parentId);
        return EnrollmentResult.success(recoveryCodes);
    }

    @Transactional(readOnly = true)
    public SecurityStatus getSecurityStatus(final Long parentId) {
        final Parent parent = parentRepository.findById(parentId)
                .orElseThrow(() -> new RuntimeException("Parent not found"));
        return new SecurityStatus(Boolean.TRUE.equals(parent.getTotpEnabled()), recoveryCodeHashes(parent).size());
    }

    @Transactional
    public synchronized void disableTotp(final Long parentId, final String passwordHash, final String code) {
        final Parent parent = parentRepository.findByIdForUpdate(parentId)
                .orElseThrow(() -> new RuntimeException("Parent not found"));
        if (!passwordMatches(parent, passwordHash))
            throw new RuntimeException("Invalid credentials");
        if (!Boolean.TRUE.equals(parent.getTotpEnabled()))
            throw new RuntimeException("Two-factor authentication is not enabled");
        if (!verifyAndConsumeFactor(parent, code))
            throw new RuntimeException("Invalid two-factor code");

        parent.setTotpEnabled(false);
        parent.setTotpSecretEncrypted(null);
        parent.setTotpLastAcceptedStep(null);
        parent.setTotpRecoveryCodeHashes(null);
        parentRepository.save(parent);
        sessionService.revokeAll(parentId);
    }

    private boolean verifyAndConsumeFactor(final Parent parent, final String code) {
        if (code == null || code.isBlank()) return false;

        if (code.matches("\\d{6}")) {
            if (!Boolean.TRUE.equals(parent.getTotpEnabled()) || parent.getTotpSecretEncrypted() == null)
                return false;
            final OptionalLong matchingStep = totpCodeService.findMatchingStep(
                    secretCipher.decrypt(parent.getTotpSecretEncrypted()),
                    code
            );
            if (matchingStep.isEmpty()
                    || parent.getTotpLastAcceptedStep() != null
                    && matchingStep.getAsLong() <= parent.getTotpLastAcceptedStep())
                return false;
            parent.setTotpLastAcceptedStep(matchingStep.getAsLong());
            return true;
        }

        final String suppliedHash = hashRecoveryCode(code);
        final List<String> storedHashes = recoveryCodeHashes(parent);
        for (int index = 0; index < storedHashes.size(); index++) {
            if (!MessageDigest.isEqual(
                    storedHashes.get(index).getBytes(StandardCharsets.US_ASCII),
                    suppliedHash.getBytes(StandardCharsets.US_ASCII)
            ))
                continue;

            storedHashes.remove(index);
            parent.setTotpRecoveryCodeHashes(String.join("\n", storedHashes));
            return true;
        }
        return false;
    }

    private boolean passwordMatches(final Parent parent, final String passwordHash) {
        return parentPasswordService.matchesAndUpgrade(parent, passwordHash);
    }

    private List<String> recoveryCodeHashes(final Parent parent) {
        if (parent.getTotpRecoveryCodeHashes() == null || parent.getTotpRecoveryCodeHashes().isBlank())
            return new ArrayList<>();
        return new ArrayList<>(List.of(parent.getTotpRecoveryCodeHashes().split("\\n")));
    }

    private List<String> generateRecoveryCodes() {
        final List<String> recoveryCodes = new ArrayList<>();
        for (int index = 0; index < 10; index++) {
            final byte[] bytes = new byte[10];
            secureRandom.nextBytes(bytes);
            final String encoded = Base32Codec.encode(bytes);
            recoveryCodes.add(encoded.substring(0, 4) + "-" + encoded.substring(4, 8)
                    + "-" + encoded.substring(8, 12) + "-" + encoded.substring(12, 16));
        }
        return recoveryCodes;
    }

    private String hashRecoveryCode(final String recoveryCode) {
        return HashUtility.hash(recoveryCode == null ? "" : recoveryCode
                .replace("-", "")
                .replaceAll("\\s+", "")
                .toUpperCase());
    }

    private String generateOpaqueToken() {
        final byte[] bytes = new byte[32];
        secureRandom.nextBytes(bytes);
        return Base64.getUrlEncoder().withoutPadding().encodeToString(bytes);
    }

    private String normalizeBinding(final String binding) {
        return binding == null ? "" : binding.trim();
    }

    private String urlEncode(final String value) {
        return URLEncoder.encode(value, StandardCharsets.UTF_8).replace("+", "%20");
    }

    private void removeExpiredChallenges() {
        final Instant now = clock.instant();
        loginChallenges.entrySet().removeIf(entry -> !entry.getValue().expiresAt.isAfter(now));
        loginFailureBudgets.entrySet().removeIf(entry -> !entry.getValue().expiresAt.isAfter(now));
        enrollmentChallenges.entrySet().removeIf(entry -> !entry.getValue().expiresAt.isAfter(now));
    }

    private int recordLoginFailure(final Long parentId) {
        final Instant now = clock.instant();
        LoginFailureBudget budget = loginFailureBudgets.get(parentId);
        if (budget == null || !budget.expiresAt.isAfter(now)) {
            budget = new LoginFailureBudget();
            loginFailureBudgets.put(parentId, budget);
        }
        budget.failures++;
        budget.expiresAt = now.plusSeconds(challengeTtlSeconds);
        return Math.max(0, challengeAttemptLimit - budget.failures);
    }

    private void clearLoginAttemptState(final Long parentId) {
        loginFailureBudgets.remove(parentId);
        loginChallenges.entrySet().removeIf(entry -> entry.getValue().parentId.equals(parentId));
    }

    private static final class LoginChallenge {
        private final Long parentId;
        private final String binding;
        private final Instant expiresAt;

        private LoginChallenge(
                final Long parentId,
                final String binding,
                final Instant expiresAt) {
            this.parentId = parentId;
            this.binding = binding;
            this.expiresAt = expiresAt;
        }
    }

    private static final class LoginFailureBudget {
        private Instant expiresAt = Instant.EPOCH;
        private int failures;
    }

    private static final class EnrollmentChallenge {
        private final Long parentId;
        private final String secret;
        private final Instant expiresAt;
        private int attemptsRemaining;

        private EnrollmentChallenge(
                final Long parentId,
                final String secret,
                final Instant expiresAt,
                final int attemptsRemaining) {
            this.parentId = parentId;
            this.secret = secret;
            this.expiresAt = expiresAt;
            this.attemptsRemaining = attemptsRemaining;
        }
    }

    public record LoginResult(
            boolean success,
            boolean secondFactorRequired,
            String message,
            String challengeId,
            int expiresInSeconds,
            ParentSessionService.SessionToken session) {
        private static LoginResult failure(final String message) {
            return new LoginResult(false, false, message, "", 0, null);
        }

        private static LoginResult secondFactorRequired(final String challengeId, final int expiresInSeconds) {
            return new LoginResult(false, true, "Two-factor authentication required", challengeId, expiresInSeconds, null);
        }

        private static LoginResult authenticated(final ParentSessionService.SessionToken session) {
            return new LoginResult(true, false, "Login successful", "", 0, session);
        }
    }

    public record EnrollmentDetails(String enrollmentId, String secretBase32, String otpAuthUri) {
        /* w */
    }

    public record EnrollmentResult(boolean success, String message, List<String> recoveryCodes) {
        private static EnrollmentResult failure(final String message) {
            return new EnrollmentResult(false, message, List.of());
        }

        private static EnrollmentResult success(final List<String> recoveryCodes) {
            return new EnrollmentResult(true, "Two-factor authentication enabled", List.copyOf(recoveryCodes));
        }
    }

    public record SecurityStatus(boolean totpEnabled, int recoveryCodesRemaining) {
        /* w */
    }
}
