package io.github.kawase.packet.impl.qr;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class QRLoginResponsePacket extends Packet {
    private String token;

    public QRLoginResponsePacket(final String token) {
        super(20);
        this.token = token;
    }

    public QRLoginResponsePacket() {
        super(20);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(token == null ? "" : token, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.token = readString(buffer);
    }
}
