package io.github.kawase.packet.impl.machinelearning;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class SubmitMachineLearningSolutionPacket extends Packet {
    private String requestId, problemSlug, sourceCode;

    public SubmitMachineLearningSolutionPacket(final String requestId, final String problemSlug, final String sourceCode) {
        super(79);
        this.requestId = requestId;
        this.problemSlug = problemSlug;
        this.sourceCode = sourceCode;
    }

    public SubmitMachineLearningSolutionPacket() {
        super(79);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(requestId == null ? "" : requestId, buffer);
        putString(problemSlug == null ? "" : problemSlug, buffer);
        putString(sourceCode == null ? "" : sourceCode, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        requestId = readString(buffer);
        problemSlug = readString(buffer);
        sourceCode = readString(buffer);
    }
}
