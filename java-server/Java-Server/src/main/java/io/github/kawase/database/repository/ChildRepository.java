package io.github.kawase.database.repository;

import io.github.kawase.database.entity.Child;
import jakarta.persistence.LockModeType;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;

@Repository
public interface ChildRepository extends JpaRepository<Child, Long> {
    List<Child> findByParentId(final Long parentId);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("select child from Child child where child.id = :id")
    Optional<Child> findByIdForUpdate(@Param("id") final Long id);
}
