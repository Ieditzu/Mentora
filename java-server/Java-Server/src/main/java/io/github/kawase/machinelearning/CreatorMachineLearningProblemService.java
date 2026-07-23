package io.github.kawase.machinelearning;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.ChildMachineLearningProgress;
import io.github.kawase.database.entity.CreatorMachineLearningProblem;
import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.repository.ChildMachineLearningProgressRepository;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.repository.CreatorMachineLearningProblemRepository;
import io.github.kawase.database.repository.ParentRepository;
import io.github.kawase.database.services.LearningProfileService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

@Service
@RequiredArgsConstructor
public class CreatorMachineLearningProblemService {
    private final CreatorMachineLearningProblemRepository creatorProblemRepository;
    private final ParentRepository parentRepository;
    private final ChildRepository childRepository;
    private final ChildMachineLearningProgressRepository progressRepository;
    private final LearningProfileService learningProfileService;
    private final MachineLearningCatalog catalog;
    private final ObjectMapper objectMapper;

    @Transactional
    public CreatorProblemDto create(final Long parentId, final UpsertProblemRequest request) {
        final Parent parent = parentRepository.findById(parentId)
                .orElseThrow(() -> new RuntimeException("Parent not found"));
        if (request == null)
            throw new RuntimeException("Missing machine-learning problem payload");

        final String title = requireText(request.title(), "Title", 160);
        final String slug = (request.slug() == null || request.slug().isBlank() ? title : request.slug())
                .trim()
                .toLowerCase(Locale.ROOT)
                .replaceAll("[^a-z0-9]+", "-")
                .replaceAll("(^-+|-+$)", "");
        if (slug.isBlank() || slug.length() > 96)
            throw new RuntimeException("Slug must contain letters or numbers and be at most 96 characters");
        if (creatorProblemRepository.existsBySlug(slug)
                || catalog.getProblems().stream().anyMatch(problem -> problem.getSlug().equals(slug)))
            throw new RuntimeException("A machine-learning problem with this slug already exists");

        final String expectedJson = requireText(request.expectedJson(), "Expected JSON", 65_536);
        final JsonNode expectedResult;
        try {
            expectedResult = objectMapper.readTree(expectedJson);
            if (expectedResult == null)
                throw new RuntimeException("Expected JSON is required");
        } catch (JsonProcessingException exception) {
            throw new RuntimeException("Expected JSON is invalid", exception);
        }

        if (request.metricType() == null)
            throw new RuntimeException("Metric type is required");
        if (request.threshold() == null || !Double.isFinite(request.threshold()) || request.threshold() < 0.0)
            throw new RuntimeException("Threshold must be a finite non-negative number");
        validateMetricConfiguration(request.metricType(), expectedResult, request.threshold());
        if (request.rewardPoints() == null || request.rewardPoints() < 0 || request.rewardPoints() > 1_000)
            throw new RuntimeException("Reward points must be between 0 and 1000");

        final String difficulty = requireText(request.difficulty(), "Difficulty", 16).toUpperCase(Locale.ROOT);
        if (!List.of("EASY", "MEDIUM", "HARD").contains(difficulty))
            throw new RuntimeException("Difficulty must be EASY, MEDIUM, or HARD");

        final CreatorMachineLearningProblem problem = new CreatorMachineLearningProblem();
        problem.setParent(parent);
        problem.setSlug(slug);
        problem.setTitle(title);
        problem.setTitleRo(localizedText(request.titleRo(), title, 160));
        problem.setDescription(requireText(request.description(), "Description", 8_000));
        problem.setDescriptionRo(localizedText(request.descriptionRo(), problem.getDescription(), 8_000));
        problem.setHint(requireText(request.hint(), "Hint", 4_000));
        problem.setHintRo(localizedText(request.hintRo(), problem.getHint(), 4_000));
        problem.setDifficulty(difficulty);
        problem.setConcepts(normalizeList(request.concepts(), "Concepts"));
        problem.setStarterCode(requireText(request.starterCode(), "Starter code", 65_536));
        problem.setDatasetPreview(requireText(request.datasetPreview(), "Dataset preview", 65_536));
        problem.setDatasetColumns(normalizeList(request.datasetColumns(), "Dataset columns"));
        problem.setTrainCsv(requireText(request.trainCsv(), "Training CSV", 262_144));
        problem.setTestCsv(requireText(request.testCsv(), "Test CSV", 262_144));
        problem.setExpectedJson(expectedJson);
        problem.setMetricName(requireText(request.metricName(), "Metric name", 120));
        problem.setMetricType(request.metricType());
        problem.setThreshold(request.threshold());
        problem.setRewardPoints(request.rewardPoints());
        problem.setPublished(Boolean.TRUE.equals(request.published()));
        return toDto(creatorProblemRepository.saveAndFlush(problem));
    }

    @Transactional(readOnly = true)
    public List<CreatorProblemDto> getForParent(final Long parentId) {
        if (!parentRepository.existsById(parentId))
            throw new RuntimeException("Parent not found");
        return creatorProblemRepository.findByParentIdOrderByUpdatedAtDesc(parentId).stream()
                .map(this::toDto)
                .toList();
    }

