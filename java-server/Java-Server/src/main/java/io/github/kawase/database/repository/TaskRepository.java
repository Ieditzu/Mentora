package io.github.kawase.database.repository;

import io.github.kawase.database.entity.Task;
import org.springframework.data.jpa.repository.JpaRepository;

public interface TaskRepository extends JpaRepository<Task, Long> {
    /* w */
}