package io.github.kawase.database.entity;

import jakarta.persistence.*;
import lombok.Data;

@Entity
@Table(name = "tasks")
@Data
public class Task {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private String title;

    @Column(name = "point_value", nullable = false)
    private Integer pointValue;

    @Column(columnDefinition = "TEXT")
    private String description;

    @Column(name = "code_template", columnDefinition = "TEXT")
    private String codeTemplate;

    @Column(name = "is_ai_generated", nullable = false, columnDefinition = "boolean default false")
    private boolean aiGenerated = false;
}