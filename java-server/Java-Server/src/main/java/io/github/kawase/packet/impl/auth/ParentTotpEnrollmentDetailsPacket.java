package io.github.kawase.packet.impl.auth;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class ParentTotpEnrollmentDetailsPacket extends Packet {
    private String enrollmentId, secretBase32, otpAuthUri;

    public ParentTotpEnrollmentDetailsPacket(
            final String enrollmentId,
            final String secretBase32,
            final String otpAuthUri) {
        super(84);
        this.enrollmentId = enrollmentId;
        this.secretBase32 = secretBase32;
        this.otpAuthUri = otpAuthUri;
    }

    public ParentTotpEnrollmentDetailsPacket() {
        super(84);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(enrollmentId == null ? "" : enrollmentId, buffer);
        putString(secretBase32 == null ? "" : secretBase32, buffer);
        putString(otpAuthUri == null ? "" : otpAuthUri, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        enrollmentId = readString(buffer);
        secretBase32 = readString(buffer);
        otpAuthUri = readString(buffer);
    }
}
