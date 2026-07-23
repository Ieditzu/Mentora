package io.github.kawase.shared.protocol

/** Unencrypted Mentora wire packet. [PacketFactory] adds and consumes the leading ID. */
sealed class MentoraPacket {
    abstract val id: Int
    internal abstract fun writeBody(cursor: ByteCursor)
}

data class HandshakePacket(
    val clientFingerprint: String,
    val protocolVersion: Int = 1,
    val deviceId: String = ""
) : MentoraPacket() {
    override val id = 1
    override fun writeBody(cursor: ByteCursor) {
        cursor.writeString(clientFingerprint)
        if (protocolVersion >= 2 || deviceId.isNotEmpty()) {
            cursor.writeInt(protocolVersion)
            cursor.writeString(deviceId)
        }
    }
}

data class AuthPacket(val emailHash: String, val passwordHash: String) : MentoraPacket() {
    override val id = 2
    override fun writeBody(cursor: ByteCursor) { cursor.writeString(emailHash); cursor.writeString(passwordHash) }
}

data class RegisterParentPacket(val email: String, val passwordHash: String) : MentoraPacket() {
    override val id = 3
    override fun writeBody(cursor: ByteCursor) { cursor.writeString(email); cursor.writeString(passwordHash) }
}

data class AddChildPacket(val childName: String) : MentoraPacket() {
    override val id = 4
    override fun writeBody(cursor: ByteCursor) = cursor.writeString(childName)
}

data class AddGoalPacket(
    val childId: Long, val title: String, val reward: String, val requiredPoints: Int, val requiredTaskId: Long
) : MentoraPacket() {
    override val id = 5
    override fun writeBody(cursor: ByteCursor) { cursor.writeLong(childId); cursor.writeString(title); cursor.writeString(reward); cursor.writeInt(requiredPoints); cursor.writeLong(requiredTaskId) }
}

data class CompleteTaskPacket(val childId: Long, val taskId: Long) : MentoraPacket() {
    override val id = 8
    override fun writeBody(cursor: ByteCursor) { cursor.writeLong(childId); cursor.writeLong(taskId) }
}

data class ActionResponsePacket(val requestPacketId: Int, val success: Boolean, val message: String, val resultId: Long) : MentoraPacket() {
    override val id = 9
    override fun writeBody(cursor: ByteCursor) { cursor.writeInt(requestPacketId); cursor.writeBoolean(success); cursor.writeString(message); cursor.writeLong(resultId) }
}

data class AuthResponsePacket(val success: Boolean, val parentId: Long, val message: String, val parentPfp: String) : MentoraPacket() {
    override val id = 10
    override fun writeBody(cursor: ByteCursor) { cursor.writeBoolean(success); cursor.writeLong(parentId); cursor.writeString(message); cursor.writeString(parentPfp) }
}

class FetchTasksPacket : MentoraPacket() { override val id = 11; override fun writeBody(cursor: ByteCursor) = Unit }
data class FetchTasksResponsePacket(val tasks: List<TaskPayload>) : MentoraPacket() {
    override val id = 12
    override fun writeBody(cursor: ByteCursor) { cursor.writeInt(tasks.size); tasks.forEach { cursor.writeLong(it.id); cursor.writeString(it.title); cursor.writeInt(it.pointValue) } }
}
data class TaskPayload(val id: Long, val title: String, val pointValue: Int)

data class FetchGoalsPacket(val childId: Long) : MentoraPacket() { override val id = 13; override fun writeBody(cursor: ByteCursor) = cursor.writeLong(childId) }
data class FetchGoalsResponsePacket(val goals: List<GoalPayload>) : MentoraPacket() {
    override val id = 14
    override fun writeBody(cursor: ByteCursor) { cursor.writeInt(goals.size); goals.forEach { cursor.writeLong(it.id); cursor.writeString(it.title); cursor.writeString(it.reward); cursor.writeBoolean(it.isCompleted); cursor.writeInt(it.requiredPoints); cursor.writeLong(it.requiredTaskId) } }
}
data class GoalPayload(val id: Long, val title: String, val reward: String, val isCompleted: Boolean, val requiredPoints: Int, val requiredTaskId: Long)

class FetchChildrenPacket : MentoraPacket() { override val id = 15; override fun writeBody(cursor: ByteCursor) = Unit }
data class FetchChildrenResponsePacket(val children: List<ChildPayload>) : MentoraPacket() {
    override val id = 16
    override fun writeBody(cursor: ByteCursor) { cursor.writeInt(children.size); children.forEach { cursor.writeLong(it.id); cursor.writeString(it.name); cursor.writeInt(it.totalPoints); cursor.writeBoolean(it.isOnline); cursor.writeString(it.pfp) } }
}
data class ChildPayload(val id: Long, val name: String, val totalPoints: Int, val isOnline: Boolean, val pfp: String)

