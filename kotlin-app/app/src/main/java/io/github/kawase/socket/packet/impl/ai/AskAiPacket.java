package io.github.kawase.socket.packet.impl.ai;

import io.github.kawase.socket.packet.Packet;
import java.nio.ByteBuffer;

public class AskAiPacket extends Packet {
    private String question;
    private String context;

    public AskAiPacket(final String question, final String context) {
        super(30);
        this.question = question;
        this.context = context;
    }

    public AskAiPacket() {
        super(30);
    }

    public String getQuestion() {
        return question;
    }

    public String getContext() {
        return context;
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
