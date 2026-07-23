package io.github.kawase.database.services;

import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.repository.ParentRepository;
import io.github.kawase.security.ParentPasswordService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;
import java.util.Optional;

@Service
@RequiredArgsConstructor
public class ParentService {
    private final ParentRepository parentRepository;
    private final ParentPasswordService parentPasswordService;

    @Transactional
    public Parent createParentAccount(final String email, final String passwordHash) {
        if (parentRepository.findByEmail(email).isPresent()) {
            throw new RuntimeException("An account with this email already exists!");
        }

        final Parent newParent = new Parent();
        newParent.setEmail(email);
        newParent.setPasswordHash(parentPasswordService.encode(passwordHash));

        return parentRepository.save(newParent);
    }

    public Optional<Parent> findByEmail(final String email) {
        return parentRepository.findByEmail(email);
    }

    public Optional<Parent> findById(final Long id) {
        return parentRepository.findById(id);
    }

    @Transactional
    public void updatePfp(final Long parentId, final String base64Pfp) {
        parentRepository.findById(parentId).ifPresent(parent -> {
            parent.setProfilePicture(base64Pfp);
            parentRepository.save(parent);
        });
    }

    @Transactional(readOnly = true)
    public List<Child> getChildren(final Long parentId) {
        return parentRepository.findById(parentId)
                .map(parent -> {
                    parent.getChildEntities().size(); // Force initialization
                    return parent.getChildEntities();
                })
                .orElse(List.of());
    }
}
