package io.github.kawase.database.services;

import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.ChildCourseProgress;
import io.github.kawase.database.entity.Course;
import io.github.kawase.database.entity.CourseQuizQuestion;
import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.repository.ChildCourseProgressRepository;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.repository.CourseRepository;
import io.github.kawase.database.repository.ParentRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.stream.Collectors;

@Service
@RequiredArgsConstructor
public class CourseService {
    private final CourseRepository courseRepository;
    private final ParentRepository parentRepository;
    private final ChildRepository childRepository;
    private final ChildCourseProgressRepository childCourseProgressRepository;
    private final LearningProfileService learningProfileService;

    @Transactional
    public Course createCourse(final Long parentId, final UpsertCourseRequest request) {
        final Parent parent = requireParent(parentId);
        final Course course = new Course();
        course.setParent(parent);
        applyCourseRequest(course, request);
        return courseRepository.save(course);
    }

    @Transactional
    public CourseDetailDto createCourseDetail(final Long parentId, final UpsertCourseRequest request) {
        return toDetail(createCourse(parentId, request), false);
    }

    @Transactional
    public Course updateCourse(final Long parentId, final Long courseId, final UpsertCourseRequest request) {
        final Course course = requireOwnedCourse(parentId, courseId);
        applyCourseRequest(course, request);
        return courseRepository.save(course);
    }

    @Transactional
    public CourseDetailDto updateCourseDetail(final Long parentId, final Long courseId, final UpsertCourseRequest request) {
        return toDetail(updateCourse(parentId, courseId, request), false);
    }

    @Transactional
    public void deleteCourse(final Long parentId, final Long courseId) {
        final Course course = requireOwnedCourse(parentId, courseId);
        courseRepository.delete(course);
    }

    @Transactional(readOnly = true)
    public List<CourseSummaryDto> getCoursesForParent(final Long parentId) {
        final Parent parent = requireParent(parentId);
        return courseRepository.findByParentOrderByUpdatedAtDesc(parent).stream()
                .map(course -> toSummary(course, false))
                .toList();
    }

    @Transactional(readOnly = true)
    public CourseDetailDto getOwnedCourse(final Long parentId, final Long courseId) {
        return toDetail(requireOwnedCourse(parentId, courseId), false);
    }

    @Transactional(readOnly = true)
    public List<CourseSummaryDto> getPublishedCoursesForChild(final Long childId) {
        final Set<Long> completedCourseIds;
        if (childId == null)
            completedCourseIds = Set.of();
        else {
            final Child child = requireChild(childId);
            completedCourseIds = childCourseProgressRepository.findByChild(child).stream()
                    .filter(progress -> Boolean.TRUE.equals(progress.getCompleted()))
                    .map(progress -> progress.getCourse().getId())
                    .collect(Collectors.toSet());
        }

        return courseRepository.findByPublishedTrueOrderByUpdatedAtDesc().stream()
                .map(course -> toSummary(course, completedCourseIds.contains(course.getId())))
                .toList();
    }

    @Transactional(readOnly = true)
    public CourseDetailDto getPublishedCourseDetail(final Long courseId, final Long childId) {
        final Course course = courseRepository.findByIdAndPublishedTrue(courseId)
                .orElseThrow(() -> new RuntimeException("Course not found"));
        final boolean completed;
        if (childId == null)
            completed = false;
        else {
            final Child child = requireChild(childId);
            completed = childCourseProgressRepository.findByChildAndCourse(child, course)
                    .map(progress -> Boolean.TRUE.equals(progress.getCompleted()))
                    .orElse(false);
        }
        return toDetail(course, completed);
    }

