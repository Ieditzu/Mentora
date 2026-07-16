package io.github.kawase.machinelearning;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.ChildMachineLearningProgress;
import io.github.kawase.database.repository.ChildMachineLearningProgressRepository;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.services.LearningProfileService;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.Optional;
import java.util.concurrent.atomic.AtomicReference;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class MachineLearningServiceTest {
    private final ObjectMapper objectMapper = new ObjectMapper();
    private final MachineLearningCatalog catalog = new MachineLearningCatalog();
    private final MachineLearningExecutor executor = mock(MachineLearningExecutor.class);
    private final ChildRepository childRepository = mock(ChildRepository.class);
    private final ChildMachineLearningProgressRepository progressRepository = mock(ChildMachineLearningProgressRepository.class);
    private final LearningProfileService learningProfileService = mock(LearningProfileService.class);
    private final MachineLearningService service = new MachineLearningService(catalog, executor, childRepository, progressRepository, learningProfileService, objectMapper);

    @Test
    void catalogExposesNineSafeProblems() throws Exception {
        when(childRepository.existsById(7L)).thenReturn(true);
        when(progressRepository.findByChildId(7L)).thenReturn(List.of());

        final JsonNode response = objectMapper.readTree(service.buildCatalogJson(7L));

        assertEquals(9, response.path("problems").size());
        assertFalse(response.toString().contains("trainCsv"));
        assertFalse(response.toString().contains("expectedJson"));
        assertEquals(3, response.path("problems").findValuesAsText("difficulty").stream().filter("EASY"::equals).count());
        assertEquals(3, response.path("problems").findValuesAsText("difficulty").stream().filter("MEDIUM"::equals).count());
        assertEquals(3, response.path("problems").findValuesAsText("difficulty").stream().filter("HARD"::equals).count());
    }

    @Test
    void successfulProblemRewardsOnlyOnce() throws Exception {
        final Child child = new Child();
        child.setId(7L);
        child.setTotalPoints(0);
        final AtomicReference<ChildMachineLearningProgress> storedProgress = new AtomicReference<>();
        when(childRepository.findById(7L)).thenReturn(Optional.of(child));
        when(progressRepository.findByChildIdAndProblemSlug(7L, "easy-line-of-best-fit"))
                .thenAnswer(ignored -> Optional.ofNullable(storedProgress.get()));
        when(progressRepository.save(any(ChildMachineLearningProgress.class))).thenAnswer(invocation -> {
            storedProgress.set(invocation.getArgument(0));
            return invocation.getArgument(0);
        });
        when(executor.execute(any(), any())).thenReturn(new MachineLearningExecutor.ExecutionResult(
                false, true, objectMapper.readTree("[13,15,17]"), "", ""
        ));

        final JsonNode first = objectMapper.readTree(service.submit(7L, "easy-line-of-best-fit", "def solve(a, b): return []"));
        final JsonNode second = objectMapper.readTree(service.submit(7L, "easy-line-of-best-fit", "def solve(a, b): return []"));

        assertTrue(first.path("passed").asBoolean());
        assertTrue(first.path("rewardGranted").asBoolean());
        assertFalse(second.path("rewardGranted").asBoolean());
        assertEquals(20, child.getTotalPoints());
        assertEquals(2, storedProgress.get().getAttemptCount());
    }
}
