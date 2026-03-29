package io.github.kawase.packet.impl.ai;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class AiResponsePacket extends Packet {
    private String response;

    public AiResponsePacket(final String response) {
        super(31);
        this.response = response;
    }

    public AiResponsePacket() {
        super(31);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(response == null ? "" : response, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.response = readString(buffer);
    }
}
