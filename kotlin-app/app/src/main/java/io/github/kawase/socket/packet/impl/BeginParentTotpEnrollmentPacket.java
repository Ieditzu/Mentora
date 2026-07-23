package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class BeginParentTotpEnrollmentPacket extends Packet {
    private String passwordHash;

    public BeginParentTotpEnrollmentPacket(final String passwordHash) {
        super(83);
        this.passwordHash = passwordHash;
    }

    public BeginParentTotpEnrollmentPacket() {
        super(83);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(passwordHash == null ? "" : passwordHash, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        passwordHash = readString(buffer);
    }

    public String getPasswordHash() {
        return passwordHash;
    }
}
