package io.github.kawase.packet.impl.game;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
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
}
