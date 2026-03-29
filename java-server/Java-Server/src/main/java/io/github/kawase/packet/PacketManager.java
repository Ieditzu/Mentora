package io.github.kawase.packet;

import io.github.kawase.exceptions.PacketException;
import io.github.kawase.packet.impl.ai.AiResponsePacket;
import io.github.kawase.packet.impl.ai.AskAiPacket;
import io.github.kawase.packet.impl.auth.*;
import io.github.kawase.packet.impl.child.*;
import io.github.kawase.packet.impl.core.*;
import io.github.kawase.packet.impl.game.*;
import io.github.kawase.packet.impl.language.ExecuteCPPCodePacket;
import io.github.kawase.packet.impl.language.ExecuteCPPCodeResponsePacket;
import io.github.kawase.packet.impl.language.ExecutePythonCodePacket;
import io.github.kawase.packet.impl.language.ExecutePythonCodeResponsePacket;
import io.github.kawase.packet.impl.qr.*;

public class PacketManager {
    public Packet createPacket(final int id) {
        return switch (id) {
            case 1 -> new HandShakePacket();
            case 2 -> new AuthPacket();
            case 3 -> new RegisterParentPacket();
            case 4 -> new AddChildPacket();
            case 5 -> new AddGoalPacket();
            case 8 -> new CompleteTaskPacket();
            case 9 -> new ActionResponsePacket();
            case 10 -> new AuthResponsePacket();
            case 11 -> new FetchTasksPacket();
            case 12 -> new FetchTasksResponsePacket();
            case 13 -> new FetchGoalsPacket();
            case 14 -> new FetchGoalsResponsePacket();
            case 15 -> new FetchChildrenPacket();
            case 16 -> new FetchChildrenResponsePacket();
            case 17 -> new FetchCompletedTasksPacket();
            case 18 -> new FetchCompletedTasksResponsePacket();
            case 19 -> new GenerateQRLoginPacket();
            case 20 -> new QRLoginResponsePacket();
            case 21 -> new ClaimQRLoginPacket();
            case 22 -> new ChildAuthResponsePacket();
            case 23 -> new FetchChildStatsPacket();
            case 24 -> new FetchChildStatsResponsePacket();
            case 25 -> new VerifySessionPacket();
            case 26 -> new UpdatePfpPacket();
            case 27 -> new RemoveChildPacket();
            case 28 -> new ExecuteCPPCodePacket();
            case 29 -> new ExecuteCPPCodeResponsePacket();
            case 30 -> new AskAiPacket();
            case 31 -> new AiResponsePacket();
            case 32 -> new FetchChildStatsByParentPacket();
            case 33 -> new RecordLearningEventPacket();
            case 34 -> new ExecutePythonCodePacket();
            case 35 -> new ExecutePythonCodeResponsePacket();

            default -> throw new PacketException("Unknown packet ID: " + id);
        };
    }
}
