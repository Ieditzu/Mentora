package io.github.kawase.packet.impl.companion;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

/**
 * Server sends back a short companion line and an emotion tag.
 * Emotion values: "happy", "encouraging", "concerned", "excited", "thinking"
 */
@Getter
public class CompanionSpeakResponsePacket extends Packet {
    private String line;
    private String emotion;

    public CompanionSpeakResponsePacket(final String line, final String emotion) {
        super(48);
        this.line = line;
        this.emotion = emotion;
    }

    public CompanionSpeakResponsePacket() {
        super(48);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(line == null ? "" : line, buffer);
        putString(emotion == null ? "encouraging" : emotion, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.line = readString(buffer);
        this.emotion = readString(buffer);
    }
}
