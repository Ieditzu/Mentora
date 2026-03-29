package io.github.kawase.packet.impl.qr;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class ChildAuthResponsePacket extends Packet {
    private boolean success;
    private long childId;
    private String childName;
    private String sessionToken;

    public ChildAuthResponsePacket(final boolean success, final long childId, final String childName, final String sessionToken) {
        super(22);
        this.success = success;
        this.childId = childId;
        this.childName = childName;
        this.sessionToken = sessionToken;
    }

    public ChildAuthResponsePacket() {
        super(22);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.put((byte) (success ? 1 : 0));
        buffer.putLong(childId);
        putString(childName == null ? "" : childName, buffer);
        putString(sessionToken == null ? "" : sessionToken, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.success = buffer.get() == 1;
        this.childId = buffer.getLong();
        this.childName = readString(buffer);
        this.sessionToken = readString(buffer);
    }
}
