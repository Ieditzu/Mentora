package io.github.kawase.machinelearning;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.ChildMachineLearningProgress;
import io.github.kawase.database.repository.ChildMachineLearningProgressRepository;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.services.LearningProfileService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

@Service
@RequiredArgsConstructor
public class MachineLearningService {
    private final MachineLearningCatalog catalog;
    private final MachineLearningExecutor executor;
    private final ChildRepository childRepository;
    private final ChildMachineLearningProgressRepository progressRepository;
    private final LearningProfileService learningProfileService;
    private final ObjectMapper objectMapper;

    @Transactional(readOnly = true)
    public String buildCatalogJson(final Long childId) {
        if (childId == null) throw new RuntimeException("Not logged in as a child.");
        if (!childRepository.existsById(childId)) throw new RuntimeException("Child not found.");

        final Map<String, ChildMachineLearningProgress> progressBySlug = new HashMap<>();
        for (final ChildMachineLearningProgress progress : progressRepository.findByChildId(childId))
            progressBySlug.put(progress.getProblemSlug(), progress);

        final ArrayNode problems = objectMapper.createArrayNode();
        for (final MachineLearningProblem problem : catalog.getProblems()) {
            final ChildMachineLearningProgress progress = progressBySlug.get(problem.getSlug());
            final ObjectNode item = problems.addObject();
            item.put("slug", problem.getSlug());
            item.put("title", problem.getTitle());
            item.put("titleRo", problem.getTitleRo());
            item.put("description", problem.getDescription());
            item.put("descriptionRo", problem.getDescriptionRo());
            item.put("hint", problem.getHint());
            item.put("hintRo", problem.getHintRo());
            item.put("difficulty", problem.getDifficulty());
            item.set("concepts", objectMapper.valueToTree(problem.getConcepts()));
            item.put("starterCode", problem.getStarterCode());
            item.put("datasetPreview", problem.getDatasetPreview());
            item.set("datasetColumns", objectMapper.valueToTree(problem.getDatasetColumns()));
            item.put("rewardPoints", problem.getRewardPoints());
            item.put("attemptCount", progress == null ? 0 : progress.getAttemptCount());
            item.put("bestScore", progress == null ? 0.0 : progress.getBestScore());
            item.put("completed", progress != null && Boolean.TRUE.equals(progress.getCompleted()));
        }

        final ObjectNode root = objectMapper.createObjectNode();
        root.set("problems", problems);
        return writeJson(root);
    }

    @Transactional
    public synchronized String submit(final Long childId, final String problemSlug, final String sourceCode) {
        if (childId == null) throw new RuntimeException("Not logged in as a child.");
        final Child child = childRepository.findById(childId).orElseThrow(() -> new RuntimeException("Child not found."));
        final MachineLearningProblem problem = catalog.requireProblem(problemSlug == null ? "" : problemSlug.trim());
        final MachineLearningExecutor.ExecutionResult execution = executor.execute(problem, sourceCode);

        if (execution.infrastructureError())
            return writeJson(buildInfrastructureResult(
                    problem,
                    child,
                    progressRepository.findByChildIdAndProblemSlug(childId, problem.getSlug()).orElse(null),
                    execution.error()
            ));

        final Grade grade = execution.success() ? grade(problem, execution.result()) : new Grade(false, 0.0, 0.0);
        final ChildMachineLearningProgress progress = progressRepository.findByChildIdAndProblemSlug(childId, problem.getSlug())
                .orElseGet(() -> {
                    final ChildMachineLearningProgress created = new ChildMachineLearningProgress();
                    created.setChild(child);
                    created.setProblemSlug(problem.getSlug());
                    created.setAttemptCount(0);
                    created.setBestScore(0.0);
                    created.setCompleted(false);
                    created.setRewardGranted(false);
                    return created;
                });

        progress.setAttemptCount(progress.getAttemptCount() + 1);
        progress.setBestScore(Math.max(progress.getBestScore(), grade.score()));
        progress.setLastScore(grade.score());
        progress.setLastAttemptAt(Instant.now());
        boolean rewardGranted = false;
        if (grade.passed()) {
            progress.setCompleted(true);
            if (progress.getCompletedAt() == null)
                progress.setCompletedAt(Instant.now());
            if (!Boolean.TRUE.equals(progress.getRewardGranted())) {
                child.setTotalPoints(child.getTotalPoints() + problem.getRewardPoints());
                progress.setRewardGranted(true);
                rewardGranted = true;
                childRepository.save(child);
            }
        }
        progressRepository.save(progress);

        learningProfileService.recordLearningEvent(
                childId,
                "ml_problem_attempt",
                problem.getConcepts().getFirst(),
                grade.passed() ? 1 : 0,
                "problem=" + problem.getSlug() + ", difficulty=" + problem.getDifficulty() + ", metric=" + grade.metricValue()
        );

        final String feedback = execution.success()
                ? grade.passed() ? "Correct — the hidden evaluation passed." : "Not yet — improve the result against the hidden evaluation data."
                : "The solution could not be evaluated because it raised an error.";
        progress.setLastFeedback(feedback);
        progressRepository.save(progress);

        final ObjectNode result = buildBaseResult(problem, child);
        result.put("passed", grade.passed());
        result.put("score", grade.score());
        result.put("metricName", problem.getMetricName());
        result.put("metricValue", grade.metricValue());
        result.put("threshold", problem.getThreshold());
        result.put("feedback", feedback);
        result.put("stdout", execution.stdout());
        result.put("error", execution.error());
        result.put("attemptCount", progress.getAttemptCount());
        result.put("bestScore", progress.getBestScore());
        result.put("completed", progress.getCompleted());
        result.put("rewardGranted", rewardGranted);
        result.put("totalPoints", child.getTotalPoints());
        return writeJson(result);
    }

