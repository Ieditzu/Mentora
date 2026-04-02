package io.github.kawase.packet.impl.child;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class DevLoginAsChildPacket extends Packet {
    private long childId;

    public DevLoginAsChildPacket(final long childId) {
        super(43);
        this.childId = childId;
    }

    public DevLoginAsChildPacket() {
        super(43);
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
