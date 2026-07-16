package io.github.kawase.ui

import org.json.JSONObject
import java.util.Locale

/**
 * Parses the optional machine-learning extension in packet 24's game-stats JSON.
 *
 * This parser is intentionally independent from the legacy programming profiles. A missing or
 * malformed ML object therefore clears only the ML section and cannot affect the existing C++,
 * Python, or General analytics.
 */
internal object MachineLearningProfileParser {
    const val PROFILE_KEY = "aiProfileMachineLearning"

    const val DATA_PREP = "Data Prep"
    const val REGRESSION = "Regression"
    const val CLASSIFICATION = "Classification"
    const val EVALUATION = "Evaluation"
    const val NEURAL_NETWORKS = "Neural Networks"
    const val LLMS = "LLMs"

    val radarAxes = listOf(DATA_PREP, REGRESSION, CLASSIFICATION, EVALUATION, NEURAL_NETWORKS, LLMS)

    fun parse(gameStatsJson: String): AiProfile? {
        return try {
            val root = JSONObject(gameStatsJson)
            val profile = root.optJSONObject(PROFILE_KEY) ?: return null
            val correct = profile.nonNegativeInt("correctCount")
            val incorrect = profile.nonNegativeInt("incorrectCount")
            val total = correct + incorrect
            val accuracy = if (total == 0) 0.0 else correct.toDouble() / total.toDouble()
            val level = when {
                total < 4 -> "Beginner"
                accuracy >= 0.85 && total >= 8 -> "Advanced"
                accuracy >= 0.65 -> "Intermediate"
                else -> "Beginner"
            }

            val topicsJson = profile.optJSONObject("topics")
            val conceptsJson = profile.optJSONObject("concepts")
            val topicScores = topicScores(topicsJson)
            val strengths = topicScores
                .filter { it.second > 0 }
                .sortedByDescending { it.second }
                .map { it.first }
                .take(3)
            val needsHelp = topicScores
                .filter { it.second < 0 }
                .sortedBy { it.second }
                .map { it.first }
                .take(3)

            val recentMistakes = buildList {
                val recentEvents = profile.optJSONArray("recentEvents") ?: return@buildList
                for (index in 0 until recentEvents.length()) {
                    val event = recentEvents.optJSONObject(index) ?: continue
                    if (event.optString("correctness") == "incorrect") {
                        add(displayTopic(event.optString("topic", "unknown")))
                    }
                }
            }.take(3)

            val conceptScores = mutableListOf<Pair<String, Int>>()
            val helpScores = mutableListOf<Pair<String, Int>>()
            conceptsJson?.objectKeys()?.forEach { key ->
                val concept = conceptsJson.optJSONObject(key) ?: return@forEach
                conceptScores += displayTopic(key) to
                    (concept.nonNegativeInt("correct") - concept.nonNegativeInt("incorrect"))
                helpScores += displayTopic(key) to concept.nonNegativeInt("helpRequests")
            }
            val struggleConcepts = conceptScores
                .filter { it.second < 0 }
                .sortedBy { it.second }
                .map { it.first }
                .take(3)
            val helpTopics = helpScores
                .filter { it.second > 0 }
                .sortedByDescending { it.second }
                .map { it.first }
                .take(3)

            val commonMistakes = buildList {
                val mistakes = profile.optJSONObject("mistakes") ?: return@buildList
                mistakes.objectKeys().forEach { key ->
                    val count = mistakes.nonNegativeInt(key)
                    if (count > 0) add(displayTopic(key) to count)
                }
            }.sortedByDescending { it.second }.map { it.first }.take(3)

            val rawOneLine = profile.optString("summaryOneLine", "")
            val rawThreeLine = profile.optString("summaryThreeLine", "")
            val rawSummary = profile.optString("summaryText", "")
            val summaryOneLine = rawOneLine.ifBlank {
                if (total == 0) {
                    "No activity yet."
                } else {
                    "$level level - ${String.format(Locale.ROOT, "%.0f", accuracy * 100)}% accuracy across $total attempts."
                }
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
                totalInteractions = profile.nonNegativeInt("totalInteractions"),
                correctCount = correct,
                incorrectCount = incorrect,
                hintsUsed = profile.nonNegativeInt("hintsUsed"),
                chatTurns = profile.nonNegativeInt("chatTurns"),
                strengths = strengths,
                needsHelp = needsHelp,
                recentMistakes = recentMistakes,
                struggleConcepts = struggleConcepts,
                commonMistakes = commonMistakes,
                helpTopics = helpTopics,
                skillScores = buildRadarScores(topicsJson, conceptsJson),
                lastUpdated = profile.optString("lastUpdated", ""),
                summaryText = rawSummary,
                summaryOneLine = summaryOneLine,
                summaryThreeLine = summaryThreeLine
            )
        } catch (_: Exception) {
            null
        }
    }

