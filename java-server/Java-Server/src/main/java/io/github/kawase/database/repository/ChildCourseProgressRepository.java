package io.github.kawase.database.repository;

import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.ChildCourseProgress;
import io.github.kawase.database.entity.Course;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;
import java.util.Optional;

public interface ChildCourseProgressRepository extends JpaRepository<ChildCourseProgress, Long> {
    Optional<ChildCourseProgress> findByChildAndCourse(Child child, Course course);
    List<ChildCourseProgress> findByChild(Child child);
}
