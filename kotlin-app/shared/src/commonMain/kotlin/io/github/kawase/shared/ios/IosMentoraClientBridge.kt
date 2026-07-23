package io.github.kawase.shared.ios

import io.github.kawase.shared.model.Child
import io.github.kawase.shared.model.CompletedTask
import io.github.kawase.shared.model.Goal
import io.github.kawase.shared.model.LiveSessionState
import io.github.kawase.shared.model.Task
import io.github.kawase.shared.model.WeeklyReport
import io.github.kawase.shared.protocol.ActionResponsePacket
import io.github.kawase.shared.protocol.AddChildPacket
import io.github.kawase.shared.protocol.AddGoalPacket
import io.github.kawase.shared.protocol.AiResponsePacket
import io.github.kawase.shared.protocol.AskAiPacket
import io.github.kawase.shared.protocol.AuthPacket
import io.github.kawase.shared.protocol.AuthResponsePacket
import io.github.kawase.shared.protocol.BeginParentTotpEnrollmentPacket
import io.github.kawase.shared.protocol.ClaimQRLoginPacket
import io.github.kawase.shared.protocol.CompleteTaskPacket
import io.github.kawase.shared.protocol.ConfirmParentTotpEnrollmentPacket
import io.github.kawase.shared.protocol.DisableParentTotpPacket
import io.github.kawase.shared.protocol.FetchChildStatsByParentPacket
import io.github.kawase.shared.protocol.FetchChildStatsResponsePacket
import io.github.kawase.shared.protocol.FetchChildrenPacket
import io.github.kawase.shared.protocol.FetchChildrenResponsePacket
import io.github.kawase.shared.protocol.FetchCompletedTasksPacket
import io.github.kawase.shared.protocol.FetchCompletedTasksResponsePacket
import io.github.kawase.shared.protocol.FetchGoalsPacket
import io.github.kawase.shared.protocol.FetchGoalsResponsePacket
import io.github.kawase.shared.protocol.FetchParentSecurityStatusPacket
import io.github.kawase.shared.protocol.FetchTasksPacket
import io.github.kawase.shared.protocol.FetchTasksResponsePacket
import io.github.kawase.shared.protocol.FetchWeeklyReportPacket
import io.github.kawase.shared.protocol.HandshakePacket
import io.github.kawase.shared.protocol.LiveSessionUpdatePacket
import io.github.kawase.shared.protocol.MentoraPacket
import io.github.kawase.shared.protocol.PacketFactory
import io.github.kawase.shared.protocol.PacketFrameCodec
import io.github.kawase.shared.protocol.ParentChallengeCompletedPacket
import io.github.kawase.shared.protocol.ParentChallengePacket
import io.github.kawase.shared.protocol.ParentAuthSessionPacket
import io.github.kawase.shared.protocol.ParentSecondFactorRequiredPacket
import io.github.kawase.shared.protocol.ParentSecurityStatusPacket
import io.github.kawase.shared.protocol.ParentTotpEnrollmentDetailsPacket
import io.github.kawase.shared.protocol.ParentTotpEnrollmentResultPacket
import io.github.kawase.shared.protocol.RegisterParentPacket
import io.github.kawase.shared.protocol.RemoveChildPacket
import io.github.kawase.shared.protocol.ResumeParentSessionPacket
import io.github.kawase.shared.protocol.RevokeParentSessionPacket
import io.github.kawase.shared.protocol.SendParentChallengePacket
import io.github.kawase.shared.protocol.SetClientLanguagePacket
import io.github.kawase.shared.protocol.SubscribeLiveSessionPacket
import io.github.kawase.shared.protocol.UpdatePfpPacket
import io.github.kawase.shared.protocol.VerifyParentSecondFactorPacket
import io.github.kawase.shared.protocol.WeeklyReportResponsePacket

/** A Swift-exportable, immutable view of all state received from the parent server. */
data class IosMentoraSnapshot(
    val isLoggedIn: Boolean = false,
    val parentId: Long = -1L,
    val parentProfilePicture: String = "",
    val languageTag: String = "en",
    val children: List<Child> = emptyList(),
    val tasks: List<Task> = emptyList(),
    val goals: List<Goal> = emptyList(),
    val completedTasks: List<CompletedTask> = emptyList(),
    val profiles: List<IosChildProfile> = emptyList(),
    val liveSessions: List<LiveSessionState> = emptyList(),
    val weeklyReports: List<WeeklyReport> = emptyList(),
    val lastAiResponse: String = "",
    val twoFactorEnabled: Boolean = false,
    val recoveryCodesRemaining: Int = 0
)