    fun hasInteractions(profile: AiProfile): Boolean {
        return profile.totalInteractions > 0 ||
            profile.correctCount > 0 ||
            profile.incorrectCount > 0 ||
            profile.hintsUsed > 0 ||
            profile.chatTurns > 0
    }

    private fun topicScores(json: JSONObject?): List<Pair<String, Int>> {
        if (json == null) return emptyList()
        return json.objectKeys().mapNotNull { key ->
            val values = json.optJSONObject(key) ?: return@mapNotNull null
            displayTopic(key) to
                (values.nonNegativeInt("correct") - values.nonNegativeInt("incorrect"))
        }
    }

    private fun buildRadarScores(topics: JSONObject?, concepts: JSONObject?): Map<String, Float> {
        val totals = radarAxes.associateWith { intArrayOf(0, 0) }.toMutableMap()

        fun add(json: JSONObject?) {
            if (json == null) return
            json.objectKeys().forEach { topic ->
                val axis = axisFor(topic) ?: return@forEach
                val stats = json.optJSONObject(topic) ?: return@forEach
                val bucket = totals.getValue(axis)
                bucket[0] += stats.nonNegativeInt("correct")
                bucket[1] += stats.nonNegativeInt("incorrect")
            }
        }

        add(topics)
        add(concepts)
        return radarAxes.associateWith { axis ->
            val score = totals.getValue(axis)
            val attempts = score[0] + score[1]
            if (attempts == 0) 0f else score[0].toFloat() / attempts.toFloat()
        }
    }

    private fun axisFor(rawTopic: String): String? {
        val topic = rawTopic
            .lowercase()
            .replace('_', ' ')
            .replace('-', ' ')
            .replace(':', ' ')
            .replace(Regex("\\s+"), " ")
            .trim()
        return when {
            listOf("data prep", "preprocess", "data clean", "missing value", "dataset inspection")
                .any(topic::contains) -> DATA_PREP
            listOf("neural network", "neural", "mlp").any(topic::contains) -> NEURAL_NETWORKS
            listOf("llm", "language model", "n gram", "ngram", "tf idf", "tfidf", "intent", "next token")
                .any(topic::contains) -> LLMS
            listOf("evaluation", "metric", "mean absolute", "mean squared", "mae", "mse", "r2", "r squared")
                .any(topic::contains) -> EVALUATION
            listOf("classification", "classifier", "logistic").any(topic::contains) -> CLASSIFICATION
            topic.contains("regression") -> REGRESSION
            else -> null
        }
    }

    private fun displayTopic(rawTopic: String): String {
        return rawTopic
            .removePrefix("ml:")
            .replace('_', ' ')
            .replace('-', ' ')
            .trim()
            .ifEmpty { "general" }
    }

    private fun JSONObject.nonNegativeInt(key: String): Int = optInt(key, 0).coerceAtLeast(0)

    private fun JSONObject.objectKeys(): List<String> = buildList {
        val iterator = keys()
        while (iterator.hasNext()) add(iterator.next())
    }
}
