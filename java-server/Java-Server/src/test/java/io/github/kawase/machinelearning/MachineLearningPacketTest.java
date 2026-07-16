package io.github.kawase.machinelearning;

import io.github.kawase.packet.Packet;
import io.github.kawase.packet.PacketManager;
import io.github.kawase.packet.impl.machinelearning.SubmitMachineLearningSolutionPacket;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;

class MachineLearningPacketTest {
    @Test
    void submissionRoundTripsThroughEncryptedProtocol() throws Exception {
        final SubmitMachineLearningSolutionPacket original = new SubmitMachineLearningSolutionPacket(
                "request-42", "easy-line-of-best-fit", "def solve(train_path, test_path):\n    return [13, 15, 17]"
        );

        final Packet decoded = Packet.construct(original.encode(), new PacketManager());
        final SubmitMachineLearningSolutionPacket submission = assertInstanceOf(SubmitMachineLearningSolutionPacket.class, decoded);

        assertEquals("request-42", submission.getRequestId());
        assertEquals("easy-line-of-best-fit", submission.getProblemSlug());
        assertEquals(original.getSourceCode(), submission.getSourceCode());
    }
}
