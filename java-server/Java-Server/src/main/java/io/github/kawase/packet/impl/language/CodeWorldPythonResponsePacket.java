package io.github.kawase.packet.impl.language;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class CodeWorldPythonResponsePacket extends Packet {
    private String requestId;
    private String commandsText;
    private String output;
    private String error;

    public CodeWorldPythonResponsePacket(final String requestId, final String commandsText, final String output, final String error) {
        super(75);
        this.requestId = requestId;
        this.commandsText = commandsText;
        this.output = output;
        this.error = error;
    }

    public CodeWorldPythonResponsePacket() {
        super(75);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(requestId == null ? "" : requestId, buffer);
        putString(commandsText == null ? "" : commandsText, buffer);
        putString(output == null ? "" : output, buffer);
        putString(error == null ? "" : error, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.requestId = readString(buffer);
        this.commandsText = readString(buffer);
        this.output = readString(buffer);
        this.error = readString(buffer);
    }
}