    @Transactional(readOnly = true)
    public ChildMachineLearningProgressDto getChildProgress(final Long parentId, final Long childId) {
        final Child child = childRepository.findById(childId)
                .orElseThrow(() -> new RuntimeException("Child not found"));
        if (child.getParent() == null || !child.getParent().getId().equals(parentId))
            throw new RuntimeException("Access denied");

        final Map<String, MachineLearningProblem> problemsBySlug = new LinkedHashMap<>();
        for (final MachineLearningProblem problem : catalog.getProblems())
            problemsBySlug.put(problem.getSlug(), problem);

        final List<ChildProgressAttemptDto> attempts = progressRepository.findByChildId(childId).stream()
                .sorted(Comparator.comparing(
                        ChildMachineLearningProgress::getLastAttemptAt,
                        Comparator.nullsLast(Comparator.reverseOrder())
                ))
                .map(progress -> {
                    final MachineLearningProblem problem = problemsBySlug.get(progress.getProblemSlug());
                    return new ChildProgressAttemptDto(
                            progress.getProblemSlug(),
                            problem == null ? "Unknown problem" : problem.getTitle(),
                            progress.getLastFeedback() == null ? "" : progress.getLastFeedback(),
                            progress.getAttemptCount(),
                            progress.getLastScore() == null ? 0.0 : progress.getLastScore(),
                            progress.getBestScore(),
                            Boolean.TRUE.equals(progress.getCompleted()),
                            Boolean.TRUE.equals(progress.getRewardGranted()),
                            progress.getLastAttemptAt(),
                            progress.getCompletedAt()
                    );
                })
                .toList();
        return new ChildMachineLearningProgressDto(
                child.getId(),
                child.getName(),
                child.getTotalPoints(),
                learningProfileService.buildProfileSummary(childId, "machine_learning"),
                attempts
        );
    }

    private String requireText(final String value, final String label, final int maximumLength) {
        if (value == null || value.isBlank())
            throw new RuntimeException(label + " is required");
        if (value.length() > maximumLength)
            throw new RuntimeException(label + " exceeds " + maximumLength + " characters");
        return value.trim();
    }

    private String localizedText(final String value, final String fallback, final int maximumLength) {
        if (value == null || value.isBlank()) return fallback;
        if (value.length() > maximumLength)
            throw new RuntimeException("Localized text exceeds " + maximumLength + " characters");
        return value.trim();
    }

    private List<String> normalizeList(final List<String> values, final String label) {
        if (values == null || values.isEmpty())
            throw new RuntimeException(label + " must not be empty");
        final List<String> normalized = values.stream()
                .map(value -> requireText(value, label + " item", 120))
                .distinct()
                .toList();
        if (normalized.size() > 32)
            throw new RuntimeException(label + " must contain at most 32 values");
        return normalized;
    }

    private void validateMetricConfiguration(
            final MachineLearningProblem.MetricType metricType,
            final JsonNode expectedResult,
            final double threshold) {
        if (metricType == MachineLearningProblem.MetricType.EXACT)
            return;
        if (!expectedResult.isArray() || expectedResult.isEmpty())
            throw new RuntimeException(metricType + " requires a non-empty expected JSON array");
        if (metricType == MachineLearningProblem.MetricType.MAE) {
            for (final JsonNode value : expectedResult) {
                if (!value.isNumber() || !Double.isFinite(value.asDouble()))
                    throw new RuntimeException("MAE expected values must all be finite numbers");
            }
            return;
        }
        if (threshold > 1.0)
            throw new RuntimeException("ACCURACY threshold must be between 0 and 1");
    }

    private CreatorProblemDto toDto(final CreatorMachineLearningProblem problem) {
        return new CreatorProblemDto(
                problem.getId(),
                problem.getParent().getId(),
                problem.getSlug(),
                problem.getTitle(),
                problem.getTitleRo(),
                problem.getDescription(),
                problem.getDescriptionRo(),
                problem.getHint(),
                problem.getHintRo(),
                problem.getDifficulty(),
                List.copyOf(problem.getConcepts()),
                problem.getStarterCode(),
                problem.getDatasetPreview(),
                List.copyOf(problem.getDatasetColumns()),
                problem.getMetricName(),
                problem.getMetricType(),
                problem.getThreshold(),
                problem.getRewardPoints(),
                Boolean.TRUE.equals(problem.getPublished()),
                problem.getCreatedAt(),
                problem.getUpdatedAt()
        );
    }

    public record UpsertProblemRequest(
            String slug, String title, String titleRo,
            String description, String descriptionRo,
            String hint, String hintRo, String difficulty,
            List<String> concepts,
            String starterCode, String datasetPreview,
            List<String> datasetColumns,
            String trainCsv, String testCsv, String expectedJson, String metricName,
            MachineLearningProblem.MetricType metricType,
            Double threshold,
            Integer rewardPoints,
            Boolean published) {
        /* w */
    }

    public record CreatorProblemDto(
            Long id, Long parentId,
            String slug, String title, String titleRo,
            String description, String descriptionRo,
            String hint, String hintRo, String difficulty,
            List<String> concepts,
            String starterCode, String datasetPreview,
            List<String> datasetColumns,
            String metricName,
            MachineLearningProblem.MetricType metricType,
            double threshold,
            int rewardPoints,
            boolean published,
            Instant createdAt, Instant updatedAt) {
        /* w */
    }

    public record ChildMachineLearningProgressDto(
            Long childId,
            String childName,
            int totalPoints,
            String profileSummary,
            List<ChildProgressAttemptDto> attempts) {
        /* w */
    }

    public record ChildProgressAttemptDto(
            String problemSlug, String problemTitle, String lastFeedback,
            int attemptCount,
            double lastScore, double bestScore,
            boolean completed, boolean rewardGranted,
            Instant lastAttemptAt, Instant completedAt) {
        /* w */
    }
}
