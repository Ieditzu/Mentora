package io.github.kawase.ui

import androidx.compose.runtime.State
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import io.github.kawase.socket.packet.Packet
import io.github.kawase.socket.packet.PacketManager
import io.github.kawase.socket.packet.impl.*
import io.github.kawase.socket.utility.HashUtility
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.launch
import org.java_websocket.client.WebSocketClient
import org.java_websocket.handshake.ServerHandshake
import java.net.URI
import java.nio.ByteBuffer

import androidx.compose.ui.graphics.Color
import io.github.kawase.ui.theme.PrimaryLight
import io.github.kawase.ui.theme.SecondaryLight
import io.github.kawase.ui.theme.BackgroundWhite
import io.github.kawase.ui.theme.SurfaceGray
import io.github.kawase.localization.AppLanguages

import android.app.Application
import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import android.content.SharedPreferences
import android.os.Build
import android.util.Log
import androidx.compose.ui.graphics.toArgb
import androidx.core.app.NotificationCompat
import androidx.lifecycle.AndroidViewModel
import org.json.JSONObject

data class Child(val id: Long, val name: String, val points: Int, val isOnline: Boolean, val pfp: String? = null)
data class Task(val id: Long, val name: String, val points: Int)
data class Goal(val id: Long, val title: String, val reward: String, val completed: Boolean, val requiredPoints: Int)
data class CompletedTask(val id: Long, val taskTitle: String, val pointValue: Int, val completedAt: String)
data class LiveSessionState(
    val childId: Long,
    val childName: String,
    val online: Boolean,
    val padName: String,
    val codeText: String,
    val attemptCount: Int,
    val hintRequested: Boolean,
    val status: String,
    val updatedAt: String
)
data class WeeklyReport(
    val childId: Long,
    val childName: String,
    val weekStart: String,
    val weekEnd: String,
    val reportText: String,
    val aiGenerated: Boolean
)
data class AiProfile(
    val level: String,
    val totalInteractions: Int,
    val correctCount: Int,
    val incorrectCount: Int,
    val hintsUsed: Int,
    val chatTurns: Int,
    val strengths: List<String>,
    val needsHelp: List<String>,
    val recentMistakes: List<String>,
    val struggleConcepts: List<String>,
    val commonMistakes: List<String>,
    val helpTopics: List<String>,
    val skillScores: Map<String, Float>,
    val lastUpdated: String,
    val summaryText: String,
    val summaryOneLine: String,
    val summaryThreeLine: String
)

class SocketViewModel(application: Application) : AndroidViewModel(application) {
    private var client: AndroidClientSocket? = null
    private val packetManager = PacketManager()
    private val prefs: SharedPreferences = application.getSharedPreferences("mentora_prefs", Context.MODE_PRIVATE)
    private var notificationId = 100

    companion object {
        private const val CHANNEL_ID = "mentora_activity"
        private const val CHANNEL_NAME = "Mentora Activity"
        private const val APP_LANGUAGE_KEY = "app_language"
    }

    init {
        createNotificationChannel(application)
    }

