package io.github.kawase.packet.impl.machinelearning;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class FetchMachineLearningProblemsPacket extends Packet {
    private String requestId;

    public FetchMachineLearningProblemsPacket(final String requestId) {
        super(77);
        this.requestId = requestId;
    }

    public FetchMachineLearningProblemsPacket() {
        super(77);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(requestId == null ? "" : requestId, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        requestId = readString(buffer);
    }
}
