package io.github.kawase.shared.protocol

/** Encodes and decodes the plaintext protocol payload: four-byte packet ID followed by its body. */
object PacketFactory {
    fun encode(packet: MentoraPacket): ByteArray = ByteCursor.writer().also {
        it.writeInt(packet.id)
        packet.writeBody(it)
    }.toByteArray()

    fun decode(payload: ByteArray): MentoraPacket {
        val cursor = ByteCursor.reader(payload)
        val packet = when (val id = cursor.readInt()) {
            1 -> HandshakePacket(cursor.readString())
            2 -> AuthPacket(cursor.readString(), cursor.readString())
            3 -> RegisterParentPacket(cursor.readString(), cursor.readString())
            4 -> AddChildPacket(cursor.readString())
            5 -> AddGoalPacket(cursor.readLong(), cursor.readString(), cursor.readString(), cursor.readInt(), cursor.readLong())
            8 -> CompleteTaskPacket(cursor.readLong(), cursor.readLong())
            9 -> ActionResponsePacket(cursor.readInt(), cursor.readBoolean(), cursor.readString(), cursor.readLong())
            10 -> AuthResponsePacket(cursor.readBoolean(), cursor.readLong(), cursor.readString(), cursor.readString())
            11 -> FetchTasksPacket()
            12 -> FetchTasksResponsePacket(List(cursor.readCollectionSize()) { TaskPayload(cursor.readLong(), cursor.readString(), cursor.readInt()) })
            13 -> FetchGoalsPacket(cursor.readLong())
            14 -> FetchGoalsResponsePacket(List(cursor.readCollectionSize()) { GoalPayload(cursor.readLong(), cursor.readString(), cursor.readString(), cursor.readBoolean(), cursor.readInt(), cursor.readLong()) })
            15 -> FetchChildrenPacket()
            16 -> FetchChildrenResponsePacket(List(cursor.readCollectionSize()) { ChildPayload(cursor.readLong(), cursor.readString(), cursor.readInt(), cursor.readBoolean(), cursor.readString()) })
            17 -> FetchCompletedTasksPacket(cursor.readLong())
            18 -> FetchCompletedTasksResponsePacket(List(cursor.readCollectionSize()) { CompletedTaskPayload(cursor.readLong(), cursor.readString(), cursor.readInt(), cursor.readString()) })
            21 -> ClaimQRLoginPacket(cursor.readString(), cursor.readLong())
            24 -> FetchChildStatsResponsePacket(cursor.readString(), cursor.readInt(), cursor.readString(), cursor.readInt(), cursor.readInt(), cursor.readInt())
            26 -> UpdatePfpPacket(cursor.readLong(), cursor.readString())
            27 -> RemoveChildPacket(cursor.readLong())
            30 -> AskAiPacket(cursor.readString(), cursor.readString())
            31 -> AiResponsePacket(cursor.readString())
            32 -> FetchChildStatsByParentPacket(cursor.readLong())
            64 -> SubscribeLiveSessionPacket(cursor.readLong(), cursor.readBoolean())
            65 -> LiveSessionUpdatePacket(cursor.readLong(), cursor.readString(), cursor.readBoolean(), cursor.readString(), cursor.readString(), cursor.readInt(), cursor.readBoolean(), cursor.readString(), cursor.readString())
            66 -> SendParentChallengePacket(cursor.readLong(), cursor.readString())
            67 -> ParentChallengePacket(cursor.readString(), cursor.readLong(), cursor.readString(), cursor.readString())
            68 -> ParentChallengeCompletedPacket(cursor.readString(), cursor.readLong(), cursor.readString(), cursor.readString())
            69 -> FetchWeeklyReportPacket(cursor.readLong())
            70 -> WeeklyReportResponsePacket(cursor.readLong(), cursor.readString(), cursor.readString(), cursor.readString(), cursor.readString(), cursor.readBoolean())
            76 -> SetClientLanguagePacket(cursor.readString())
            else -> throw ProtocolException("Unknown packet ID: $id")
        }
        if (!cursor.isAtEnd) throw ProtocolException("Unexpected trailing data in packet ${packet.id}")
        return packet
    }
}
