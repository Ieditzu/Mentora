package io.github.kawase.database.services;

import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.Course;
import io.github.kawase.database.entity.CourseQuizQuestion;
import io.github.kawase.database.repository.ChildCourseProgressRepository;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.repository.CourseRepository;
import io.github.kawase.database.repository.ParentRepository;
import org.junit.jupiter.api.Test;

import java.util.Optional;

import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class CourseServiceValidationTest {
    private final CourseRepository courseRepository = mock(CourseRepository.class);
    private final ParentRepository parentRepository = mock(ParentRepository.class);
    private final ChildRepository childRepository = mock(ChildRepository.class);
    private final ChildCourseProgressRepository progressRepository = mock(ChildCourseProgressRepository.class);
    private final LearningProfileService learningProfileService = mock(LearningProfileService.class);
    private final CourseService service = new CourseService(
            courseRepository,
            parentRepository,
            childRepository,
            progressRepository,
            learningProfileService
    );

    @Test
    void rejectsClientControlledQuestionCountsAndOutOfRangeScores() {
        final Child child = new Child();
        child.setId(7L);
        child.setName("Ada");
        final Course course = new Course();
        course.setId(11L);
        course.setPublished(true);
        course.getQuestions().add(new CourseQuizQuestion());
        course.getQuestions().add(new CourseQuizQuestion());
        when(childRepository.findById(7L)).thenReturn(Optional.of(child));
        when(courseRepository.findByIdAndPublishedTrue(11L)).thenReturn(Optional.of(course));

        assertThrows(RuntimeException.class, () -> service.recordCourseCompletion(7L, 11L, 1, 1));
        assertThrows(RuntimeException.class, () -> service.recordCourseCompletion(7L, 11L, 3, 2));
        assertThrows(RuntimeException.class, () -> service.recordCourseCompletion(7L, 11L, -1, 2));

        verify(progressRepository, never()).save(any());
        verify(childRepository, never()).save(any());
    }
}
