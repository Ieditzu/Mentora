package io.github.kawase.database.services;

import io.github.kawase.database.entity.Child;
import io.github.kawase.database.entity.Goal;
import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.entity.Task;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.repository.GoalRepository;
import io.github.kawase.database.repository.ParentRepository;
import io.github.kawase.database.repository.TaskRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.ZonedDateTime;
import java.util.List;

@Service
@RequiredArgsConstructor
public class GoalService {

    private final GoalRepository goalRepository;
    private final ParentRepository parentRepository;
    private final ChildRepository childRepository;
    private final TaskRepository taskRepository;

    @Transactional
    public Goal createPointsGoal(final Long parentId, final Long childId, final String title, final String reward, final Integer requiredPoints) {
        final Parent parent = parentRepository.findById(parentId)
                .orElseThrow(() -> new RuntimeException("Parent not found"));
        final Child child = childRepository.findById(childId)
                .orElseThrow(() -> new RuntimeException("Child not found"));

        final Goal goal = new Goal();
        goal.setParent(parent);
        goal.setChild(child);
        goal.setTitle(title);
        goal.setReward(reward);
        goal.setRequiredPoints(requiredPoints);
        goal.setIsCompleted(false);

        return goalRepository.save(goal);
    }

    @Transactional
    public Goal createTaskGoal(final Long parentId, final Long childId, final String title, final String reward, final Long requiredTaskId) {
        final Parent parent = parentRepository.findById(parentId)
                .orElseThrow(() -> new RuntimeException("Parent not found"));
        final Child child = childRepository.findById(childId)
                .orElseThrow(() -> new RuntimeException("Child not found"));
        final Task task = taskRepository.findById(requiredTaskId)
                .orElseThrow(() -> new RuntimeException("Task not found"));

        final Goal goal = new Goal();
        goal.setParent(parent);
        goal.setChild(child);
        goal.setTitle(title);
        goal.setReward(reward);
        goal.setRequiredTask(task);
        goal.setIsCompleted(false);

        return goalRepository.save(goal);
    }

    @Transactional
    public void removeGoal(final Long goalId) {
        if (goalRepository.existsById(goalId)) {
            goalRepository.deleteById(goalId);
        } else {
            throw new RuntimeException("Goal not found with ID: " + goalId);
        }
    }

    @Transactional
    public void checkAndCompleteGoals(final Child child, final Task justCompletedTask) {
        final List<Goal> childGoals = child.getGoals();

        if (childGoals == null || childGoals.isEmpty()) {
            return;
        }

        for (final Goal goal : childGoals) {
            if (goal.getIsCompleted()) {
                continue;
            }

            boolean isGoalMet = false;

            if (goal.getRequiredPoints() != null && goal.getRequiredPoints() > 0) {
                if (child.getTotalPoints() >= goal.getRequiredPoints()) {
                    isGoalMet = true;
                }
            } else if (goal.getRequiredTask() != null) {
                if (goal.getRequiredTask().getId().equals(justCompletedTask.getId())) {
                    isGoalMet = true;
                }
            }

            if (isGoalMet) {
                goal.setIsCompleted(true);
                goal.setCompletedAt(ZonedDateTime.now());
                goalRepository.save(goal);
                
                System.out.println("Child " + child.getName() + " completed goal: " + goal.getTitle() + "! Reward unlocked: " + goal.getReward());
            }
        }
    }
}