/** The server returns profile analytics as JSON; Swift owns presentation and parsing of this payload. */
data class IosChildProfile(
    val childId: Long,
    val name: String,
    val totalPoints: Int,
    val gameStatsJson: String,
    val streak: Int,
    val completedTaskCount: Int,
    val totalTaskCount: Int
)

/** Result of one incoming encrypted server frame, suitable for SwiftUI alert and state handling. */
data class IosMentoraEvent(
    val type: String,
    val success: Boolean = true,
    val message: String = "",
    val requestPacketId: Int = -1,
    val snapshot: IosMentoraSnapshot,
    val challengeId: String = "",
    val expiresInSeconds: Int = 0,
    val recoveryAllowed: Boolean = false,
    val enrollmentId: String = "",
    val secretBase32: String = "",
    val otpAuthUri: String = "",
    val recoveryCodes: List<String> = emptyList()
)

/**
 * Protocol reducer used by the Swift app. Command methods produce binary WebSocket
 * payloads; the app sends them over URLSessionWebSocketTask and feeds binary replies
 * back through [receive]. No credentials are retained by this bridge.
 */
class IosMentoraClientBridge(languageTag: String = "en") {
    private val frameCodec = PacketFrameCodec()
    private var current = IosMentoraSnapshot(languageTag = languageTag)
    private val profilesByChildId = linkedMapOf<Long, IosChildProfile>()
    private val liveSessionsByChildId = linkedMapOf<Long, LiveSessionState>()
    private val reportsByChildId = linkedMapOf<Long, WeeklyReport>()
    private var pendingProfileChildId: Long? = null
    private var pendingSessionToken = ""
    private var pendingSessionExpiry = 0L

    fun snapshot(): IosMentoraSnapshot = current

    fun handshake(clientFingerprint: String = "ios_client"): ByteArray = frame(HandshakePacket(clientFingerprint))
    fun handshakeV2(clientFingerprint: String, deviceId: String): ByteArray {
        return frame(HandshakePacket(clientFingerprint, 2, deviceId))
    }

    fun authenticate(email: String, password: String): ByteArray {
        return authenticateHashed(
            MentoraSha256.hex(email.trim().lowercase()),
            MentoraSha256.hex(password)
        )
    }

    fun authenticateHashed(emailHash: String, passwordHash: String): ByteArray = frame(AuthPacket(emailHash, passwordHash))

    fun sha256(value: String): String = MentoraSha256.hex(value)

    /** Matches Android's deployed registration behavior: the server receives hashed email and password. */
    fun register(email: String, password: String): ByteArray {
        return registerHashed(
            MentoraSha256.hex(email.trim().lowercase()),
            MentoraSha256.hex(password)
        )
    }

    fun registerHashed(emailHash: String, passwordHash: String): ByteArray {
        return frame(RegisterParentPacket(emailHash, passwordHash))
    }

    fun setLanguage(languageTag: String): ByteArray {
        current = current.copy(languageTag = languageTag)
        return frame(SetClientLanguagePacket(languageTag))
    }

