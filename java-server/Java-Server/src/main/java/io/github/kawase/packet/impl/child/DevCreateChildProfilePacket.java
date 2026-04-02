package io.github.kawase.packet.impl.child;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class DevCreateChildProfilePacket extends Packet {
    private String childName;

    public DevCreateChildProfilePacket(final String childName) {
        super(44);
        this.childName = childName;
    }

    public DevCreateChildProfilePacket() {
        super(44);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(childName == null ? "" : childName, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childName = readString(buffer);
    }
}
