package io.github.kawase.packet.impl.auth;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class ParentSecondFactorRequiredPacket extends Packet {
    private String challengeId;
    private int expiresInSeconds;
    private boolean recoveryAllowed;

    public ParentSecondFactorRequiredPacket(
            final String challengeId,
            final int expiresInSeconds,
            final boolean recoveryAllowed) {
        super(81);
        this.challengeId = challengeId;
        this.expiresInSeconds = expiresInSeconds;
        this.recoveryAllowed = recoveryAllowed;
    }

    public ParentSecondFactorRequiredPacket() {
        super(81);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(challengeId == null ? "" : challengeId, buffer);
        buffer.putInt(expiresInSeconds);
        buffer.put((byte) (recoveryAllowed ? 1 : 0));
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        challengeId = readString(buffer);
        expiresInSeconds = buffer.getInt();
        recoveryAllowed = buffer.get() == 1;
    }
}
