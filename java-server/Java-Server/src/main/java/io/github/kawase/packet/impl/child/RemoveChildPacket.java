package io.github.kawase.packet.impl.child;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class RemoveChildPacket extends Packet {
    private long childId;

    public RemoveChildPacket(final long childId) {
        super(27);
        this.childId = childId;
    }

    public RemoveChildPacket() {
        super(27);
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
