package io.github.kawase.web;

import org.springframework.stereotype.Service;

import java.time.Instant;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

@Service
public class WebSessionService {
    private static final long SESSION_TTL_SECONDS = 7L * 24L * 60L * 60L;
    private final Map<String, SessionRecord> sessions = new ConcurrentHashMap<>();

    public String createSession(final Long parentId) {
        String token = UUID.randomUUID().toString();
        sessions.put(token, new SessionRecord(parentId, Instant.now().plusSeconds(SESSION_TTL_SECONDS)));
        return token;
    }

    public Long requireParentId(final String token) {
        SessionRecord session = sessions.get(token);
        if (session == null || session.expiresAt().isBefore(Instant.now())) {
            sessions.remove(token);
            throw new RuntimeException("Unauthorized");
        }
        return session.parentId();
    }

    private record SessionRecord(Long parentId, Instant expiresAt) {}
}
