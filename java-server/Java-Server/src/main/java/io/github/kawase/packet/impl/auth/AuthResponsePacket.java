package io.github.kawase.packet.impl.auth;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class AuthResponsePacket extends Packet {
    private boolean success;
    private long parentId;
    private String message;
    private String parentPfp;

    public AuthResponsePacket(final boolean success, final long parentId, final String message, final String parentPfp) {
        super(10);
        this.success = success;
        this.parentId = parentId;
        this.message = message;
        this.parentPfp = parentPfp;
    }

    public AuthResponsePacket() {
        super(10);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.put((byte) (success ? 1 : 0));
        buffer.putLong(parentId);
        putString(message == null ? "" : message, buffer);
        putString(parentPfp == null ? "" : parentPfp, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.success = buffer.get() == 1;
        this.parentId = buffer.getLong();
        this.message = readString(buffer);
        this.parentPfp = readString(buffer);
    }
}
