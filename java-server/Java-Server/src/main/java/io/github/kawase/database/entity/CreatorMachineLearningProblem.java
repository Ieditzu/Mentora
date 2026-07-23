package io.github.kawase.database.entity;

import io.github.kawase.machinelearning.MachineLearningProblem;
import io.hypersistence.utils.hibernate.type.json.JsonType;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.PrePersist;
import jakarta.persistence.PreUpdate;
import jakarta.persistence.Table;
import lombok.Data;
import org.hibernate.annotations.Type;

import java.time.Instant;
import java.util.ArrayList;
import java.util.List;

@Entity
@Table(name = "creator_machine_learning_problems")
@Data
public class CreatorMachineLearningProblem {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "parent_id", nullable = false)
    private Parent parent;

    @Column(nullable = false, unique = true, length = 96)
    private String slug;

    @Column(nullable = false, length = 160)
    private String title;

    @Column(name = "title_ro", nullable = false, length = 160)
    private String titleRo;

    @Column(nullable = false, columnDefinition = "TEXT")
    private String description;

    @Column(name = "description_ro", nullable = false, columnDefinition = "TEXT")
    private String descriptionRo;

    @Column(nullable = false, columnDefinition = "TEXT")
    private String hint;

    @Column(name = "hint_ro", nullable = false, columnDefinition = "TEXT")
    private String hintRo;

    @Column(nullable = false, length = 16)
    private String difficulty;

    @Type(JsonType.class)
    @Column(nullable = false, columnDefinition = "jsonb")
    private List<String> concepts = new ArrayList<>();

    @Column(name = "starter_code", nullable = false, columnDefinition = "TEXT")
    private String starterCode;

    @Column(name = "dataset_preview", nullable = false, columnDefinition = "TEXT")
    private String datasetPreview;

    @Type(JsonType.class)
    @Column(name = "dataset_columns", nullable = false, columnDefinition = "jsonb")
    private List<String> datasetColumns = new ArrayList<>();

    @Column(name = "train_csv", nullable = false, columnDefinition = "TEXT")
    private String trainCsv;

    @Column(name = "test_csv", nullable = false, columnDefinition = "TEXT")
    private String testCsv;

    @Column(name = "expected_json", nullable = false, columnDefinition = "TEXT")
    private String expectedJson;

    @Column(name = "metric_name", nullable = false, length = 120)
    private String metricName;

    @Enumerated(EnumType.STRING)
    @Column(name = "metric_type", nullable = false, length = 16)
    private MachineLearningProblem.MetricType metricType;

    @Column(nullable = false)
    private Double threshold;

    @Column(name = "reward_points", nullable = false)
    private Integer rewardPoints;

    @Column(name = "is_published", nullable = false)
    private Boolean published;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;

    @PrePersist
    public void onCreate() {
        final Instant now = Instant.now();
        createdAt = now;
        updatedAt = now;
    }

    @PreUpdate
    public void onUpdate() {
        updatedAt = Instant.now();
    }
}
