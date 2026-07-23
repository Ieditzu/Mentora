package io.github.kawase.packet.impl.auth;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class DisableParentTotpPacket extends Packet {
    private String passwordHash, code;

    public DisableParentTotpPacket(final String passwordHash, final String code) {
        super(87);
        this.passwordHash = passwordHash;
        this.code = code;
    }

    public DisableParentTotpPacket() {
        super(87);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(passwordHash == null ? "" : passwordHash, buffer);
        putString(code == null ? "" : code, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        passwordHash = readString(buffer);
        code = readString(buffer);
    }
}
