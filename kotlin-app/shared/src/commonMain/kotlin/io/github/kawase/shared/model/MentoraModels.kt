package io.github.kawase.shared.model

data class Child(
    val id: Long,
    val name: String,
    val points: Int,
    val isOnline: Boolean,
    val profilePicture: String? = null
)

data class Task(val id: Long, val name: String, val points: Int)

data class Goal(
    val id: Long,
    val title: String,
    val reward: String,
    val isCompleted: Boolean,
    val requiredPoints: Int
)

data class CompletedTask(
    val id: Long,
    val taskTitle: String,
    val pointValue: Int,
    val completedAt: String
)

data class WeeklyReport(
    val childId: Long,
    val childName: String,
    val weekStart: String,
    val weekEnd: String,
    val reportText: String,
    val isAiGenerated: Boolean
)

data class LiveSessionState(
    val childId: Long,
    val childName: String,
    val isOnline: Boolean,
    val padName: String,
    val codeText: String,
    val attemptCount: Int,
    val hasRequestedHint: Boolean,
    val status: String,
    val updatedAt: String
)
