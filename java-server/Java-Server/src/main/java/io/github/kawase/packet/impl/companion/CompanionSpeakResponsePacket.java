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
    private String sourceTranscript;

    public CompanionSpeakResponsePacket(final String line, final String emotion) {
        this(line, emotion, "");
    }

    public CompanionSpeakResponsePacket(final String line, final String emotion, final String sourceTranscript) {
        super(48);
        this.line = line;
        this.emotion = emotion;
        this.sourceTranscript = sourceTranscript;
    }

    public CompanionSpeakResponsePacket() {
        super(48);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(line == null ? "" : line, buffer);
        putString(emotion == null ? "encouraging" : emotion, buffer);
        putString(sourceTranscript == null ? "" : sourceTranscript, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.line = readString(buffer);
        this.emotion = readString(buffer);
        this.sourceTranscript = buffer.hasRemaining() ? readString(buffer) : "";
    }
}
