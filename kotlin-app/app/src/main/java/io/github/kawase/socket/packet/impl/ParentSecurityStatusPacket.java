package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class ParentSecurityStatusPacket extends Packet {
    private boolean totpEnabled;
    private int recoveryCodesRemaining;

    public ParentSecurityStatusPacket(final boolean totpEnabled, final int recoveryCodesRemaining) {
        super(89);
        this.totpEnabled = totpEnabled;
        this.recoveryCodesRemaining = recoveryCodesRemaining;
    }

    public ParentSecurityStatusPacket() {
        super(89);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.put((byte) (totpEnabled ? 1 : 0));
        buffer.putInt(recoveryCodesRemaining);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        totpEnabled = buffer.get() == 1;
        recoveryCodesRemaining = buffer.getInt();
    }

    public boolean isTotpEnabled() {
        return totpEnabled;
    }

    public int getRecoveryCodesRemaining() {
        return recoveryCodesRemaining;
    }
}
