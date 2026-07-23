package io.github.kawase.database.repository;

import io.github.kawase.database.entity.CreatorMachineLearningProblem;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

public interface CreatorMachineLearningProblemRepository extends JpaRepository<CreatorMachineLearningProblem, Long> {
    boolean existsBySlug(final String slug);
    List<CreatorMachineLearningProblem> findByParentIdOrderByUpdatedAtDesc(final Long parentId);
    List<CreatorMachineLearningProblem> findByPublishedTrueOrderByUpdatedAtDesc();
}
