package io.github.kawase.packet.impl.machinelearning;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class MachineLearningProblemsResponsePacket extends Packet {
    private String requestId, problemsJson;

    public MachineLearningProblemsResponsePacket(final String requestId, final String problemsJson) {
        super(78);
        this.requestId = requestId;
        this.problemsJson = problemsJson;
    }

    public MachineLearningProblemsResponsePacket() {
        super(78);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(requestId == null ? "" : requestId, buffer);
        putString(problemsJson == null ? "{}" : problemsJson, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        requestId = readString(buffer);
        problemsJson = readString(buffer);
    }
}
