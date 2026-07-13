package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

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

    public String getLanguageTag() {
        return languageTag;
    }
}
