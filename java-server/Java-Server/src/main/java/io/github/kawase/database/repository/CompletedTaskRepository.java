package io.github.kawase.database.repository;

import io.github.kawase.database.entity.CompletedTask;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

public interface CompletedTaskRepository extends JpaRepository<CompletedTask, Long> {
    List<CompletedTask> findByChildIdOrderByCompletedAtDesc(Long childId);
}
