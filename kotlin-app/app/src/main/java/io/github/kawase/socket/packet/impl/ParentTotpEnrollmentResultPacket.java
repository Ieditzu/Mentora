package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.exceptions.PacketException;
import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.List;

public class ParentTotpEnrollmentResultPacket extends Packet {
    private boolean success;
    private String message;
    private List<String> recoveryCodes = new ArrayList<>();

    public ParentTotpEnrollmentResultPacket(
            final boolean success,
            final String message,
            final List<String> recoveryCodes) {
        super(86);
        this.success = success;
        this.message = message;
        this.recoveryCodes = recoveryCodes == null ? List.of() : recoveryCodes;
    }

    public ParentTotpEnrollmentResultPacket() {
        super(86);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.put((byte) (success ? 1 : 0));
        putString(message == null ? "" : message, buffer);
        buffer.putInt(recoveryCodes.size());
        for (final String recoveryCode : recoveryCodes)
            putString(recoveryCode == null ? "" : recoveryCode, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        success = buffer.get() == 1;
        message = readString(buffer);
        final int recoveryCodeCount = buffer.getInt();
        if (recoveryCodeCount < 0 || recoveryCodeCount > 10_000)
            throw new PacketException("Invalid recovery code count");
        recoveryCodes = new ArrayList<>(recoveryCodeCount);
        for (int index = 0; index < recoveryCodeCount; index++)
            recoveryCodes.add(readString(buffer));
    }

    public boolean isSuccess() {
        return success;
    }

    public String getMessage() {
        return message;
    }

    public List<String> getRecoveryCodes() {
        return recoveryCodes;
    }
}
