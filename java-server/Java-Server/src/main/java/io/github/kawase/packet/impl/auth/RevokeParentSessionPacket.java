package io.github.kawase.packet.impl.auth;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class RevokeParentSessionPacket extends Packet {
    private String sessionToken;
    private boolean revokeAll;

    public RevokeParentSessionPacket(final String sessionToken, final boolean revokeAll) {
        super(92);
        this.sessionToken = sessionToken;
        this.revokeAll = revokeAll;
    }

    public RevokeParentSessionPacket() {
        super(92);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(sessionToken == null ? "" : sessionToken, buffer);
        buffer.put((byte) (revokeAll ? 1 : 0));
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        sessionToken = readString(buffer);
        revokeAll = buffer.get() == 1;
    }
}
