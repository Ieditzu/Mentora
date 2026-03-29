package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;
import java.nio.ByteBuffer;

public class RemoveChildPacket extends Packet {
    private long childId;

    public RemoveChildPacket(final long childId) {
        super(27);
        this.childId = childId;
    }

    public RemoveChildPacket() {
        super(27);
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
