package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class ParentChallengeCompletedPacket extends Packet {
    private String challengeId;
    private long childId;
    private String message;
    private String completedAt;

    public ParentChallengeCompletedPacket(final String challengeId, final long childId, final String message, final String completedAt) {
        super(68);
        this.challengeId = challengeId;
        this.childId = childId;
        this.message = message;
        this.completedAt = completedAt;
    }

    public ParentChallengeCompletedPacket() {
        super(68);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(challengeId == null ? "" : challengeId, buffer);
        buffer.putLong(childId);
        putString(message == null ? "" : message, buffer);
        putString(completedAt == null ? "" : completedAt, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.challengeId = readString(buffer);
        this.childId = buffer.getLong();
        this.message = readString(buffer);
        this.completedAt = readString(buffer);
    }

    public String getChallengeId() { return challengeId; }
    public long getChildId() { return childId; }
    public String getMessage() { return message; }
    public String getCompletedAt() { return completedAt; }
}
