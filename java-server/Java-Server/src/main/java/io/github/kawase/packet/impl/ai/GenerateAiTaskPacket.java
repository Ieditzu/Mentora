package io.github.kawase.packet.impl.ai;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class GenerateAiTaskPacket extends Packet {
    private String language; // "python" or "cpp"

    public GenerateAiTaskPacket(final String language) {
        super(45);
        this.language = language;
    }

    public GenerateAiTaskPacket() {
        super(45);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(language == null ? "python" : language, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.language = readString(buffer);
    }
}
