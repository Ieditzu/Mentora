package io.github.kawase.database.services;

import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.repository.ParentRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.Optional;

@Service
@RequiredArgsConstructor
public class ParentService {
    private final ParentRepository parentRepository;

    @Transactional
    public Parent createParentAccount(final String email, final String passwordHash) {
        if (parentRepository.findByEmail(email).isPresent()) {
            throw new RuntimeException("An account with this email already exists!");
        }

        final Parent newParent = new Parent();
        newParent.setEmail(email);
        newParent.setPasswordHash(passwordHash);

        return parentRepository.save(newParent);
    }

    public boolean loginParent(final String email, final String passwordHash) {
        final Optional<Parent> parentOpt = parentRepository.findByEmail(email);

        if (parentOpt.isPresent()) {
            final Parent parent = parentOpt.get();
            return parent.getPasswordHash().equals(passwordHash);
        }

        return false;
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
    public java.util.List<io.github.kawase.database.entity.Child> getChildren(final Long parentId) {
        return parentRepository.findById(parentId)
                .map(parent -> {
                    parent.getChildEntities().size(); // Force initialization
                    return parent.getChildEntities();
                })
                .orElse(java.util.List.of());
    }
}
