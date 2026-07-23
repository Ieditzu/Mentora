package io.github.kawase.database.entity;

import jakarta.persistence.CascadeType;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.OneToMany;
import jakarta.persistence.Table;
import lombok.Data;
import lombok.ToString;

import java.util.List;

@Entity
@Table(name = "parents")
@Data
public class Parent {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, unique = true)
    private String email;

    @Column(name = "password_hash", nullable = false)
    private String passwordHash;

    @Column(name = "totp_secret_encrypted", columnDefinition = "TEXT")
    private String totpSecretEncrypted;

    @Column(name = "totp_enabled", nullable = false, columnDefinition = "boolean default false")
    private Boolean totpEnabled = false;

    @Column(name = "totp_last_accepted_step")
    private Long totpLastAcceptedStep;

    @Column(name = "totp_recovery_code_hashes", columnDefinition = "TEXT")
    private String totpRecoveryCodeHashes;

    @Column(columnDefinition = "TEXT")
    private String profilePicture;

    @ToString.Exclude
    @OneToMany(mappedBy = "parent", cascade = CascadeType.ALL, orphanRemoval = true)
    private List<Child> childEntities;

    @ToString.Exclude
    @OneToMany(mappedBy = "parent", cascade = CascadeType.ALL, orphanRemoval = true)
    private List<Goal> goals;
}
