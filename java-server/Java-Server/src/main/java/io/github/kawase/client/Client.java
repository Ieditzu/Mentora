package io.github.kawase.client;

import io.github.kawase.packet.PacketManager;
import lombok.Getter;
import lombok.RequiredArgsConstructor;
import lombok.Setter;

@RequiredArgsConstructor
@Getter
@Setter
public class Client {
    private final String hostID;
    private final PacketManager packetManager;

    private boolean auth;
    private boolean handshake;

    private Long parentId;
    private Long childId;
}