    fun addChild(name: String): ByteArray = frame(AddChildPacket(name))
    fun removeChild(childId: Long): ByteArray = frame(RemoveChildPacket(childId))
    fun fetchChildren(): ByteArray = frame(FetchChildrenPacket())
    fun fetchTasks(): ByteArray = frame(FetchTasksPacket())
    fun completeTask(childId: Long, taskId: Long): ByteArray = frame(CompleteTaskPacket(childId, taskId))
    fun fetchGoals(childId: Long): ByteArray = frame(FetchGoalsPacket(childId))
    fun addGoal(childId: Long, title: String, reward: String, requiredPoints: Int, requiredTaskId: Long): ByteArray {
        return frame(AddGoalPacket(childId, title, reward, requiredPoints, requiredTaskId))
    }
    fun fetchCompletedTasks(childId: Long): ByteArray = frame(FetchCompletedTasksPacket(childId))
    fun fetchChildProfile(childId: Long): ByteArray {
        pendingProfileChildId = childId
        return frame(FetchChildStatsByParentPacket(childId))
    }
    fun subscribeLiveSession(childId: Long): ByteArray = frame(SubscribeLiveSessionPacket(childId, true))
    fun unsubscribeLiveSession(childId: Long): ByteArray = frame(SubscribeLiveSessionPacket(childId, false))
    fun fetchWeeklyReport(childId: Long): ByteArray = frame(FetchWeeklyReportPacket(childId))
    fun sendParentChallenge(childId: Long, message: String): ByteArray = frame(SendParentChallengePacket(childId, message.trim()))
    fun claimQrLogin(token: String, childId: Long): ByteArray = frame(ClaimQRLoginPacket(token, childId))
    fun updateProfilePicture(childId: Long, base64Picture: String): ByteArray = frame(UpdatePfpPacket(childId, base64Picture))
    fun askAi(question: String, context: String): ByteArray = frame(AskAiPacket(question, context))
    fun verifySecondFactor(challengeId: String, code: String): ByteArray {
        return frame(VerifyParentSecondFactorPacket(challengeId, code.trim()))
    }
    fun beginTotpEnrollment(password: String): ByteArray {
        return frame(BeginParentTotpEnrollmentPacket(MentoraSha256.hex(password)))
    }
    fun confirmTotpEnrollment(enrollmentId: String, code: String): ByteArray {
        return frame(ConfirmParentTotpEnrollmentPacket(enrollmentId, code.trim()))
    }
    fun disableTotp(password: String, code: String): ByteArray {
        return frame(DisableParentTotpPacket(MentoraSha256.hex(password), code.trim()))
    }
    fun fetchParentSecurityStatus(): ByteArray = frame(FetchParentSecurityStatusPacket())
    fun resumeParentSession(sessionToken: String, deviceId: String): ByteArray {
        return frame(ResumeParentSessionPacket(sessionToken, deviceId))
    }
    fun revokeParentSession(sessionToken: String, revokeAll: Boolean): ByteArray {
        return frame(RevokeParentSessionPacket(sessionToken, revokeAll))
    }

    fun takeSessionToken(): String {
        val sessionToken = pendingSessionToken
        pendingSessionToken = ""
        return sessionToken
    }

    fun takeSessionExpiryEpochSeconds(): Long {
        val sessionExpiry = pendingSessionExpiry
        pendingSessionExpiry = 0L
        return sessionExpiry
    }

    fun clearParentSession() {
        pendingSessionToken = ""
        pendingSessionExpiry = 0L
        current = current.copy(
            isLoggedIn = false,
            parentId = -1L,
            parentProfilePicture = "",
            children = emptyList(),
            tasks = emptyList(),
            goals = emptyList(),
            completedTasks = emptyList(),
            profiles = emptyList(),
            liveSessions = emptyList(),
            weeklyReports = emptyList(),
            lastAiResponse = "",
            twoFactorEnabled = false,
            recoveryCodesRemaining = 0
        )
        profilesByChildId.clear()
        liveSessionsByChildId.clear()
        reportsByChildId.clear()
        pendingProfileChildId = null
    }

    /** Commands normally issued immediately after a successful authentication response. */
    fun initialDashboardRequests(): List<ByteArray> = listOf(fetchChildren(), fetchTasks())

    fun receive(frame: ByteArray): IosMentoraEvent = reduce(PacketFactory.decode(frameCodec.decode(frame)))

    private fun frame(packet: MentoraPacket): ByteArray = frameCodec.encode(PacketFactory.encode(packet))

