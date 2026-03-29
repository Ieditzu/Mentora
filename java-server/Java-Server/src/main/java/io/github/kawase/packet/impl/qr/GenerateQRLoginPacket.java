package io.github.kawase.packet.impl.qr;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class GenerateQRLoginPacket extends Packet {

    public GenerateQRLoginPacket() {
        super(19);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
    }

    @Override
    protected void read(final ByteBuffer buffer) {
    }
}
