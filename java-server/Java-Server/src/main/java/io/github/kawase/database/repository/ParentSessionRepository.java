package io.github.kawase.database.repository;

import io.github.kawase.database.entity.ParentSession;
import jakarta.persistence.LockModeType;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;
import java.util.Optional;

public interface ParentSessionRepository extends JpaRepository<ParentSession, Long> {
    Optional<ParentSession> findByTokenHashAndRevokedAtIsNull(final String tokenHash);
    List<ParentSession> findByParentIdAndRevokedAtIsNull(final Long parentId);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("select session from ParentSession session where session.tokenHash = :tokenHash and session.revokedAt is null")
    Optional<ParentSession> findActiveByTokenHashForUpdate(@Param("tokenHash") final String tokenHash);
}