    @Transactional
    public ChildCourseProgress recordCourseCompletion(final Long childId, final long courseId, final int score, final int totalQuestions) {
        final Child child = requireChild(childId);
        final Course course = courseRepository.findByIdAndPublishedTrue(courseId)
                .orElseThrow(() -> new RuntimeException("Course not found"));
        final int storedQuestionCount = course.getQuestions().size();
        if (totalQuestions != storedQuestionCount)
            throw new RuntimeException("Submitted question count does not match the published course");
        if (score < 0 || score > storedQuestionCount)
            throw new RuntimeException("Submitted score is outside the published course range");

        final ChildCourseProgress progress = childCourseProgressRepository.findByChildAndCourse(child, course)
                .orElseGet(() -> {
                    final ChildCourseProgress created = new ChildCourseProgress();
                    created.setChild(child);
                    created.setCourse(course);
                    return created;
                });

        progress.setAttemptCount(progress.getAttemptCount() + 1);
        progress.setLastScore(score);
        progress.setBestScore(Math.max(progress.getBestScore(), score));
        progress.setTotalQuestions(storedQuestionCount);
        progress.setLastAttemptAt(Instant.now());

        final boolean completedNow = storedQuestionCount > 0 && score == storedQuestionCount;
        if (completedNow) {
            progress.setCompleted(true);
            if (progress.getCompletedAt() == null)
                progress.setCompletedAt(Instant.now());
        }

        if (completedNow && !Boolean.TRUE.equals(progress.getRewardGranted())) {
            child.setTotalPoints(child.getTotalPoints() + course.getPointReward());
            progress.setRewardGranted(true);
            childRepository.save(child);
        }

        final String topic = course.getLanguage() + "_course:" + course.getAcronym();
        final String details = "Course=" + course.getTitle() + ", score=" + score + "/" + storedQuestionCount;
        learningProfileService.recordLearningEvent(childId, "course_quiz_attempt", topic, completedNow ? 1 : 0, details);

        return childCourseProgressRepository.save(progress);
    }

    private void applyCourseRequest(final Course course, final UpsertCourseRequest request) {
        if (request == null)
            throw new RuntimeException("Missing course payload");
        if (request.title() == null || request.title().isBlank())
            throw new RuntimeException("Course title is required");
        if (request.questions() == null || request.questions().isEmpty())
            throw new RuntimeException("At least one quiz question is required");

        course.setTitle(request.title().trim());
        course.setAcronym(sanitizeAcronym(request.acronym(), request.title()));
        course.setLanguage(normalizeValue(request.language(), "general"));
        course.setDifficulty(normalizeValue(request.difficulty(), "beginner"));
        course.setSummary(trimToLength(request.summary(), 280, request.title()));
        course.setDescription(request.description() == null ? "" : request.description().trim());
        course.setPointReward(request.pointReward() == null ? 50 : Math.max(0, request.pointReward()));
        course.setPublished(Boolean.TRUE.equals(request.published()));

        final List<CourseQuizQuestion> rebuiltQuestions = new ArrayList<>();
        final List<UpsertQuizQuestionRequest> incomingQuestions = new ArrayList<>(request.questions());
        incomingQuestions.sort(Comparator.comparingInt(q -> q.orderIndex() == null ? 0 : q.orderIndex()));

        int nextIndex = 0;
        for (final UpsertQuizQuestionRequest questionRequest : incomingQuestions) {
            if (questionRequest == null || questionRequest.prompt() == null || questionRequest.prompt().isBlank())
                throw new RuntimeException("Each question must have a prompt");

            final List<String> options = List.of(
                    safeOption(questionRequest.optionA()),
                    safeOption(questionRequest.optionB()),
                    safeOption(questionRequest.optionC()),
                    safeOption(questionRequest.optionD())
            );

            final int correctIndex = questionRequest.correctIndex() == null ? -1 : questionRequest.correctIndex();
            if (correctIndex < 0 || correctIndex > 3)
                throw new RuntimeException("Each question must define a correct answer between 0 and 3");

            final CourseQuizQuestion question = new CourseQuizQuestion();
            question.setCourse(course);
            question.setOrderIndex(nextIndex++);
            question.setPrompt(questionRequest.prompt().trim());
            question.setOptionA(options.get(0));
            question.setOptionB(options.get(1));
            question.setOptionC(options.get(2));
            question.setOptionD(options.get(3));
            question.setCorrectIndex(correctIndex);
            question.setExplanation(questionRequest.explanation() == null ? "" : questionRequest.explanation().trim());
            rebuiltQuestions.add(question);
        }

        course.getQuestions().clear();
        course.getQuestions().addAll(rebuiltQuestions);
    }

    private Parent requireParent(final Long parentId) {
        return parentRepository.findById(parentId)
                .orElseThrow(() -> new RuntimeException("Parent not found"));
    }

