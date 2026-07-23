package io.github.kawase.database.repository;

import io.github.kawase.database.entity.Parent;
import jakarta.persistence.LockModeType;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;
import java.util.Optional;

@Repository
public interface ParentRepository extends JpaRepository<Parent, Long> {
    Optional<Parent> findByEmail(final String email);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("select parent from Parent parent where parent.id = :parentId")
    Optional<Parent> findByIdForUpdate(@Param("parentId") final Long parentId);
}
