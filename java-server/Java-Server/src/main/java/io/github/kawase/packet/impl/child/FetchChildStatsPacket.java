package io.github.kawase.packet.impl.child;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class FetchChildStatsPacket extends Packet {
    public FetchChildStatsPacket() {
        super(23);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
    }

    @Override
    protected void read(final ByteBuffer buffer) {
    }
}
