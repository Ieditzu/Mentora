package io.github.kawase.packet.impl.child;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class FetchChildStatsByParentPacket extends Packet {
    private long childId;

    public FetchChildStatsByParentPacket(final long childId) {
        super(32);
        this.childId = childId;
    }

    public FetchChildStatsByParentPacket() {
        super(32);
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
