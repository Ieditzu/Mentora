package io.github.kawase.packet.impl.machinelearning;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class MachineLearningSubmissionResultPacket extends Packet {
    private String requestId, resultJson;

    public MachineLearningSubmissionResultPacket(final String requestId, final String resultJson) {
        super(80);
        this.requestId = requestId;
        this.resultJson = resultJson;
    }

    public MachineLearningSubmissionResultPacket() {
        super(80);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(requestId == null ? "" : requestId, buffer);
        putString(resultJson == null ? "{}" : resultJson, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        requestId = readString(buffer);
        resultJson = readString(buffer);
    }
}