    private fun reduce(packet: MentoraPacket): IosMentoraEvent {
        var type = "packet"
        var success = true
        var message = ""
        var requestPacketId = -1
        var challengeId = ""
        var expiresInSeconds = 0
        var recoveryAllowed = false
        var enrollmentId = ""
        var secretBase32 = ""
        var otpAuthUri = ""
        var recoveryCodes = emptyList<String>()
        when (packet) {
            is AuthResponsePacket -> {
                type = "authentication"
                success = packet.success
                message = packet.message
                if (packet.success) {
                    current = current.copy(
                        isLoggedIn = true,
                        parentId = packet.parentId,
                        parentProfilePicture = packet.parentPfp
                    )
                }
            }
            is ActionResponsePacket -> {
                type = "action"
                success = packet.success
                message = packet.message
                requestPacketId = packet.requestPacketId
                if (!packet.success && packet.requestPacketId == 91) {
                    clearParentSession()
                }
                if (packet.success && packet.requestPacketId == 87) {
                    current = current.copy(twoFactorEnabled = false, recoveryCodesRemaining = 0)
                }
            }
            is ParentSecondFactorRequiredPacket -> {
                type = "secondFactorRequired"
                challengeId = packet.challengeId
                expiresInSeconds = packet.expiresInSeconds
                recoveryAllowed = packet.recoveryAllowed
            }
            is ParentAuthSessionPacket -> {
                type = "parentSession"
                success = packet.success
                message = packet.message
                if (packet.success) {
                    pendingSessionToken = packet.sessionToken
                    pendingSessionExpiry = packet.expiresAtEpochSeconds
                    current = current.copy(
                        isLoggedIn = true,
                        parentId = packet.parentId,
                        parentProfilePicture = packet.parentPfp
                    )
                } else {
                    clearParentSession()
                }
            }
            is ParentTotpEnrollmentDetailsPacket -> {
                type = "totpEnrollmentDetails"
                enrollmentId = packet.enrollmentId
                secretBase32 = packet.secretBase32
                otpAuthUri = packet.otpAuthUri
            }
            is ParentTotpEnrollmentResultPacket -> {
                type = "totpEnrollmentResult"
                success = packet.success
                message = packet.message
                recoveryCodes = packet.recoveryCodes
                if (packet.success) {
                    current = current.copy(
                        twoFactorEnabled = true,
                        recoveryCodesRemaining = packet.recoveryCodes.size
                    )
                }
            }
            is ParentSecurityStatusPacket -> {
                type = "parentSecurityStatus"
                current = current.copy(
                    twoFactorEnabled = packet.totpEnabled,
                    recoveryCodesRemaining = packet.recoveryCodesRemaining
                )
            }
            is FetchChildrenResponsePacket -> {
                type = "children"
                current = current.copy(children = packet.children.map { Child(it.id, it.name, it.totalPoints, it.isOnline, it.pfp) })
            }
            is FetchTasksResponsePacket -> {
                type = "tasks"
                current = current.copy(tasks = packet.tasks.map { Task(it.id, it.title, it.pointValue) })
            }
            is FetchGoalsResponsePacket -> {
                type = "goals"
                current = current.copy(goals = packet.goals.map { Goal(it.id, it.title, it.reward, it.isCompleted, it.requiredPoints) })
            }
            is FetchCompletedTasksResponsePacket -> {
                type = "history"
                current = current.copy(completedTasks = packet.completedTasks.map { CompletedTask(it.id, it.taskTitle, it.pointValue, it.completedAt) })
            }
            is FetchChildStatsResponsePacket -> {
                type = "profile"
                pendingProfileChildId?.let { childId ->
                    profilesByChildId[childId] = IosChildProfile(childId, packet.name, packet.totalPoints, packet.gameStatsJson, packet.streak, packet.completedTaskCount, packet.totalTaskCount)
                    pendingProfileChildId = null
                    current = current.copy(profiles = profilesByChildId.values.toList())
                }
            }
            is LiveSessionUpdatePacket -> {
                type = "liveSession"
                liveSessionsByChildId[packet.childId] = LiveSessionState(packet.childId, packet.childName, packet.online, packet.padName, packet.codeText, packet.attemptCount, packet.hintRequested, packet.status, packet.updatedAt)
                current = current.copy(liveSessions = liveSessionsByChildId.values.toList())
            }
            is WeeklyReportResponsePacket -> {
                type = "weeklyReport"
                reportsByChildId[packet.childId] = WeeklyReport(packet.childId, packet.childName, packet.weekStart, packet.weekEnd, packet.reportText, packet.aiGenerated)
                current = current.copy(weeklyReports = reportsByChildId.values.toList())
            }
            is AiResponsePacket -> {
                type = "ai"
                message = packet.response
                current = current.copy(lastAiResponse = packet.response)
            }
            is ParentChallengePacket -> {
                type = "challenge"
                message = packet.message
            }
            is ParentChallengeCompletedPacket -> {
                type = "challengeCompleted"
                message = packet.message
            }
            else -> type = "packet_${packet.id}"
        }
        return IosMentoraEvent(
            type,
            success,
            message,
            requestPacketId,
            current,
            challengeId,
            expiresInSeconds,
            recoveryAllowed,
            enrollmentId,
            secretBase32,
            otpAuthUri,
            recoveryCodes
        )
    }
}
