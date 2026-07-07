package io.github.kawase.packet.impl.game;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class SendParentChallengePacket extends Packet {
    private long childId;
    private String message;

    public SendParentChallengePacket(final long childId, final String message) {
        super(66);
        this.childId = childId;
        this.message = message;
    }

    public SendParentChallengePacket() {
        super(66);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
        putString(message == null ? "" : message, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
        this.message = readString(buffer);
    }
}
