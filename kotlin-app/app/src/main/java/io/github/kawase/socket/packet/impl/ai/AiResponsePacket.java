package io.github.kawase.socket.packet.impl.ai;

import io.github.kawase.socket.packet.Packet;
import java.nio.ByteBuffer;

public class AiResponsePacket extends Packet {
    private String response;

    public AiResponsePacket(final String response) {
        super(31);
        this.response = response;
    }

    public AiResponsePacket() {
        super(31);
    }

    public String getResponse() {
        return response;
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
