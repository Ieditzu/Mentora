package io.github.kawase.database.entity;

import jakarta.persistence.*;
import lombok.Data;
import java.time.ZonedDateTime;

@Entity
@Table(name = "goals")
@Data
public class Goal {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "parent_id", nullable = false)
    private Parent parent;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "child_id", nullable = false)
    private Child child;

    @Column(nullable = false)
    private String title;

    @Column(nullable = false)
    private String reward;

    @Column(name = "required_points")
    private Integer requiredPoints;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "required_task_id")
    private Task requiredTask;

    @Column(name = "is_completed", nullable = false)
    private Boolean isCompleted = false;

    @Column(name = "completed_at")
    private ZonedDateTime completedAt;
}