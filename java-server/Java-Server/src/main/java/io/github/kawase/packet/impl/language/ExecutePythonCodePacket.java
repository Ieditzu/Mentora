package io.github.kawase.packet.impl.language;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class ExecutePythonCodePacket extends Packet {
    private String code;

    public ExecutePythonCodePacket(final String code) {
        super(34);
        this.code = code;
    }

    public ExecutePythonCodePacket() {
        super(34);
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