data class FetchCompletedTasksPacket(val childId: Long) : MentoraPacket() { override val id = 17; override fun writeBody(cursor: ByteCursor) = cursor.writeLong(childId) }
data class FetchCompletedTasksResponsePacket(val completedTasks: List<CompletedTaskPayload>) : MentoraPacket() {
    override val id = 18
    override fun writeBody(cursor: ByteCursor) { cursor.writeInt(completedTasks.size); completedTasks.forEach { cursor.writeLong(it.id); cursor.writeString(it.taskTitle); cursor.writeInt(it.pointValue); cursor.writeString(it.completedAt) } }
}
data class CompletedTaskPayload(val id: Long, val taskTitle: String, val pointValue: Int, val completedAt: String)

data class ClaimQRLoginPacket(val token: String, val childId: Long) : MentoraPacket() { override val id = 21; override fun writeBody(cursor: ByteCursor) { cursor.writeString(token); cursor.writeLong(childId) } }
data class FetchChildStatsResponsePacket(val name: String, val totalPoints: Int, val gameStatsJson: String, val streak: Int, val completedTaskCount: Int, val totalTaskCount: Int) : MentoraPacket() {
    override val id = 24
    override fun writeBody(cursor: ByteCursor) { cursor.writeString(name); cursor.writeInt(totalPoints); cursor.writeString(gameStatsJson); cursor.writeInt(streak); cursor.writeInt(completedTaskCount); cursor.writeInt(totalTaskCount) }
}
data class UpdatePfpPacket(val childId: Long, val base64Pfp: String) : MentoraPacket() { override val id = 26; override fun writeBody(cursor: ByteCursor) { cursor.writeLong(childId); cursor.writeString(base64Pfp) } }
data class RemoveChildPacket(val childId: Long) : MentoraPacket() { override val id = 27; override fun writeBody(cursor: ByteCursor) = cursor.writeLong(childId) }
data class AskAiPacket(val question: String, val context: String) : MentoraPacket() { override val id = 30; override fun writeBody(cursor: ByteCursor) { cursor.writeString(question); cursor.writeString(context) } }
data class AiResponsePacket(val response: String) : MentoraPacket() { override val id = 31; override fun writeBody(cursor: ByteCursor) = cursor.writeString(response) }
data class FetchChildStatsByParentPacket(val childId: Long) : MentoraPacket() { override val id = 32; override fun writeBody(cursor: ByteCursor) = cursor.writeLong(childId) }

data class SubscribeLiveSessionPacket(val childId: Long, val subscribe: Boolean) : MentoraPacket() { override val id = 64; override fun writeBody(cursor: ByteCursor) { cursor.writeLong(childId); cursor.writeBoolean(subscribe) } }
data class LiveSessionUpdatePacket(val childId: Long, val childName: String, val online: Boolean, val padName: String, val codeText: String, val attemptCount: Int, val hintRequested: Boolean, val status: String, val updatedAt: String) : MentoraPacket() {
    override val id = 65
    override fun writeBody(cursor: ByteCursor) { cursor.writeLong(childId); cursor.writeString(childName); cursor.writeBoolean(online); cursor.writeString(padName); cursor.writeString(codeText); cursor.writeInt(attemptCount); cursor.writeBoolean(hintRequested); cursor.writeString(status); cursor.writeString(updatedAt) }
}
data class SendParentChallengePacket(val childId: Long, val message: String) : MentoraPacket() { override val id = 66; override fun writeBody(cursor: ByteCursor) { cursor.writeLong(childId); cursor.writeString(message) } }
data class ParentChallengePacket(val challengeId: String, val childId: Long, val message: String, val sentAt: String) : MentoraPacket() { override val id = 67; override fun writeBody(cursor: ByteCursor) { cursor.writeString(challengeId); cursor.writeLong(childId); cursor.writeString(message); cursor.writeString(sentAt) } }
data class ParentChallengeCompletedPacket(val challengeId: String, val childId: Long, val message: String, val completedAt: String) : MentoraPacket() { override val id = 68; override fun writeBody(cursor: ByteCursor) { cursor.writeString(challengeId); cursor.writeLong(childId); cursor.writeString(message); cursor.writeString(completedAt) } }
data class FetchWeeklyReportPacket(val childId: Long) : MentoraPacket() { override val id = 69; override fun writeBody(cursor: ByteCursor) = cursor.writeLong(childId) }
data class WeeklyReportResponsePacket(val childId: Long, val childName: String, val weekStart: String, val weekEnd: String, val reportText: String, val aiGenerated: Boolean) : MentoraPacket() { override val id = 70; override fun writeBody(cursor: ByteCursor) { cursor.writeLong(childId); cursor.writeString(childName); cursor.writeString(weekStart); cursor.writeString(weekEnd); cursor.writeString(reportText); cursor.writeBoolean(aiGenerated) } }
data class SetClientLanguagePacket(val languageTag: String) : MentoraPacket() { override val id = 76; override fun writeBody(cursor: ByteCursor) = cursor.writeString(languageTag) }