    private Child requireChild(final Long childId) {
        return childRepository.findById(childId)
                .orElseThrow(() -> new RuntimeException("Child not found"));
    }

    private Course requireOwnedCourse(final Long parentId, final Long courseId) {
        final Course course = courseRepository.findById(courseId)
                .orElseThrow(() -> new RuntimeException("Course not found"));
        if (course.getParent() == null || !course.getParent().getId().equals(parentId))
            throw new RuntimeException("Access denied");
        course.getQuestions().size();
        return course;
    }

    private CourseSummaryDto toSummary(final Course course, final boolean completed) {
        return new CourseSummaryDto(
                course.getId(),
                course.getTitle(),
                course.getAcronym(),
                course.getLanguage(),
                course.getDifficulty(),
                course.getSummary(),
                course.getPointReward(),
                Boolean.TRUE.equals(course.getPublished()),
                course.getQuestions().size(),
                completed,
                course.getUpdatedAt() != null ? course.getUpdatedAt().toString() : ""
        );
    }

    private CourseDetailDto toDetail(final Course course, final boolean completed) {
        final List<CourseQuestionDto> questions = course.getQuestions().stream()
                .sorted(Comparator.comparingInt(CourseQuizQuestion::getOrderIndex))
                .map(question -> new CourseQuestionDto(
                        question.getId(),
                        question.getOrderIndex(),
                        question.getPrompt(),
                        List.of(question.getOptionA(), question.getOptionB(), question.getOptionC(), question.getOptionD()),
                        question.getCorrectIndex(),
                        question.getExplanation() == null ? "" : question.getExplanation()
                ))
                .toList();

        return new CourseDetailDto(
                course.getId(),
                course.getTitle(),
                course.getAcronym(),
                course.getLanguage(),
                course.getDifficulty(),
                course.getSummary(),
                course.getDescription() == null ? "" : course.getDescription(),
                course.getPointReward(),
                Boolean.TRUE.equals(course.getPublished()),
                completed,
                questions,
                course.getUpdatedAt() != null ? course.getUpdatedAt().toString() : ""
        );
    }

    private String normalizeValue(final String value, final String fallback) {
        return value == null || value.isBlank() ? fallback : value.trim().toLowerCase();
    }

    private String sanitizeAcronym(final String acronym, final String title) {
        final String base = acronym == null || acronym.isBlank() ? title : acronym;
        final String sanitized = base.toUpperCase()
                .replaceAll("[^A-Z0-9]+", "-")
                .replaceAll("(^-|-$)", "");
        return sanitized.isBlank() ? "COURSE" : sanitized.substring(0, Math.min(24, sanitized.length()));
    }

    private String trimToLength(final String value, final int maxLength, final String fallback) {
        final String resolved = value == null || value.isBlank() ? fallback : value.trim();
        return resolved.length() <= maxLength ? resolved : resolved.substring(0, maxLength);
    }

    private String safeOption(final String value) {
        if (value == null || value.isBlank())
            throw new RuntimeException("Each question must have four answer options");
        return value.trim();
    }

    public record UpsertCourseRequest(
            String title, String acronym, String language, String difficulty, String summary, String description,
            Integer pointReward,
            Boolean published,
            List<UpsertQuizQuestionRequest> questions) {
        /* w */
    }

    public record UpsertQuizQuestionRequest(
            Integer orderIndex,
            String prompt, String optionA, String optionB, String optionC, String optionD,
            Integer correctIndex,
            String explanation) {
        /* w */
    }

    public record CourseSummaryDto(
            Long id,
            String title, String acronym, String language, String difficulty, String summary,
            Integer pointReward,
            Boolean published,
            Integer questionCount,
            Boolean completed,
            String updatedAt) {
        /* w */
    }

    public record CourseDetailDto(
            Long id,
            String title, String acronym, String language, String difficulty, String summary, String description,
            Integer pointReward,
            Boolean published, Boolean completed,
            List<CourseQuestionDto> questions,
            String updatedAt) {
        /* w */
    }

    public record CourseQuestionDto(
            Long id,
            Integer orderIndex,
            String prompt,
            List<String> options,
            Integer correctIndex,
            String explanation) {
        /* w */
    }
}
