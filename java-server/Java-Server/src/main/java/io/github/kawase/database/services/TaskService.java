package io.github.kawase.database.services;

import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.CompletedTask;
import io.github.kawase.database.entity.Task;
import io.github.kawase.database.entity.enums.DefaultTaskType;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.repository.CompletedTaskRepository;
import io.github.kawase.database.repository.TaskRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.context.annotation.Lazy;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.ZonedDateTime;
import java.util.ArrayList;
import java.util.List;

@Service
@RequiredArgsConstructor
public class TaskService {

    private final TaskRepository taskRepository;
    private final ChildRepository childRepository;
    private final CompletedTaskRepository completedTaskRepository;
    
    @Lazy
    private final GoalService goalService;

    @Transactional
    public void initializeGlobalTasks() {
        if (taskRepository.count() > 0) return;

        final List<Task> defaultTasks = new ArrayList<>();
        for (final DefaultTaskType defaultTask : DefaultTaskType.values()) {
            final Task task = new Task();
            task.setTitle(defaultTask.getTitle());
            task.setPointValue(defaultTask.getPointValue());
            defaultTasks.add(task);
        }
        taskRepository.saveAll(defaultTasks);
    }

    @Transactional(readOnly = true)
    public List<Task> getAllTasks() {
        return taskRepository.findAll();
    }

    @Transactional
    public void completeTask(final Long childId, final Long taskId) {
        final Child child = childRepository.findById(childId)
                .orElseThrow(() -> new RuntimeException("Child not found with ID: " + childId));
                
        final Task task = taskRepository.findById(taskId)
                .orElseThrow(() -> new RuntimeException("Task not found with ID: " + taskId));

        final CompletedTask completedTask = new CompletedTask();
        completedTask.setChild(child);
        completedTask.setTask(task);
        completedTask.setCompletedAt(ZonedDateTime.now());
        completedTaskRepository.save(completedTask);

        child.setTotalPoints(child.getTotalPoints() + task.getPointValue());
        
        final var data = child.getGameStats();
        final int totalTasks = (int) data.getOrDefault("tasks_completed", 0);
        data.put("tasks_completed", totalTasks + 1);

        childRepository.save(child);
        
        if (goalService != null) {
            goalService.checkAndCompleteGoals(child, task);
        }
    }
}
