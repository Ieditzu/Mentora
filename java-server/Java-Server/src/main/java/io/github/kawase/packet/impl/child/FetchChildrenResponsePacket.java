package io.github.kawase.packet.impl.child;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.List;

@Getter
public class FetchChildrenResponsePacket extends Packet {
    public static class ChildDto {
        public long id;
        public String name;
        public int totalPoints;
        public boolean isOnline;
        public String pfp;

        public ChildDto(long id, String name, int totalPoints, boolean isOnline, String pfp) {
            this.id = id;
            this.name = name;
            this.totalPoints = totalPoints;
            this.isOnline = isOnline;
            this.pfp = pfp;
        }
    }

    private List<ChildDto> children = new ArrayList<>();

    public FetchChildrenResponsePacket(final List<ChildDto> children) {
        super(16);
        this.children = children;
    }

    public FetchChildrenResponsePacket() {
        super(16);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putInt(children.size());
        for (ChildDto child : children) {
            buffer.putLong(child.id);
            putString(child.name, buffer);
            buffer.putInt(child.totalPoints);
            buffer.put((byte) (child.isOnline ? 1 : 0));
            putString(child.pfp == null ? "" : child.pfp, buffer);
        }
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        int size = buffer.getInt();
        for (int i = 0; i < size; i++) {
            long id = buffer.getLong();
            String name = readString(buffer);
            int points = buffer.getInt();
            boolean isOnline = buffer.get() == 1;
            String pfp = readString(buffer);
            children.add(new ChildDto(id, name, points, isOnline, pfp));
        }
    }
}
