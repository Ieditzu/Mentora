package io.github.kawase.packet.impl.qr;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class VerifySessionPacket extends Packet {
    private long childId;
    private String sessionToken;

    public VerifySessionPacket(final long childId, final String sessionToken) {
        super(25);
        this.childId = childId;
        this.sessionToken = sessionToken;
    }

    public VerifySessionPacket() {
        super(25);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
        putString(sessionToken == null ? "" : sessionToken, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
        this.sessionToken = readString(buffer);
    }
}
