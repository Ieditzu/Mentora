package io.github.kawase.database.repository;

import io.github.kawase.database.entity.GameSession;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;
import java.util.Optional;

@Repository
public interface GameSessionRepository extends JpaRepository<GameSession, Long> {
    Optional<GameSession> findByChildId(Long childId);
    Optional<GameSession> findByChildIdAndSessionToken(Long childId, String sessionToken);
}
