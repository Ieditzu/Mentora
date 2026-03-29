package io.github.kawase.packet.impl.ai;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class AskAiPacket extends Packet {
    private String question;
    private String context; // e.g., "cpp_help", "general", "hint"

    public AskAiPacket(final String question, final String context) {
        super(30);
        this.question = question;
        this.context = context;
    }

    public AskAiPacket() {
        super(30);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(question == null ? "" : question, buffer);
        putString(context == null ? "" : context, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.question = readString(buffer);
        this.context = readString(buffer);
    }
}
