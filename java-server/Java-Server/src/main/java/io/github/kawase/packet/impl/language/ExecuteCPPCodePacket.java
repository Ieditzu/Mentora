package io.github.kawase.packet.impl.language;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class ExecuteCPPCodePacket extends Packet {
    private String code;

    public ExecuteCPPCodePacket(final String code) {
        super(28);
        this.code = code;
    }

    public ExecuteCPPCodePacket() {
        super(28);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(code, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.code = readString(buffer);
    }
}
