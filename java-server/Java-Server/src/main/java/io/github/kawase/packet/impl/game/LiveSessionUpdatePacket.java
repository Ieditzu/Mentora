package io.github.kawase.packet.impl.game;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class LiveSessionUpdatePacket extends Packet {
    private long childId;
    private String childName;
    private boolean online;
    private String padName;
    private String codeText;
    private int attemptCount;
    private boolean hintRequested;
    private String status;
    private String updatedAt;

    public LiveSessionUpdatePacket(final long childId, final String childName, final boolean online,
                                   final String padName, final String codeText, final int attemptCount,
                                   final boolean hintRequested, final String status, final String updatedAt) {
        super(65);
        this.childId = childId;
        this.childName = childName;
        this.online = online;
        this.padName = padName;
        this.codeText = codeText;
        this.attemptCount = attemptCount;
        this.hintRequested = hintRequested;
        this.status = status;
        this.updatedAt = updatedAt;
    }

    public LiveSessionUpdatePacket() {
        super(65);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
        putString(childName == null ? "" : childName, buffer);
        buffer.put((byte) (online ? 1 : 0));
        putString(padName == null ? "" : padName, buffer);
        putString(codeText == null ? "" : codeText, buffer);
        buffer.putInt(attemptCount);
        buffer.put((byte) (hintRequested ? 1 : 0));
        putString(status == null ? "" : status, buffer);
        putString(updatedAt == null ? "" : updatedAt, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
        this.childName = readString(buffer);
        this.online = buffer.get() == 1;
        this.padName = readString(buffer);
        this.codeText = readString(buffer);
        this.attemptCount = buffer.getInt();
        this.hintRequested = buffer.get() == 1;
        this.status = readString(buffer);
        this.updatedAt = readString(buffer);
    }
}
