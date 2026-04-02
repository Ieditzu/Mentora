package io.github.kawase.packet.impl.child;

import io.github.kawase.packet.Packet;
import java.nio.ByteBuffer;

public class FetchAllChildrenPacket extends Packet {
    public FetchAllChildrenPacket() {
        super(41);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
    }

    @Override
    protected void read(final ByteBuffer buffer) {
    }
}
