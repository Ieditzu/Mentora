package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class FetchCompletedTasksPacket extends Packet {
    private long childId;

    public FetchCompletedTasksPacket(final long childId) {
        super(0x11);
        this.childId = childId;
    }

    public FetchCompletedTasksPacket() {
        super(0x11);
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
