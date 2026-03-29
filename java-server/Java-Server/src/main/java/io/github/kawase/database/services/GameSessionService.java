package io.github.kawase.database.services;

import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.GameSession;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.repository.GameSessionRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import java.util.Optional;

@Service
@RequiredArgsConstructor
public class GameSessionService {

    private final GameSessionRepository gameSessionRepository;
    private final ChildRepository childRepository;

    @Transactional
    public String createOrUpdateSession(Long childId, String token) {
        Child child = childRepository.findById(childId)
                .orElseThrow(() -> new RuntimeException("Child not found"));

        GameSession session = gameSessionRepository.findByChildId(childId)
                .orElse(new GameSession());
        
        session.setChild(child);
        session.setSessionToken(token);
        gameSessionRepository.save(session);
        return token;
    }

    @Transactional(readOnly = true)
    public boolean verifySession(Long childId, String token) {
        return gameSessionRepository.findByChildIdAndSessionToken(childId, token).isPresent();
    }
}
