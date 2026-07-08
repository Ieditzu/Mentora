package io.github.kawase.packet.impl.language;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class CodeWorldPythonRunPacket extends Packet {
    private String requestId;
    private String code;

    public CodeWorldPythonRunPacket(final String requestId, final String code) {
        super(74);
        this.requestId = requestId;
        this.code = code;
    }

    public CodeWorldPythonRunPacket() {
        super(74);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(requestId == null ? "" : requestId, buffer);
        putString(code == null ? "" : code, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.requestId = readString(buffer);
        this.code = readString(buffer);
    }
}
