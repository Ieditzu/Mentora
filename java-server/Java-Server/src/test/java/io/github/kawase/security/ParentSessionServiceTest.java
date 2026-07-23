package io.github.kawase.security;

import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.entity.ParentSession;
import io.github.kawase.database.repository.ParentRepository;
import io.github.kawase.database.repository.ParentSessionRepository;
import io.github.kawase.utility.HashUtility;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;

import java.security.SecureRandom;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.Optional;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.atomic.AtomicInteger;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class ParentSessionServiceTest {
    @Test
    void storesOnlyTokenHashAndRotatesOnResume() {
        final ParentSessionRepository sessionRepository = mock(ParentSessionRepository.class);
        final ParentRepository parentRepository = mock(ParentRepository.class);
        final Parent parent = new Parent();
        parent.setId(4L);
        when(parentRepository.findById(4L)).thenReturn(Optional.of(parent));
        when(sessionRepository.save(any(ParentSession.class))).thenAnswer(invocation -> invocation.getArgument(0));
        final ParentSessionService service = new ParentSessionService(
                sessionRepository,
                parentRepository,
                Clock.fixed(Instant.parse("2026-07-23T10:00:00Z"), ZoneOffset.UTC),
                new SecureRandom(),
                30
        );

        final ParentSessionService.SessionToken issued = service.issue(4L, "phone-1");
        final ArgumentCaptor<ParentSession> captor = ArgumentCaptor.forClass(ParentSession.class);
        verify(sessionRepository).save(captor.capture());
        final ParentSession stored = captor.getValue();

        assertNotEquals(issued.rawToken(), stored.getTokenHash());
        assertEquals(HashUtility.hash(issued.rawToken()), stored.getTokenHash());
        when(sessionRepository.findActiveByTokenHashForUpdate(HashUtility.hash(issued.rawToken())))
                .thenReturn(Optional.of(stored));

        final var resumed = service.resume(issued.rawToken(), "phone-1");

        assertTrue(resumed.isPresent());
        assertNotEquals(issued.rawToken(), resumed.get().rawToken());
        assertEquals(HashUtility.hash(resumed.get().rawToken()), stored.getTokenHash());
    }

    @Test
    void rejectsResumeFromDifferentDevice() {
        final ParentSessionRepository sessionRepository = mock(ParentSessionRepository.class);
        final ParentRepository parentRepository = mock(ParentRepository.class);
        final ParentSession stored = new ParentSession();
        stored.setTokenHash(HashUtility.hash("token"));
        stored.setDeviceIdHash(HashUtility.hash("phone-1"));
        stored.setExpiresAt(Instant.parse("2026-08-23T10:00:00Z"));
        when(sessionRepository.findActiveByTokenHashForUpdate(HashUtility.hash("token")))
                .thenReturn(Optional.of(stored));
        final ParentSessionService service = new ParentSessionService(
                sessionRepository,
                parentRepository,
                Clock.fixed(Instant.parse("2026-07-23T10:00:00Z"), ZoneOffset.UTC),
                new SecureRandom(),
                30
        );

        assertFalse(service.resume("token", "phone-2").isPresent());
    }

    @Test
    void concurrentResumeConsumesOriginalTokenOnlyOnce() {
        final ParentSessionRepository sessionRepository = mock(ParentSessionRepository.class);
        final ParentRepository parentRepository = mock(ParentRepository.class);
        final Parent parent = new Parent();
        parent.setId(4L);
        final ParentSession stored = new ParentSession();
        stored.setParent(parent);
        stored.setTokenHash(HashUtility.hash("original-token"));
        stored.setDeviceIdHash(HashUtility.hash("phone-1"));
        stored.setExpiresAt(Instant.parse("2026-08-23T10:00:00Z"));
        when(sessionRepository.findActiveByTokenHashForUpdate(any()))
                .thenAnswer(invocation -> invocation.getArgument(0).equals(stored.getTokenHash())
                        ? Optional.of(stored)
                        : Optional.empty());
        when(sessionRepository.save(any(ParentSession.class))).thenAnswer(invocation -> invocation.getArgument(0));
        final ParentSessionService service = new ParentSessionService(
                sessionRepository,
                parentRepository,
                Clock.fixed(Instant.parse("2026-07-23T10:00:00Z"), ZoneOffset.UTC),
                new SecureRandom(),
                30
        );
        final CountDownLatch start = new CountDownLatch(1);
        final AtomicInteger successfulResumes = new AtomicInteger();

        final CompletableFuture<Void> first = CompletableFuture.runAsync(() -> resumeAfterLatch(
                service,
                start,
                successfulResumes
        ));
        final CompletableFuture<Void> second = CompletableFuture.runAsync(() -> resumeAfterLatch(
                service,
                start,
                successfulResumes
        ));
        start.countDown();
        CompletableFuture.allOf(first, second).join();

        assertEquals(1, successfulResumes.get());
    }

    private void resumeAfterLatch(
            final ParentSessionService service,
            final CountDownLatch start,
            final AtomicInteger successfulResumes) {
        try {
            start.await();
            if (service.resume("original-token", "phone-1").isPresent())
                successfulResumes.incrementAndGet();
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            throw new RuntimeException(exception);
        }
    }
}
