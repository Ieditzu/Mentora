package io.github.kawase.database.repository;

import io.github.kawase.database.entity.ChildMachineLearningProgress;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;
import java.util.Optional;

public interface ChildMachineLearningProgressRepository extends JpaRepository<ChildMachineLearningProgress, Long> {
    Optional<ChildMachineLearningProgress> findByChildIdAndProblemSlug(Long childId, String problemSlug);
    List<ChildMachineLearningProgress> findByChildId(Long childId);
}
