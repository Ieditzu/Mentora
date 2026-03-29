package io.github.kawase.database.repository;

import io.github.kawase.database.entity.CompletedTask;
import org.springframework.data.jpa.repository.JpaRepository;

public interface CompletedTaskRepository extends JpaRepository<CompletedTask, Long> {
    /* w */
}