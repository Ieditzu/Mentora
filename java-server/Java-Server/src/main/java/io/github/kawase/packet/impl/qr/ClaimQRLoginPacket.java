package io.github.kawase.packet.impl.qr;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class ClaimQRLoginPacket extends Packet {
    private String token;
    private long childId;

    public ClaimQRLoginPacket(final String token, final long childId) {
        super(21);
        this.token = token;
        this.childId = childId;
    }

    public ClaimQRLoginPacket() {
        super(21);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(token == null ? "" : token, buffer);
        buffer.putLong(childId);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.token = readString(buffer);
        this.childId = buffer.getLong();
    }
}