    private Grade grade(final MachineLearningProblem problem, final JsonNode actual) {
        try {
            final JsonNode expected = objectMapper.readTree(problem.getExpectedJson());
            return switch (problem.getMetricType()) {
                case EXACT -> exactGrade(expected, actual);
                case MAE -> maeGrade(expected, actual, problem.getThreshold());
                case ACCURACY -> accuracyGrade(expected, actual, problem.getThreshold());
            };
        } catch (JsonProcessingException exception) {
            throw new IllegalStateException("Invalid expected result for " + problem.getSlug(), exception);
        }
    }

    private Grade exactGrade(final JsonNode expected, final JsonNode actual) {
        final boolean matches = equivalent(expected, actual);
        return new Grade(matches, matches ? 100.0 : 0.0, matches ? 1.0 : 0.0);
    }

    private Grade maeGrade(final JsonNode expected, final JsonNode actual, final double threshold) {
        if (expected == null || !expected.isArray() || expected.isEmpty()
                || actual == null || !actual.isArray() || actual.size() != expected.size())
            return new Grade(false, 0.0, Double.POSITIVE_INFINITY);
        double totalError = 0.0;
        for (int index = 0; index < expected.size(); index++) {
            if (!expected.get(index).isNumber() || !actual.get(index).isNumber())
                return new Grade(false, 0.0, Double.POSITIVE_INFINITY);
            final double expectedValue = expected.get(index).asDouble();
            final double actualValue = actual.get(index).asDouble();
            if (!Double.isFinite(expectedValue) || !Double.isFinite(actualValue))
                return new Grade(false, 0.0, Double.POSITIVE_INFINITY);
            final double absoluteError = Math.abs(expectedValue - actualValue);
            if (!Double.isFinite(absoluteError))
                return new Grade(false, 0.0, Double.POSITIVE_INFINITY);
            totalError += absoluteError;
        }
        final double meanAbsoluteError = totalError / expected.size();
        return new Grade(meanAbsoluteError <= threshold, Math.max(0.0, 100.0 - meanAbsoluteError * 10.0), meanAbsoluteError);
    }

    private Grade accuracyGrade(final JsonNode expected, final JsonNode actual, final double threshold) {
        if (expected == null || !expected.isArray() || expected.isEmpty()
                || actual == null || !actual.isArray() || actual.size() != expected.size())
            return new Grade(false, 0.0, 0.0);
        int correct = 0;
        for (int index = 0; index < expected.size(); index++) {
            if (equivalent(expected.get(index), actual.get(index)))
                correct++;
        }
        final double accuracy = (double) correct / expected.size();
        return new Grade(accuracy >= threshold, accuracy * 100.0, accuracy);
    }

    private boolean equivalent(final JsonNode expected, final JsonNode actual) {
        if (expected == null || actual == null) return expected == actual;
        if (expected.isNumber() && actual.isNumber()) return Math.abs(expected.asDouble() - actual.asDouble()) <= 0.01;
        if (expected.isObject() && actual.isObject()) {
            final var fields = expected.fields();
            while (fields.hasNext()) {
                final Map.Entry<String, JsonNode> field = fields.next();
                if (!actual.has(field.getKey()) || !equivalent(field.getValue(), actual.get(field.getKey()))) return false;
            }
            return expected.size() == actual.size();
        }
        if (expected.isArray() && actual.isArray()) {
            if (expected.size() != actual.size()) return false;
            for (int index = 0; index < expected.size(); index++) {
                if (!equivalent(expected.get(index), actual.get(index))) return false;
            }
            return true;
        }
        return expected.asText().equals(actual.asText());
    }

    private ObjectNode buildInfrastructureResult(final MachineLearningProblem problem, final Child child, final ChildMachineLearningProgress progress, final String error) {
        final ObjectNode result = buildBaseResult(problem, child);
        result.put("infrastructureError", true);
        result.put("passed", false);
        result.put("score", 0.0);
        result.put("metricName", problem.getMetricName());
        result.put("metricValue", 0.0);
        result.put("threshold", problem.getThreshold());
        result.put("feedback", "The secure runner is temporarily unavailable; this was not counted as an attempt.");
        result.put("stdout", "");
        result.put("error", error);
        result.put("attemptCount", progress == null ? 0 : progress.getAttemptCount());
        result.put("bestScore", progress == null ? 0.0 : progress.getBestScore());
        result.put("completed", progress != null && Boolean.TRUE.equals(progress.getCompleted()));
        result.put("rewardGranted", false);
        return result;
    }

    private ObjectNode buildBaseResult(final MachineLearningProblem problem, final Child child) {
        final ObjectNode result = objectMapper.createObjectNode();
        result.put("problemSlug", problem.getSlug());
        result.put("rewardPoints", problem.getRewardPoints());
        result.put("totalPoints", child.getTotalPoints());
        result.put("infrastructureError", false);
        return result;
    }

    private String writeJson(final JsonNode node) {
        try {
            return objectMapper.writeValueAsString(node);
        } catch (JsonProcessingException exception) {
            throw new RuntimeException("Could not serialize machine-learning response", exception);
        }
    }

    private record Grade(boolean passed, double score, double metricValue) {
        /* w */
    }
}
