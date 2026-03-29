package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;
import java.nio.ByteBuffer;

public class FetchChildStatsByParentPacket extends Packet {
    private long childId;

    public FetchChildStatsByParentPacket(final long childId) {
        super(32);
        this.childId = childId;
    }

    public FetchChildStatsByParentPacket() {
        super(32);
    }

    public long getChildId() {
        return childId;
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
    }
}
