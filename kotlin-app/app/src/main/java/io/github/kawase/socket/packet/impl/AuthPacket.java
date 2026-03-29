package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;


import java.nio.ByteBuffer;

public class AuthPacket extends Packet {
    private String emailHash, passwordHash;

    // hash these before sending them off, todo: dont forget.
    public AuthPacket(final String emailHash, final String passwordHash) {
        super(0x02);

        this.emailHash = emailHash;
        this.passwordHash = passwordHash;
    }

    public AuthPacket() {
        super(0x02);
    }

    @Override
    public void write(final ByteBuffer buffer) {
        putString(emailHash, buffer);
        putString(passwordHash, buffer);
    }

    @Override
    public void read(final ByteBuffer buffer) {
        this.emailHash = readString(buffer);
        this.passwordHash = readString(buffer);
    }

    public String getEmailHash() {
        return emailHash;
    }

    public String getPasswordHash() {
        return passwordHash;
    }
}