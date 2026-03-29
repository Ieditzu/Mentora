package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class FetchGoalsPacket extends Packet {
    private long childId;

    public FetchGoalsPacket(final long childId) {
        super(0x0D);
        this.childId = childId;
    }

    public FetchGoalsPacket() {
        super(0x0D);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
    }

    public long getChildId() {
        return childId;
    }
}
