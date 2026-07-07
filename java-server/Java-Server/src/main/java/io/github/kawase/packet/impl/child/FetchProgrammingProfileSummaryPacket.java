package io.github.kawase.packet.impl.child;

import io.github.kawase.packet.Packet;

import java.nio.ByteBuffer;

public class FetchProgrammingProfileSummaryPacket extends Packet {
    public FetchProgrammingProfileSummaryPacket() {
        super(71);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
    }

    @Override
    protected void read(final ByteBuffer buffer) {
    }
}
