package io.github.kawase.packet.impl.language;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class ExecutePythonCodeResponsePacket extends Packet {
    private String output;
    private String error;

    public ExecutePythonCodeResponsePacket(final String output, final String error) {
        super(35);
        this.output = output;
        this.error = error;
    }

    public ExecutePythonCodeResponsePacket() {
        super(35);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(output == null ? "" : output, buffer);
        putString(error == null ? "" : error, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.output = readString(buffer);
        this.error = readString(buffer);
    }
}
