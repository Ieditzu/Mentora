package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class VerifyParentSecondFactorPacket extends Packet {
    private String challengeId, code;

    public VerifyParentSecondFactorPacket(final String challengeId, final String code) {
        super(82);
        this.challengeId = challengeId;
        this.code = code;
    }

    public VerifyParentSecondFactorPacket() {
        super(82);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(challengeId == null ? "" : challengeId, buffer);
        putString(code == null ? "" : code, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        challengeId = readString(buffer);
        code = readString(buffer);
    }

    public String getChallengeId() {
        return challengeId;
    }

    public String getCode() {
        return code;
    }
}
