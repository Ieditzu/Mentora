package io.github.kawase.packet.impl.game;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
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
}
