package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

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

    public String getChallengeId() {
        return challengeId;
    }

    public int getExpiresInSeconds() {
        return expiresInSeconds;
    }

    public boolean isRecoveryAllowed() {
        return recoveryAllowed;
    }
}
