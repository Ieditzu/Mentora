package io.github.kawase.packet.impl.child;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class UpdatePfpPacket extends Packet {
    private long childId; // -1 for parent
    private String base64Pfp;

    public UpdatePfpPacket(final long childId, final String base64Pfp) {
        super(26);
        this.childId = childId;
        this.base64Pfp = base64Pfp;
    }

    public UpdatePfpPacket() {
        super(26);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
        putString(base64Pfp == null ? "" : base64Pfp, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
        this.base64Pfp = readString(buffer);
    }
}
