package io.github.kawase.machinelearning;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.ChildMachineLearningProgress;
import io.github.kawase.database.repository.ChildMachineLearningProgressRepository;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.services.LearningProfileService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.util.Optional;
import java.util.concurrent.atomic.AtomicReference;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class MachineLearningEvaluatorTest {
    private final ObjectMapper objectMapper = new ObjectMapper();
    private final MachineLearningExecutor executor = mock(MachineLearningExecutor.class);
    private final ChildRepository childRepository = mock(ChildRepository.class);
    private final ChildMachineLearningProgressRepository progressRepository = mock(ChildMachineLearningProgressRepository.class);
    private final LearningProfileService learningProfileService = mock(LearningProfileService.class);
    private final MachineLearningService service = new MachineLearningService(
            new MachineLearningCatalog(),
            executor,
            childRepository,
            progressRepository,
            learningProfileService,
            objectMapper
    );
    private final AtomicReference<ChildMachineLearningProgress> storedProgress = new AtomicReference<>();
    private final Child child = new Child();

    @BeforeEach
    void setUpPersistence() {
        child.setId(41L);
        child.setName("Ada");
        child.setTotalPoints(0);
        when(childRepository.findById(41L)).thenReturn(Optional.of(child));
        when(progressRepository.findByChildIdAndProblemSlug(eq(41L), any()))
                .thenAnswer(ignored -> Optional.ofNullable(storedProgress.get()));
        when(progressRepository.save(any(ChildMachineLearningProgress.class))).thenAnswer(invocation -> {
            storedProgress.set(invocation.getArgument(0));
            return invocation.getArgument(0);
        });
    }

    @Test
    void wrongAnswerAndRuntimeErrorProduceDeterministicScoresWithoutRewards() throws Exception {
        when(executor.execute(any(), any()))
                .thenReturn(new MachineLearningExecutor.ExecutionResult(
                        false, true, objectMapper.readTree("[13,10,17]"), "", ""
                ))
                .thenReturn(new MachineLearningExecutor.ExecutionResult(
                        false, false, null, "", "RuntimeError: boom"
                ));

        final JsonNode wrong = objectMapper.readTree(
                service.submit(41L, "easy-line-of-best-fit", "def solve(a, b): return [13, 10, 17]")
        );
        final JsonNode runtimeError = objectMapper.readTree(
                service.submit(41L, "easy-line-of-best-fit", "raise RuntimeError('boom')")
        );

        assertFalse(wrong.path("passed").asBoolean());
        assertEquals(83.33333333333333, wrong.path("score").asDouble(), 0.0001);
        assertEquals(1.6666666666666667, wrong.path("metricValue").asDouble(), 0.0001);
        assertFalse(runtimeError.path("passed").asBoolean());
        assertEquals(0.0, runtimeError.path("score").asDouble());
        assertTrue(runtimeError.path("error").asText().contains("RuntimeError"));
        assertEquals(2, storedProgress.get().getAttemptCount());
        assertEquals(0, child.getTotalPoints());
        assertFalse(storedProgress.get().getRewardGranted());
        verify(learningProfileService).recordLearningEvent(
                41L,
                "ml_problem_attempt",
                "ml:regression",
                0,
                "problem=easy-line-of-best-fit, difficulty=EASY, metric=1.6666666666666667"
        );
    }

    @Test
    void hiddenMultiCaseAccuracyUsesEveryCase() throws Exception {
        when(executor.execute(any(), any())).thenReturn(new MachineLearningExecutor.ExecutionResult(
                false, true, objectMapper.readTree("[0,1,0]"), "", ""
        ));

        final JsonNode result = objectMapper.readTree(
                service.submit(41L, "medium-logistic-gate", "def solve(a, b): return [0, 1, 0]")
        );

        assertFalse(result.path("passed").asBoolean());
        assertEquals(66.66666666666666, result.path("score").asDouble(), 0.0001);
        assertEquals(2.0 / 3.0, result.path("metricValue").asDouble(), 0.0001);
        assertEquals(0, child.getTotalPoints());
    }

    @Test
    void infrastructureFailureDoesNotCountAsLearnerAttempt() throws Exception {
        when(executor.execute(any(), any())).thenReturn(new MachineLearningExecutor.ExecutionResult(
                true, false, null, "", "runner unavailable"
        ));

        final JsonNode result = objectMapper.readTree(
                service.submit(41L, "easy-line-of-best-fit", "def solve(a, b): return []")
        );

        assertTrue(result.path("infrastructureError").asBoolean());
        assertEquals(0, result.path("attemptCount").asInt());
        assertEquals(0, child.getTotalPoints());
        assertNull(storedProgress.get());
        verify(progressRepository, never()).save(any());
        verify(learningProfileService, never()).recordLearningEvent(
                any(), any(), any(), any(Integer.class), any()
        );
    }
}
