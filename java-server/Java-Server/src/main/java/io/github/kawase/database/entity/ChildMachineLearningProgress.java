package io.github.kawase.database.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import lombok.Data;

import java.time.Instant;

@Entity
@Table(name = "child_machine_learning_progress", uniqueConstraints = @UniqueConstraint(columnNames = { "child_id", "problem_slug" }))
@Data
public class ChildMachineLearningProgress {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "child_id", nullable = false)
    private Child child;

    @Column(name = "problem_slug", nullable = false, length = 96)
    private String problemSlug;

    @Column(name = "attempt_count", nullable = false)
    private Integer attemptCount;

    @Column(name = "best_score", nullable = false)
    private Double bestScore;

    @Column(name = "is_completed", nullable = false)
    private Boolean completed;

    @Column(name = "reward_granted", nullable = false)
    private Boolean rewardGranted;

    @Column(name = "completed_at")
    private Instant completedAt;

    @Column(name = "last_attempt_at")
    private Instant lastAttemptAt;
}
