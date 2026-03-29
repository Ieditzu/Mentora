package io.github.kawase.packet.impl.child;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class FetchChildrenPacket extends Packet {
    public FetchChildrenPacket() {
        super(0x0F);
    }

    @Override
    protected void write(final ByteBuffer buffer) {}

    @Override
    protected void read(final ByteBuffer buffer) {}
}
