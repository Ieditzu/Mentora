package io.github.kawase.packet.impl.companion;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class CompanionVoiceTextPacket extends Packet {
    private String transcript;
    private String context;

    public CompanionVoiceTextPacket(final String transcript, final String context) {
        super(58);
        this.transcript = transcript;
        this.context = context;
    }

    public CompanionVoiceTextPacket() {
        super(58);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(transcript == null ? "" : transcript, buffer);
        putString(context == null ? "" : context, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.transcript = readString(buffer);
        this.context = readString(buffer);
    }
}
