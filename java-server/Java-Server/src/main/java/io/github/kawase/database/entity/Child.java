package io.github.kawase.database.entity;

import io.hypersistence.utils.hibernate.type.json.JsonType;
import jakarta.persistence.*;
import lombok.Data;
import org.hibernate.annotations.Type;
import java.time.LocalDate;
import java.util.Map;
import java.util.HashMap;
import java.util.List;

@Entity
@Table(name = "children")
@Data
public class Child {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "parent_id")
    private Parent parent;

    @Column(nullable = false)
    private String name;

    @Column(columnDefinition = "TEXT")
    private String profilePicture;

    @Type(JsonType.class)
    @Column(name = "game_stats", columnDefinition = "jsonb")
    private Map<String, Object> gameStats = new HashMap<>();

    @Column(name = "total_points", nullable = false)
    private Integer totalPoints = 0;

    @Column(name = "streak", nullable = false)
    private Integer streak = 0;

    @Column(name = "last_login_date")
    private LocalDate lastLoginDate;

    @OneToMany(mappedBy = "child", cascade = CascadeType.ALL, orphanRemoval = true)
    private List<CompletedTask> completedTasks;

    @OneToMany(mappedBy = "child", cascade = CascadeType.ALL, orphanRemoval = true)
    private List<Goal> goals;
}