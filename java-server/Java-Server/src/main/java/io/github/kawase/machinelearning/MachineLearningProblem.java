package io.github.kawase.machinelearning;

import lombok.Builder;
import lombok.Value;

import java.util.List;

@Value
@Builder
public class MachineLearningProblem {
    String slug, title, titleRo, description, descriptionRo, hint, hintRo, difficulty, starterCode, datasetPreview, trainCsv, testCsv, expectedJson, metricName;
    List<String> concepts, datasetColumns;
    int rewardPoints;
    double threshold;
    MetricType metricType;

    public enum MetricType {
        EXACT,
        MAE,
        ACCURACY
    }
}
