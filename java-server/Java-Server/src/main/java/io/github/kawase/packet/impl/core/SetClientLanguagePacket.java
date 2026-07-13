package io.github.kawase.packet.impl.core;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class SetClientLanguagePacket extends Packet {
    private String languageTag;

    public SetClientLanguagePacket(final String languageTag) {
        super(76);
        this.languageTag = languageTag;
    }

    public SetClientLanguagePacket() {
        super(76);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(languageTag == null ? "" : languageTag, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        languageTag = readString(buffer);
    }
}
