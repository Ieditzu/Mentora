package io.github.kawase.ui

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class MachineLearningProfileParserTest {
    @Test
    fun `parses all ML axes with per-axis accuracy`() {
        val profile = MachineLearningProfileParser.parse(
            """
            {
              "aiProfileCpp": {"correctCount": 99, "incorrectCount": 0},
              "aiProfileMachineLearning": {
                "totalInteractions": 24,
                "correctCount": 17,
                "incorrectCount": 7,
                "hintsUsed": 2,
                "topics": {
                  "ml:data-prep": {"correct": 3, "incorrect": 1},
                  "ml:regression": {"correct": 2, "incorrect": 2},
                  "ml:classification": {"correct": 4, "incorrect": 1},
                  "ml:evaluation": {"correct": 1, "incorrect": 3},
                  "ml:neural-networks": {"correct": 5, "incorrect": 0},
                  "ml:llm": {"correct": 2, "incorrect": 3}
                },
                "concepts": {
                  "data_cleaning": {"correct": 1, "incorrect": 0},
                  "linear-regression": {"correct": 1, "incorrect": 1},
                  "logistic_classifier": {"correct": 0, "incorrect": 1},
                  "mae_metric": {"correct": 1, "incorrect": 0},
                  "mlp": {"correct": 0, "incorrect": 1},
                  "next-token": {"correct": 1, "incorrect": 0}
                }
              }
            }
            """.trimIndent()
        )

        requireNotNull(profile)
        assertTrue(MachineLearningProfileParser.hasInteractions(profile))
        assertEquals(0.8f, profile.skillScores.getValue(MachineLearningProfileParser.DATA_PREP), 0.0001f)
        assertEquals(0.5f, profile.skillScores.getValue(MachineLearningProfileParser.REGRESSION), 0.0001f)
        assertEquals(2f / 3f, profile.skillScores.getValue(MachineLearningProfileParser.CLASSIFICATION), 0.0001f)
        assertEquals(0.4f, profile.skillScores.getValue(MachineLearningProfileParser.EVALUATION), 0.0001f)
        assertEquals(5f / 6f, profile.skillScores.getValue(MachineLearningProfileParser.NEURAL_NETWORKS), 0.0001f)
        assertEquals(0.5f, profile.skillScores.getValue(MachineLearningProfileParser.LLMS), 0.0001f)
    }

    @Test
    fun `missing or malformed ML data is isolated`() {
        assertNull(MachineLearningProfileParser.parse("""{"aiProfileCpp":{"correctCount":2}}"""))
        assertNull(MachineLearningProfileParser.parse("""{"aiProfileMachineLearning":"invalid"}"""))
        assertNull(MachineLearningProfileParser.parse("not-json"))
    }

    @Test
    fun `zero activity profile remains hidden`() {
        val profile = MachineLearningProfileParser.parse("""{"aiProfileMachineLearning":{}}""")

        requireNotNull(profile)
        assertFalse(MachineLearningProfileParser.hasInteractions(profile))
        MachineLearningProfileParser.radarAxes.forEach { axis ->
            assertEquals(0f, profile.skillScores.getValue(axis), 0f)
        }
    }
}