    private fun createNotificationChannel(context: Context) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(CHANNEL_ID, CHANNEL_NAME, NotificationManager.IMPORTANCE_DEFAULT).apply {
                description = "Notifications about your child's learning activity"
            }
            val manager = context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
            manager.createNotificationChannel(channel)
        }
    }

    private fun sendNotification(title: String, body: String) {
        val context = getApplication<Application>()
        val manager = context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        val notification = NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_dialog_info)
            .setContentTitle(title)
            .setContentText(body)
            .setPriority(NotificationCompat.PRIORITY_DEFAULT)
            .setAutoCancel(true)
            .build()
        manager.notify(notificationId++, notification)
    }

    var isDarkMode = mutableStateOf(prefs.getBoolean("dark_mode", false))
    var primaryColor = mutableStateOf(Color(prefs.getInt("primary_color", PrimaryLight.toArgb())))
    var secondaryColor = mutableStateOf(Color(prefs.getInt("secondary_color", SecondaryLight.toArgb())))
    var appLanguage = mutableStateOf(
        prefs.getString(APP_LANGUAGE_KEY, AppLanguages.SYSTEM_DEFAULT)
            ?.takeIf { it == AppLanguages.SYSTEM_DEFAULT || AppLanguages.supported.any { language -> language.tag == it } }
            ?: AppLanguages.SYSTEM_DEFAULT
    )

    private val _isConnected = mutableStateOf(false)
    val isConnected: State<Boolean> = _isConnected

    private val _isLoggedIn = mutableStateOf(false)
    val isLoggedIn: State<Boolean> = _isLoggedIn

    private val _parentId = mutableStateOf(-1L)
    val parentId: State<Long> = _parentId

    private val _email = mutableStateOf(prefs.getString("saved_email", "") ?: "")
    val email: State<String> = _email

    private val _parentPfp = mutableStateOf<String?>(null)
    val parentPfp: State<String?> = _parentPfp
    
    private var savedEmailHash: String? = prefs.getString("email_hash", null)
    private var savedPasswordHash: String? = prefs.getString("password_hash", null)

    private var reconnectJob: Job? = null
    private var currentUrl: String = "wss://neuro.serenityutils.club"

    fun toggleDarkMode() {
        isDarkMode.value = !isDarkMode.value
        prefs.edit().putBoolean("dark_mode", isDarkMode.value).apply()
    }

    fun updatePrimaryColor(color: Color) {
        primaryColor.value = color
        prefs.edit().putInt("primary_color", color.toArgb()).apply()
    }

    fun updateAppLanguage(languageTag: String) {
        require(languageTag == AppLanguages.SYSTEM_DEFAULT || AppLanguages.supported.any { it.tag == languageTag }) {
            "Unsupported language: $languageTag"
        }
        appLanguage.value = languageTag
        prefs.edit().putString(APP_LANGUAGE_KEY, languageTag).apply()
        sendPacket(SetClientLanguagePacket(AppLanguages.resolve(languageTag, getApplication<Application>().resources.configuration)))
    }

    fun logout() {
        _isLoggedIn.value = false
        savedEmailHash = null
        savedPasswordHash = null
        prefs.edit()
            .remove("saved_email")
            .remove("email_hash")
            .remove("password_hash")
            .apply()
        client?.close()
    }

    private val _children = mutableStateListOf<Child>()
    val children: List<Child> = _children

    private val _tasks = mutableStateListOf<Task>()
    val tasks: List<Task> = _tasks

    private val _goals = mutableStateListOf<Goal>()
    val goals: List<Goal> = _goals

    private val _completedTasks = mutableStateListOf<CompletedTask>()
    val completedTasks: List<CompletedTask> = _completedTasks

    private val _aiProfilesCpp = mutableStateMapOf<Long, AiProfile>()
    val aiProfilesCpp: Map<Long, AiProfile> = _aiProfilesCpp
    private val _aiProfilesPython = mutableStateMapOf<Long, AiProfile>()
    val aiProfilesPython: Map<Long, AiProfile> = _aiProfilesPython
    private val _aiProfilesGeneral = mutableStateMapOf<Long, AiProfile>()
    val aiProfilesGeneral: Map<Long, AiProfile> = _aiProfilesGeneral
    private val _liveSessions = mutableStateMapOf<Long, LiveSessionState>()
    val liveSessions: Map<Long, LiveSessionState> = _liveSessions
    private val _weeklyReports = mutableStateMapOf<Long, WeeklyReport>()
    val weeklyReports: Map<Long, WeeklyReport> = _weeklyReports
    private val _weeklyReportLoading = mutableStateMapOf<Long, Boolean>()
    val weeklyReportLoading: Map<Long, Boolean> = _weeklyReportLoading
    private var pendingChildStatsId: Long? = null
    private var pendingWeeklyReportChildId: Long? = null

    private val _errorFlow = MutableSharedFlow<String>()
    val errorFlow: SharedFlow<String> = _errorFlow.asSharedFlow()

    private val _successFlow = MutableSharedFlow<String>()
    val successFlow: SharedFlow<String> = _successFlow.asSharedFlow()

    fun connect(url: String = "wss://neuro.serenityutils.club") {
        currentUrl = url
        if (reconnectJob == null || reconnectJob?.isCompleted == true) {
            startConnectionLoop()
        }
    }

    private fun startConnectionLoop() {
        reconnectJob = viewModelScope.launch(Dispatchers.IO) {
            while (true) {
                if (client == null || !client!!.isOpen) {
                    Log.d("Mentora", "Attempting connection to $currentUrl...")
                    try {
                        client = AndroidClientSocket(URI(currentUrl))
                        client?.connectBlocking()
                    } catch (e: Exception) {
                        Log.e("Mentora", "Connection failed: ${e.message}")
                    }
                }
                delay(5000) // Retry every 5 seconds
            }
        }
    }

    fun login(email: String, password: String) {
        this._email.value = email
        val emailHash = HashUtility.hash(email)
        val passwordHash = HashUtility.hash(password)
        
        savedEmailHash = emailHash
        savedPasswordHash = passwordHash
        prefs.edit()
            .putString("saved_email", email)
            .putString("email_hash", emailHash)
            .putString("password_hash", passwordHash)
            .apply()
            
        sendPacket(AuthPacket(emailHash, passwordHash))
    }

    fun register(email: String, password: String) {
        this._email.value = email
        val emailHash = HashUtility.hash(email)
        val passwordHash = HashUtility.hash(password)
        
        savedEmailHash = emailHash
        savedPasswordHash = passwordHash
        prefs.edit()
            .putString("saved_email", email)
            .putString("email_hash", emailHash)
            .putString("password_hash", passwordHash)
            .apply()
            
        sendPacket(RegisterParentPacket(emailHash, passwordHash))
    }

    fun addChild(name: String) {
        sendPacket(AddChildPacket(name))
    }

    fun fetchChildren() {
        sendPacket(FetchChildrenPacket())
    }

    fun fetchTasks() {
        sendPacket(FetchTasksPacket())
    }

    fun fetchGoals(childId: Long) {
        sendPacket(FetchGoalsPacket(childId))
    }

    fun fetchCompletedTasks(childId: Long) {
        sendPacket(FetchCompletedTasksPacket(childId))
    }

    fun fetchChildProfile(childId: Long) {
        pendingChildStatsId = childId
        sendPacket(FetchChildStatsByParentPacket(childId))
    }

    fun watchLiveSession(childId: Long) {
        sendPacket(SubscribeLiveSessionPacket(childId, true))
    }

    fun unwatchLiveSession(childId: Long) {
        sendPacket(SubscribeLiveSessionPacket(childId, false))
    }

    fun fetchWeeklyReport(childId: Long) {
        pendingWeeklyReportChildId = childId
        _weeklyReportLoading[childId] = true
        sendPacket(FetchWeeklyReportPacket(childId))
    }

    fun sendParentChallenge(childId: Long, message: String) {
        val trimmed = message.trim()
        if (trimmed.isNotEmpty()) {
            sendPacket(SendParentChallengePacket(childId, trimmed))
        }
    }

    fun addGoal(childId: Long, title: String, reward: String, points: Int, taskId: Long) {
        sendPacket(AddGoalPacket(childId, title, reward, points, taskId))
    }

    fun claimQRLogin(token: String, childId: Long) {
        sendPacket(ClaimQRLoginPacket(token, childId))
    }

    fun removeChild(childId: Long) {
        sendPacket(RemoveChildPacket(childId))
    }

    private var pendingPfp: String? = null
    private var pendingPfpId: Long? = null

    fun updatePfp(childId: Long, base64Pfp: String) {
        pendingPfp = base64Pfp
        pendingPfpId = childId
        sendPacket(UpdatePfpPacket(childId, base64Pfp))
    }

    private fun sendPacket(packet: Packet) {
        viewModelScope.launch(Dispatchers.IO) {
            client?.let {
                if (it.isOpen) {
                    try {
                        it.send(packet.encode())
                    } catch (e: Exception) {
                        Log.e("Mentora", "Failed to send packet: ${e.message}")
                    }
                } else {
                    _errorFlow.emit("Server disconnected. Retrying...")
                }
            } ?: run {
                _errorFlow.emit("Connecting to server...")
            }
        }
    }

    private inner class AndroidClientSocket(uri: URI) : WebSocketClient(uri) {
        init {
            if (uri.scheme == "wss") {
                try {
                    val sslContext = javax.net.ssl.SSLContext.getInstance("TLS")
                    sslContext.init(null, null, null)
                    setSocketFactory(sslContext.socketFactory)
                } catch (e: Exception) {
                    e.printStackTrace()
                }
            }
            connectionLostTimeout = 10 // Detect dead connections faster
        }

        override fun onOpen(handshakedata: ServerHandshake?) {
            Log.d("Mentora", "Socket opened")
            _isConnected.value = true
            send(HandShakePacket("android_client").encode())
            send(SetClientLanguagePacket(AppLanguages.resolve(appLanguage.value, getApplication<Application>().resources.configuration)).encode())
            
            if (savedEmailHash != null && savedPasswordHash != null) {
                send(AuthPacket(savedEmailHash, savedPasswordHash).encode())
            }
        }

        override fun onMessage(message: String?) {}

        override fun onMessage(bytes: ByteBuffer?) {
            bytes?.let {
                try {
                    val packet = Packet.construct(it, packetManager)
                    handlePacket(packet)
                } catch (e: Exception) {
                    viewModelScope.launch { _errorFlow.emit("Data error: ${e.message}") }
                }
            }
        }

        override fun onClose(code: Int, reason: String?, remote: Boolean) {
            Log.d("Mentora", "Socket closed: $reason")
            _isConnected.value = false
            _isLoggedIn.value = false
        }

        override fun onError(ex: Exception?) {
            Log.e("Mentora", "Socket error: ${ex?.message}")
            viewModelScope.launch { 
                val msg = ex?.message ?: "Unknown error"
                if (!msg.contains("Connection refused")) {
                    _errorFlow.emit("Socket Error: $msg")
                }
            }
        }
    }

    private fun handlePacket(packet: Packet) {
        when (packet) {
            is AuthResponsePacket -> {
                if (packet.isSuccess) {
                    _parentId.value = packet.parentId
                    _parentPfp.value = packet.parentPfp
                    _isLoggedIn.value = true
                    viewModelScope.launch { _successFlow.emit("Welcome back!") }
                    fetchChildren()
                    fetchTasks()
                } else {
                    viewModelScope.launch { _errorFlow.emit("Login failed: ${packet.message}") }
                }
            }
            is ActionResponsePacket -> {
                viewModelScope.launch {
                    if (packet.isSuccess) {
                        _successFlow.emit(packet.message ?: "Success")
                        if (packet.requestPacketId == 4 || packet.requestPacketId == 27) {
                            fetchChildren()
                        }
                        if (packet.requestPacketId == 26) { // UpdatePfp
                            if (pendingPfpId == -1L) {
                                _parentPfp.value = pendingPfp
                            }
                            fetchChildren()
                        }
                        if (packet.requestPacketId == 8) { // Child completed a task
                            val childName = children.firstOrNull()?.name ?: "Your child"
                            sendNotification("Task Completed!", "$childName compleated the logic minigames")
                            fetchChildren()
                        }
                        if (packet.requestPacketId == 66) {
                            _successFlow.emit(packet.message ?: "Challenge sent")
                        }
                    } else {
                        if (packet.requestPacketId == 69) {
                            pendingWeeklyReportChildId?.let { _weeklyReportLoading[it] = false }
                            pendingWeeklyReportChildId = null
                        }
                        _errorFlow.emit("Error: ${packet.message}")
                    }
                }
            }
            is FetchChildrenResponsePacket -> {
                _children.clear()
                packet.children.forEach { child ->
                    _children.add(Child(child.id, child.name, child.totalPoints, child.isOnline, child.pfp))
                }
            }
            is FetchTasksResponsePacket -> {
                _tasks.clear()
                packet.tasks.forEach { task ->
                   _tasks.add(Task(task.id, task.title, task.pointValue))
                }
            }
            is FetchGoalsResponsePacket -> {
                _goals.clear()
                packet.goals.forEach { goal ->
                    _goals.add(Goal(goal.id, goal.title, goal.reward, goal.isCompleted, goal.requiredPoints))
                }
            }
            is FetchCompletedTasksResponsePacket -> {
                _completedTasks.clear()
                packet.completedTasks.forEach { ct ->
                    _completedTasks.add(CompletedTask(ct.id, ct.taskTitle, ct.pointValue, ct.completedAt))
                }
            }
            is FetchChildStatsResponsePacket -> {
                val targetId = pendingChildStatsId
                if (targetId != null) {
                    parseAiProfile(packet.gameStatsJson, "aiProfileCpp")?.let { profile ->
                        _aiProfilesCpp[targetId] = profile
                    }
                    parseAiProfile(packet.gameStatsJson, "aiProfilePython")?.let { profile ->
                        _aiProfilesPython[targetId] = profile
                    }
                    parseAiProfile(packet.gameStatsJson, "aiProfileGeneral")?.let { profile ->
                        _aiProfilesGeneral[targetId] = profile
                    }
                    pendingChildStatsId = null
                }
            }
            is LiveSessionUpdatePacket -> {
                _liveSessions[packet.childId] = LiveSessionState(
                    childId = packet.childId,
                    childName = packet.childName,
                    online = packet.isOnline,
                    padName = packet.padName,
                    codeText = packet.codeText,
                    attemptCount = packet.attemptCount,
                    hintRequested = packet.isHintRequested,
                    status = packet.status,
                    updatedAt = packet.updatedAt
                )
            }
            is WeeklyReportResponsePacket -> {
                _weeklyReportLoading[packet.childId] = false
                if (pendingWeeklyReportChildId == packet.childId) {
                    pendingWeeklyReportChildId = null
                }
                _weeklyReports[packet.childId] = WeeklyReport(
                    childId = packet.childId,
                    childName = packet.childName,
                    weekStart = packet.weekStart,
                    weekEnd = packet.weekEnd,
                    reportText = packet.reportText,
                    aiGenerated = packet.isAiGenerated
                )
            }
            is ParentChallengeCompletedPacket -> {
                val childName = children.firstOrNull { it.id == packet.childId }?.name ?: "Your child"
                sendNotification("Challenge completed", "$childName finished: ${packet.message}")
                viewModelScope.launch { _successFlow.emit("$childName completed tonight's challenge") }
                fetchChildren()
            }
        }
    }

    private fun parseAiProfile(gameStatsJson: String, key: String): AiProfile? {
        return try {
            val stats = JSONObject(gameStatsJson)
            if (!stats.has(key)) return null
            val profile = stats.getJSONObject(key)
            val correct = profile.optInt("correctCount", 0)
            val incorrect = profile.optInt("incorrectCount", 0)
            val total = correct + incorrect
            val accuracy = if (total == 0) 0.0 else correct.toDouble() / total.toDouble()
            val level = when {
                total < 4 -> "Beginner"
                accuracy >= 0.85 && total >= 8 -> "Advanced"
                accuracy >= 0.65 -> "Intermediate"
                else -> "Beginner"
            }

            val topicsJson = profile.optJSONObject("topics")
            val scores = mutableListOf<Pair<String, Int>>()
            if (topicsJson != null) {
                val keys = topicsJson.keys()
                while (keys.hasNext()) {
                    val key = keys.next()
                    val t = topicsJson.optJSONObject(key) ?: continue
                    val tCorrect = t.optInt("correct", 0)
                    val tIncorrect = t.optInt("incorrect", 0)
                    scores.add(normalizeTopic(key) to (tCorrect - tIncorrect))
                }
            }

            val strengths = scores.sortedByDescending { it.second }.filter { it.second > 0 }.map { it.first }.take(3)
            val needsHelp = scores.sortedBy { it.second }.filter { it.second < 0 }.map { it.first }.take(3)

            val mistakes = mutableListOf<String>()
            val recentEvents = profile.optJSONArray("recentEvents")
            if (recentEvents != null) {
                for (i in 0 until recentEvents.length()) {
                    val ev = recentEvents.optJSONObject(i) ?: continue
                    if (ev.optString("correctness") == "incorrect") {
                        mistakes.add(normalizeTopic(ev.optString("topic", "unknown")))
                    }
                }
            }

            val struggleConcepts = mutableListOf<Pair<String, Int>>()
            val helpConcepts = mutableListOf<Pair<String, Int>>()
            val conceptsJson = profile.optJSONObject("concepts")
            if (conceptsJson != null) {
                val keys = conceptsJson.keys()
                while (keys.hasNext()) {
                    val key = keys.next()
                    val c = conceptsJson.optJSONObject(key) ?: continue
                    val cCorrect = c.optInt("correct", 0)
                    val cIncorrect = c.optInt("incorrect", 0)
                    val help = c.optInt("helpRequests", 0)
                    struggleConcepts.add(key to (cIncorrect - cCorrect))
                    helpConcepts.add(key to help)
                }
            }

            val struggleList = struggleConcepts.sortedByDescending { it.second }.filter { it.second > 0 }.map { it.first }.take(3)
            val helpTopics = helpConcepts.sortedByDescending { it.second }.filter { it.second > 0 }.map { it.first }.take(3)

            val commonMistakes = mutableListOf<Pair<String, Int>>()
            val mistakesJson = profile.optJSONObject("mistakes")
            if (mistakesJson != null) {
                val keys = mistakesJson.keys()
                while (keys.hasNext()) {
                    val key = keys.next()
                    commonMistakes.add(key to mistakesJson.optInt(key, 0))
                }
            }
            val commonMistakeList = commonMistakes.sortedByDescending { it.second }.filter { it.second > 0 }.map { it.first }.take(3)
            val skillScores = buildSkillScores(topicsJson, conceptsJson, correct, incorrect)

            val rawOneLine = profile.optString("summaryOneLine", "")
            val rawThreeLine = profile.optString("summaryThreeLine", "")
            val rawSummary = profile.optString("summaryText", "")

            val summaryOneLine = rawOneLine.ifBlank {
                if (total == 0) "No activity yet."
                else "${level.replaceFirstChar { it.uppercase() }} level - ${String.format("%.0f", accuracy * 100)}% accuracy across $total attempts."
            }
            val summaryThreeLine = rawThreeLine.ifBlank {
                buildString {
                    append(summaryOneLine)
                    if (strengths.isNotEmpty()) append(" Strengths: ${strengths.joinToString(", ")}.")
                    if (needsHelp.isNotEmpty()) append(" Needs work on: ${needsHelp.joinToString(", ")}.")
                }
            }

            AiProfile(
                level = level,
                totalInteractions = profile.optInt("totalInteractions", 0),
                correctCount = correct,
                incorrectCount = incorrect,
                hintsUsed = profile.optInt("hintsUsed", 0),
                chatTurns = profile.optInt("chatTurns", 0),
                strengths = strengths,
                needsHelp = needsHelp,
                recentMistakes = mistakes.take(3),
                struggleConcepts = struggleList,
                commonMistakes = commonMistakeList,
                helpTopics = helpTopics,
                skillScores = skillScores,
                lastUpdated = profile.optString("lastUpdated", ""),
                summaryText = rawSummary,
                summaryOneLine = summaryOneLine,
                summaryThreeLine = summaryThreeLine
            )
        } catch (e: Exception) {
            null
        }
    }

    private fun buildSkillScores(topicsJson: JSONObject?, conceptsJson: JSONObject?, correct: Int, incorrect: Int): Map<String, Float> {
        val axes = linkedMapOf(
            "Loops" to listOf("loop", "for", "while", "range"),
            "Functions" to listOf("function", "def ", "return"),
            "Conditionals" to listOf("conditional", "condition", "if", "else", "switch"),
            "Recursion" to listOf("recursion", "recursive"),
            "Memory" to listOf("memory", "pointer", "reference", "address", "pass-by-reference"),
            "Data Structures" to listOf("data", "structure", "array", "vector", "list", "collection", "dictionary", "map")
        )
        val totals = axes.keys.associateWith { intArrayOf(0, 0) }.toMutableMap()

        fun addFrom(json: JSONObject?) {
            if (json == null) return
            val keys = json.keys()
            while (keys.hasNext()) {
                val key = keys.next()
                val normalized = key.lowercase()
                val stats = json.optJSONObject(key) ?: continue
                axes.forEach { (axis, markers) ->
                    if (markers.any { normalized.contains(it) }) {
                        val bucket = totals[axis] ?: intArrayOf(0, 0)
                        bucket[0] += stats.optInt("correct", 0)
                        bucket[1] += stats.optInt("incorrect", 0)
                        totals[axis] = bucket
                    }
                }
            }
        }

        addFrom(conceptsJson)
        addFrom(topicsJson)

        val overallTotal = correct + incorrect
        val overall = if (overallTotal == 0) 0f else correct.toFloat() / overallTotal.toFloat()
        return totals.mapValues { (_, score) ->
            val attempts = score[0] + score[1]
            when {
                attempts > 0 -> (score[0].toFloat() / attempts.toFloat()).coerceIn(0f, 1f)
                overallTotal > 0 -> (overall * 0.35f).coerceIn(0f, 1f)
                else -> 0f
            }
        }
    }

    private fun normalizeTopic(topic: String): String {
        var t = topic
        if (t.startsWith("cpp:", true)) {
            t = t.substring(4)
        } else if (t.startsWith("python:", true)) {
            t = t.substring(7)
        } else if (t.startsWith("py:", true)) {
            t = t.substring(3)
        }
        return t.trim().ifEmpty { "general" }
    }
}
