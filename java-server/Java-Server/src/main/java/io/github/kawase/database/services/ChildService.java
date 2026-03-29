package io.github.kawase.database.services;

import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.repository.ParentRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import java.time.LocalDate;

@Service
@RequiredArgsConstructor
public class ChildService {

    private final ChildRepository childRepository;
    private final ParentRepository parentRepository;

    @Transactional
    public Child addChildToParent(final Long parentId, final String childName) {
        final Parent parent = parentRepository.findById(parentId)
                .orElseThrow(() -> new RuntimeException("Parent not found with ID: " + parentId));

        final Child newChild = new Child();
        newChild.setName(childName);
        newChild.setParent(parent);
        
        return childRepository.save(newChild);
    }

    public java.util.Optional<Child> findById(final Long id) {
        return childRepository.findById(id);
    }

    @Transactional(readOnly = true)
    public java.util.List<io.github.kawase.database.entity.Goal> getGoals(final Long childId) {
        return childRepository.findById(childId)
                .map(child -> {
                    child.getGoals().forEach(goal -> {
                        if (goal.getRequiredTask() != null) {
                            goal.getRequiredTask().getTitle();
                        }
                    });
                    return child.getGoals();
                })
                .orElse(java.util.List.of());
    }

    @Transactional
    public void updatePfp(final Long childId, final String base64Pfp) {
        childRepository.findById(childId).ifPresent(child -> {
            child.setProfilePicture(base64Pfp);
            childRepository.save(child);
        });
    }

    @Transactional
    public void deleteChild(final Long childId) {
        childRepository.deleteById(childId);
    }

    @Transactional
    public int updateStreak(final Long childId) {
        return childRepository.findById(childId).map(child -> {
            LocalDate today = LocalDate.now();
            LocalDate lastLogin = child.getLastLoginDate();

            if (today.equals(lastLogin)) {
                return child.getStreak();
            }

            if (lastLogin != null && lastLogin.plusDays(1).equals(today)) {
                child.setStreak(child.getStreak() + 1);
            } else {
                child.setStreak(1);
            }
            child.setLastLoginDate(today);
            childRepository.save(child);
            return child.getStreak();
        }).orElse(0);
    }

    @Transactional(readOnly = true)
    public java.util.List<io.github.kawase.database.entity.CompletedTask> getCompletedTasks(final Long childId) {
        return childRepository.findById(childId)
                .map(child -> {
                    child.getCompletedTasks().forEach(ct -> {
                        if (ct.getTask() != null) {
                            ct.getTask().getTitle(); // Force initialization of task proxy
                        }
                    });
                    return child.getCompletedTasks();
                })
                .orElse(java.util.List.of());
    }
}