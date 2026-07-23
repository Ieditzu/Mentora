package io.github.kawase.machinelearning;

import com.fasterxml.jackson.databind.ObjectMapper;
import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.repository.ChildMachineLearningProgressRepository;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.repository.CreatorMachineLearningProblemRepository;
import io.github.kawase.database.repository.ParentRepository;
import io.github.kawase.database.services.LearningProfileService;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.Optional;

import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class CreatorMachineLearningProblemServiceTest {
    private final CreatorMachineLearningProblemRepository problemRepository =
            mock(CreatorMachineLearningProblemRepository.class);
    private final ParentRepository parentRepository = mock(ParentRepository.class);
    private final CreatorMachineLearningProblemService service = new CreatorMachineLearningProblemService(
            problemRepository,
            parentRepository,
            mock(ChildRepository.class),
            mock(ChildMachineLearningProgressRepository.class),
            mock(LearningProfileService.class),
            new MachineLearningCatalog(),
            new ObjectMapper()
    );

    @Test
    void maeRequiresANonEmptyNumericExpectedArray() {
        prepareParent();

        assertThrows(RuntimeException.class, () -> service.create(
                1L,
                request(MachineLearningProblem.MetricType.MAE, "{}", 0.1)
        ));
        assertThrows(RuntimeException.class, () -> service.create(
                1L,
                request(MachineLearningProblem.MetricType.MAE, "[]", 0.1)
        ));
        assertThrows(RuntimeException.class, () -> service.create(
                1L,
                request(MachineLearningProblem.MetricType.MAE, "[1,\"two\"]", 0.1)
        ));
        assertThrows(RuntimeException.class, () -> service.create(
                1L,
                request(MachineLearningProblem.MetricType.MAE, "[1e9999]", 0.1)
        ));
    }

    @Test
    void accuracyRequiresANonEmptyArrayAndUnitIntervalThreshold() {
        prepareParent();

        assertThrows(RuntimeException.class, () -> service.create(
                1L,
                request(MachineLearningProblem.MetricType.ACCURACY, "[]", 0.75)
        ));
        assertThrows(RuntimeException.class, () -> service.create(
                1L,
                request(MachineLearningProblem.MetricType.ACCURACY, "[0,1]", 1.01)
        ));
    }

    private void prepareParent() {
        final Parent parent = new Parent();
        parent.setId(1L);
        when(parentRepository.findById(1L)).thenReturn(Optional.of(parent));
        when(problemRepository.existsBySlug("metric-validation")).thenReturn(false);
    }

    private CreatorMachineLearningProblemService.UpsertProblemRequest request(
            final MachineLearningProblem.MetricType metricType,
            final String expectedJson,
            final double threshold) {
        return new CreatorMachineLearningProblemService.UpsertProblemRequest(
                "metric-validation",
                "Metric validation",
                "",
                "Validate a creator-authored metric configuration.",
                "",
                "Return the expected values.",
                "",
                "EASY",
                List.of("ml:evaluation"),
                "def solve(train_path, test_path):\n    return []\n",
                "x,y\n0,1",
                List.of("x", "y"),
                "x,y\n0,1",
                "x\n1",
                expectedJson,
                "test metric",
                metricType,
                threshold,
                10,
                true
        );
    }
}
