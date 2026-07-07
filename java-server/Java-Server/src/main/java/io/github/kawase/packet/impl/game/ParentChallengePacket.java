package io.github.kawase.packet.impl.game;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class ParentChallengePacket extends Packet {
    private String challengeId;
    private long childId;
    private String message;
    private String sentAt;

    public ParentChallengePacket(final String challengeId, final long childId, final String message, final String sentAt) {
        super(67);
        this.challengeId = challengeId;
        this.childId = childId;
        this.message = message;
        this.sentAt = sentAt;
    }

    public ParentChallengePacket() {
        super(67);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(challengeId == null ? "" : challengeId, buffer);
        buffer.putLong(childId);
        putString(message == null ? "" : message, buffer);
        putString(sentAt == null ? "" : sentAt, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.challengeId = readString(buffer);
        this.childId = buffer.getLong();
        this.message = readString(buffer);
        this.sentAt = readString(buffer);
    }
}
