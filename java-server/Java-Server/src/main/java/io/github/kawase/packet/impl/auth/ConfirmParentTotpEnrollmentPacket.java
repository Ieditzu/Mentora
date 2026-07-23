package io.github.kawase.packet.impl.auth;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class ConfirmParentTotpEnrollmentPacket extends Packet {
    private String enrollmentId, code;

    public ConfirmParentTotpEnrollmentPacket(final String enrollmentId, final String code) {
        super(85);
        this.enrollmentId = enrollmentId;
        this.code = code;
    }

    public ConfirmParentTotpEnrollmentPacket() {
        super(85);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(enrollmentId == null ? "" : enrollmentId, buffer);
        putString(code == null ? "" : code, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        enrollmentId = readString(buffer);
        code = readString(buffer);
    }
}
