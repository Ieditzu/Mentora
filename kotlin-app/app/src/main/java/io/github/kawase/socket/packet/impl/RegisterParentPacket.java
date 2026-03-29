package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class RegisterParentPacket extends Packet {
    private String email;
    private String passwordHash;

    public RegisterParentPacket(final String email, final String passwordHash) {
        super(0x03);
        this.email = email;
        this.passwordHash = passwordHash;
    }

    public RegisterParentPacket() {
        super(0x03);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(email, buffer);
        putString(passwordHash, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.email = readString(buffer);
        this.passwordHash = readString(buffer);
    }

    public String getEmail() {
        return email;
    }

    public String getPasswordHash() {
        return passwordHash;
    }
}
