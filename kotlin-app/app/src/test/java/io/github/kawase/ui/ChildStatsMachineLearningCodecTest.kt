package io.github.kawase.ui

import io.github.kawase.shared.protocol.FetchChildStatsResponsePacket
import io.github.kawase.shared.protocol.PacketFactory
import org.junit.Assert.assertEquals
import org.junit.Test

class ChildStatsMachineLearningCodecTest {
    @Test
    fun `packet 24 preserves the optional ML profile as raw JSON`() {
        val gameStatsJson =
            """{"aiProfileCpp":{"correctCount":2},"aiProfileMachineLearning":{"totalInteractions":1,"topics":{"ml:regression":{"correct":1,"incorrect":0}}}}"""
        val packet = FetchChildStatsResponsePacket(
            name = "Ada",
            totalPoints = 125,
            gameStatsJson = gameStatsJson,
            streak = 4,
            completedTaskCount = 3,
            totalTaskCount = 9
        )

        val decoded = PacketFactory.decode(PacketFactory.encode(packet))

        assertEquals(packet, decoded)
        assertEquals(gameStatsJson, (decoded as FetchChildStatsResponsePacket).gameStatsJson)
    }
}
