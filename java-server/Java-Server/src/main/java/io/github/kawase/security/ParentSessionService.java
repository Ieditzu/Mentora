package io.github.kawase.security;

import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.entity.ParentSession;
import io.github.kawase.database.repository.ParentRepository;
import io.github.kawase.database.repository.ParentSessionRepository;
import io.github.kawase.utility.HashUtility;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.security.SecureRandom;
import java.time.Clock;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.Base64;
import java.util.List;
import java.util.Optional;

@Service
public class ParentSessionService {
    private final ParentSessionRepository sessionRepository;
    private final ParentRepository parentRepository;
    private final Clock clock;
    private final SecureRandom secureRandom;
    private final int ttlDays;

    public ParentSessionService(
            final ParentSessionRepository sessionRepository,
            final ParentRepository parentRepository,
            @Value("${mentora.security.parent-session-ttl-days:30}") final int ttlDays) {
        this(sessionRepository, parentRepository, Clock.systemUTC(), new SecureRandom(), ttlDays);
    }

    ParentSessionService(
            final ParentSessionRepository sessionRepository,
            final ParentRepository parentRepository,
            final Clock clock,
            final SecureRandom secureRandom,
            final int ttlDays) {
        this.sessionRepository = sessionRepository;
        this.parentRepository = parentRepository;
        this.clock = clock;
        this.secureRandom = secureRandom;
        this.ttlDays = Math.max(1, ttlDays);
    }

    @Transactional
    public SessionToken issue(final Long parentId, final String deviceId) {
        final Parent parent = parentRepository.findById(parentId)
                .orElseThrow(() -> new RuntimeException("Parent not found"));
        final Instant now = clock.instant();
        final String rawToken = generateToken();
        final ParentSession session = new ParentSession();
        session.setParent(parent);
        session.setTokenHash(HashUtility.hash(rawToken));
        session.setDeviceIdHash(hashDeviceId(deviceId));
        session.setCreatedAt(now);
        session.setLastUsedAt(now);
        session.setExpiresAt(now.plus(ttlDays, ChronoUnit.DAYS));
        sessionRepository.save(session);
        return toToken(parent, rawToken, session.getExpiresAt());
    }

    @Transactional
    public synchronized Optional<SessionToken> resume(final String rawToken, final String deviceId) {
        if (rawToken == null || rawToken.isBlank()) return Optional.empty();

        final Optional<ParentSession> storedSession = sessionRepository.findActiveByTokenHashForUpdate(HashUtility.hash(rawToken));
        if (storedSession.isEmpty()) return Optional.empty();

        final ParentSession session = storedSession.get();
        if (!session.getExpiresAt().isAfter(clock.instant())) {
            session.setRevokedAt(clock.instant());
            sessionRepository.save(session);
            return Optional.empty();
        }
        if (!session.getDeviceIdHash().isBlank()
                && !session.getDeviceIdHash().equals(hashDeviceId(deviceId)))
            return Optional.empty();

        final String rotatedToken = generateToken();
        final Instant now = clock.instant();
        session.setTokenHash(HashUtility.hash(rotatedToken));
        session.setLastUsedAt(now);
        session.setExpiresAt(now.plus(ttlDays, ChronoUnit.DAYS));
        sessionRepository.save(session);
        return Optional.of(toToken(session.getParent(), rotatedToken, session.getExpiresAt()));
    }

    @Transactional
    public Optional<Long> validate(final String rawToken) {
        final Optional<ParentSession> storedSession = findUsable(rawToken);
        if (storedSession.isEmpty()) return Optional.empty();

        final ParentSession session = storedSession.get();
        session.setLastUsedAt(clock.instant());
        sessionRepository.save(session);
        return Optional.of(session.getParent().getId());
    }

    @Transactional
    public boolean revoke(final Long parentId, final String rawToken, final boolean revokeAll) {
        if (revokeAll) {
            revokeAll(parentId);
            return true;
        }
        if (rawToken == null || rawToken.isBlank()) return false;

        final Optional<ParentSession> storedSession = sessionRepository.findByTokenHashAndRevokedAtIsNull(HashUtility.hash(rawToken));
        if (storedSession.isEmpty() || !storedSession.get().getParent().getId().equals(parentId))
            return false;

        storedSession.get().setRevokedAt(clock.instant());
        sessionRepository.save(storedSession.get());
        return true;
    }

    @Transactional
    public void revokeAll(final Long parentId) {
        final Instant now = clock.instant();
        final List<ParentSession> sessions = sessionRepository.findByParentIdAndRevokedAtIsNull(parentId);
        for (final ParentSession session : sessions)
            session.setRevokedAt(now);
        sessionRepository.saveAll(sessions);
    }

    private Optional<ParentSession> findUsable(final String rawToken) {
        if (rawToken == null || rawToken.isBlank()) return Optional.empty();

        final Optional<ParentSession> storedSession = sessionRepository.findByTokenHashAndRevokedAtIsNull(HashUtility.hash(rawToken));
        if (storedSession.isEmpty()) return Optional.empty();
        if (storedSession.get().getExpiresAt().isAfter(clock.instant())) return storedSession;

        storedSession.get().setRevokedAt(clock.instant());
        sessionRepository.save(storedSession.get());
        return Optional.empty();
    }

    private SessionToken toToken(final Parent parent, final String rawToken, final Instant expiresAt) {
        return new SessionToken(
                parent.getId(),
                parent.getProfilePicture() == null ? "" : parent.getProfilePicture(),
                rawToken,
                expiresAt.getEpochSecond()
        );
    }

    private String generateToken() {
        final byte[] tokenBytes = new byte[32];
        secureRandom.nextBytes(tokenBytes);
        return Base64.getUrlEncoder().withoutPadding().encodeToString(tokenBytes);
    }

    private String hashDeviceId(final String deviceId) {
        return deviceId == null || deviceId.isBlank() ? "" : HashUtility.hash(deviceId.trim());
    }

    public record SessionToken(Long parentId, String parentPfp, String rawToken, long expiresAtEpochSeconds) {
        /* w */
    }
}
