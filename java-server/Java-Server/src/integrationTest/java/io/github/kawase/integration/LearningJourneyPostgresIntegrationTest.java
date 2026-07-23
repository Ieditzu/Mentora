package io.github.kawase.integration;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.entity.Task;
import io.github.kawase.database.repository.ChildCourseProgressRepository;
import io.github.kawase.database.repository.ChildMachineLearningProgressRepository;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.repository.CompletedTaskRepository;
import io.github.kawase.database.services.ChildService;
import io.github.kawase.database.services.CourseService;
import io.github.kawase.database.services.ParentService;
import io.github.kawase.database.services.TaskService;
import io.github.kawase.machinelearning.MachineLearningExecutor;
import io.github.kawase.machinelearning.MachineLearningService;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.test.annotation.DirtiesContext;
import org.testcontainers.junit.jupiter.Testcontainers;

import java.util.List;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.when;

@Testcontainers(disabledWithoutDocker = true)
@SpringBootTest
@DirtiesContext(classMode = DirtiesContext.ClassMode.AFTER_CLASS)
class LearningJourneyPostgresIntegrationTest extends PostgresIntegrationTestSupport {
    @Autowired
    private ParentService parentService;

    @Autowired
    private ChildService childService;

    @Autowired
    private CourseService courseService;

    @Autowired
    private MachineLearningService machineLearningService;

    @Autowired
    private TaskService taskService;

    @Autowired
    private ChildRepository childRepository;

    @Autowired
    private ChildCourseProgressRepository courseProgressRepository;

    @Autowired
    private ChildMachineLearningProgressRepository machineLearningProgressRepository;

    @Autowired
    private CompletedTaskRepository completedTaskRepository;

    @Autowired
    private ObjectMapper objectMapper;

    @MockBean
    private MachineLearningExecutor machineLearningExecutor;

    @Test
    void creatorContentAndHiddenEvaluationPersistIntoTheParentVisibleProfile() throws Exception {
        final Parent parent = parentService.createParentAccount(
                "journey-" + UUID.randomUUID() + "@mentora.test",
                "password-hash"
        );
        final Child child = childService.addChildToParent(parent.getId(), "Ada");
        final CourseService.CourseDetailDto course = courseService.createCourseDetail(
                parent.getId(),
                new CourseService.UpsertCourseRequest(
                        "Python Functions",
                        "PY-FN",
                        "python",
                        "beginner",
                        "Functions and return values",
                        "Creator-authored integration fixture",
                        30,
                        true,
                        List.of(
                                new CourseService.UpsertQuizQuestionRequest(
                                        0, "Which keyword defines a function?", "def", "func", "let", "class", 0, ""
                                ),
                                new CourseService.UpsertQuizQuestionRequest(
                                        1, "Which keyword sends a value back?", "yield", "return", "break", "print", 1, ""
                                )
                        )
                )
        );

        assertEquals(course.id(), courseService.getPublishedCoursesForChild(child.getId()).getFirst().id());

        assertThrows(RuntimeException.class,
                () -> courseService.recordCourseCompletion(child.getId(), course.id(), 1, 1));
        assertThrows(RuntimeException.class,
                () -> courseService.recordCourseCompletion(child.getId(), course.id(), 99, 2));
        courseService.recordCourseCompletion(child.getId(), course.id(), 1, 2);
        courseService.recordCourseCompletion(child.getId(), course.id(), 2, 2);
        courseService.recordCourseCompletion(child.getId(), course.id(), 2, 2);
        final Task task = new Task();
        task.setTitle("Integration loops task");
        task.setPointValue(15);
        final Task savedTask = taskService.saveTask(task);
        taskService.completeTask(child.getId(), savedTask.getId());
        taskService.completeTask(child.getId(), savedTask.getId());
        when(machineLearningExecutor.execute(any(), any())).thenReturn(
                new MachineLearningExecutor.ExecutionResult(
                        false,
                        true,
                        objectMapper.readTree("[13,15,17]"),
                        "",
                        ""
                )
        );

        final JsonNode firstSubmission = objectMapper.readTree(machineLearningService.submit(
                child.getId(),
                "easy-line-of-best-fit",
                "def solve(train_path, test_path): return [13, 15, 17]"
        ));
        final JsonNode repeatedSubmission = objectMapper.readTree(machineLearningService.submit(
                child.getId(),
                "easy-line-of-best-fit",
                "def solve(train_path, test_path): return [13, 15, 17]"
        ));
        final Child storedChild = childRepository.findById(child.getId()).orElseThrow();

        assertTrue(firstSubmission.path("passed").asBoolean());
        assertTrue(firstSubmission.path("rewardGranted").asBoolean());
        assertFalse(repeatedSubmission.path("rewardGranted").asBoolean());
        assertEquals(65, storedChild.getTotalPoints());
        assertTrue(storedChild.getGameStats().containsKey("aiProfileMachineLearning"));
        assertEquals(3, courseProgressRepository.findByChild(storedChild).getFirst().getAttemptCount());
        assertEquals(1, completedTaskRepository.findByChildIdOrderByCompletedAtDesc(child.getId()).size());
        assertEquals(2, machineLearningProgressRepository
                .findByChildIdAndProblemSlug(child.getId(), "easy-line-of-best-fit")
                .orElseThrow()
                .getAttemptCount());
        assertEquals(65, parentService.getChildren(parent.getId()).getFirst().getTotalPoints());
    }
}
