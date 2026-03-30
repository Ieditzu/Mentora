package io.github.kawase.database.repository;

import io.github.kawase.database.entity.Course;
import io.github.kawase.database.entity.Parent;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;
import java.util.Optional;

public interface CourseRepository extends JpaRepository<Course, Long> {
    List<Course> findByParentOrderByUpdatedAtDesc(Parent parent);
    List<Course> findByPublishedTrueOrderByUpdatedAtDesc();
    Optional<Course> findByIdAndPublishedTrue(Long id);
}