data class ParentSecondFactorRequiredPacket(
    val challengeId: String,
    val expiresInSeconds: Int,
    val recoveryAllowed: Boolean
) : MentoraPacket() {
    override val id = 81
    override fun writeBody(cursor: ByteCursor) {
        cursor.writeString(challengeId)
        cursor.writeInt(expiresInSeconds)
        cursor.writeBoolean(recoveryAllowed)
    }
}

data class VerifyParentSecondFactorPacket(
    val challengeId: String,
    val code: String
) : MentoraPacket() {
    override val id = 82
    override fun writeBody(cursor: ByteCursor) {
        cursor.writeString(challengeId)
        cursor.writeString(code)
    }
}

data class BeginParentTotpEnrollmentPacket(val passwordHash: String) : MentoraPacket() {
    override val id = 83
    override fun writeBody(cursor: ByteCursor) = cursor.writeString(passwordHash)
}

data class ParentTotpEnrollmentDetailsPacket(
    val enrollmentId: String,
    val secretBase32: String,
    val otpAuthUri: String
) : MentoraPacket() {
    override val id = 84
    override fun writeBody(cursor: ByteCursor) {
        cursor.writeString(enrollmentId)
        cursor.writeString(secretBase32)
        cursor.writeString(otpAuthUri)
    }
}

data class ConfirmParentTotpEnrollmentPacket(
    val enrollmentId: String,
    val code: String
) : MentoraPacket() {
    override val id = 85
    override fun writeBody(cursor: ByteCursor) {
        cursor.writeString(enrollmentId)
        cursor.writeString(code)
    }
}

data class ParentTotpEnrollmentResultPacket(
    val success: Boolean,
    val message: String,
    val recoveryCodes: List<String>
) : MentoraPacket() {
    override val id = 86
    override fun writeBody(cursor: ByteCursor) {
        cursor.writeBoolean(success)
        cursor.writeString(message)
        cursor.writeInt(recoveryCodes.size)
        recoveryCodes.forEach(cursor::writeString)
    }
}

data class DisableParentTotpPacket(
    val passwordHash: String,
    val code: String
) : MentoraPacket() {
    override val id = 87
    override fun writeBody(cursor: ByteCursor) {
        cursor.writeString(passwordHash)
        cursor.writeString(code)
    }
}

class FetchParentSecurityStatusPacket : MentoraPacket() {
    override val id = 88
    override fun writeBody(cursor: ByteCursor) = Unit
}

data class ParentSecurityStatusPacket(
    val totpEnabled: Boolean,
    val recoveryCodesRemaining: Int
) : MentoraPacket() {
    override val id = 89
    override fun writeBody(cursor: ByteCursor) {
        cursor.writeBoolean(totpEnabled)
        cursor.writeInt(recoveryCodesRemaining)
    }
}

data class ParentAuthSessionPacket(
    val success: Boolean,
    val parentId: Long,
    val message: String,
    val parentPfp: String,
    val sessionToken: String,
    val expiresAtEpochSeconds: Long
) : MentoraPacket() {
    override val id = 90
    override fun writeBody(cursor: ByteCursor) {
        cursor.writeBoolean(success)
        cursor.writeLong(parentId)
        cursor.writeString(message)
        cursor.writeString(parentPfp)
        cursor.writeString(sessionToken)
        cursor.writeLong(expiresAtEpochSeconds)
    }
}

data class ResumeParentSessionPacket(
    val sessionToken: String,
    val deviceId: String
) : MentoraPacket() {
    override val id = 91
    override fun writeBody(cursor: ByteCursor) {
        cursor.writeString(sessionToken)
        cursor.writeString(deviceId)
    }
}

data class RevokeParentSessionPacket(
    val sessionToken: String,
    val revokeAll: Boolean
) : MentoraPacket() {
    override val id = 92
    override fun writeBody(cursor: ByteCursor) {
        cursor.writeString(sessionToken)
        cursor.writeBoolean(revokeAll)
    }
}
