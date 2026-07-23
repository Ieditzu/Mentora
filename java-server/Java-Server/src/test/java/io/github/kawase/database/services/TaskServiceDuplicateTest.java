package io.github.kawase.database.services;

import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.Task;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.repository.CompletedTaskRepository;
import io.github.kawase.database.repository.TaskRepository;
import org.junit.jupiter.api.Test;

import java.util.Optional;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class TaskServiceDuplicateTest {
    private final TaskRepository taskRepository = mock(TaskRepository.class);
    private final ChildRepository childRepository = mock(ChildRepository.class);
    private final CompletedTaskRepository completedTaskRepository = mock(CompletedTaskRepository.class);
    private final GoalService goalService = mock(GoalService.class);
    private final TaskService service = new TaskService(
            taskRepository,
            childRepository,
            completedTaskRepository,
            goalService
    );

    @Test
    void repeatedCompletionIsIdempotent() {
        final Child child = new Child();
        child.setId(7L);
        child.setName("Ada");
        final Task task = new Task();
        task.setId(11L);
        task.setTitle("Loops");
        task.setPointValue(25);
        when(childRepository.findByIdForUpdate(7L)).thenReturn(Optional.of(child));
        when(taskRepository.findById(11L)).thenReturn(Optional.of(task));
        when(completedTaskRepository.existsByChildIdAndTaskId(7L, 11L)).thenReturn(false, true);

        service.completeTask(7L, 11L);
        service.completeTask(7L, 11L);

        assertEquals(25, child.getTotalPoints());
        assertEquals(1, child.getGameStats().get("tasks_completed"));
        verify(completedTaskRepository, times(1)).save(any());
        verify(childRepository, times(1)).save(child);
        verify(goalService, times(1)).checkAndCompleteGoals(child, task);
    }
}
