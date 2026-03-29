package io.github.kawase.packet.impl.language;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class ExecuteCPPCodeResponsePacket extends Packet {
    private String output, error;

    public ExecuteCPPCodeResponsePacket(final String output, final String error) {
        super(29);
        this.output = output;
        this.error = error;
    }

    public ExecuteCPPCodeResponsePacket() {
        super(29);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(output, buffer);
        putString(error, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.output = readString(buffer);
        this.error = readString(buffer);
    }
}
