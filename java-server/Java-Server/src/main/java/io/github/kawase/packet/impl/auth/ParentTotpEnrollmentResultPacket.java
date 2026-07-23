package io.github.kawase.packet.impl.auth;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.List;

@Getter
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
        this.recoveryCodes = recoveryCodes == null ? List.of() : List.copyOf(recoveryCodes);
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
            putString(recoveryCode, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        success = buffer.get() == 1;
        message = readString(buffer);
        final int count = buffer.getInt();
        recoveryCodes = new ArrayList<>(Math.max(0, count));
        for (int index = 0; index < count; index++)
            recoveryCodes.add(readString(buffer));
    }
}
