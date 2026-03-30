package io.github.kawase.database.repository;

import io.github.kawase.database.entity.CourseQuizQuestion;
import org.springframework.data.jpa.repository.JpaRepository;

public interface CourseQuizQuestionRepository extends JpaRepository<CourseQuizQuestion, Long> {
}
