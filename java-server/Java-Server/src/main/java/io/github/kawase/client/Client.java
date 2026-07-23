package io.github.kawase.client;

import io.github.kawase.packet.PacketManager;
import lombok.Getter;
import lombok.RequiredArgsConstructor;
import lombok.Setter;

@RequiredArgsConstructor
@Getter
public class Client {
    private final String hostID;
    private final PacketManager packetManager;

    private ClientRole role = ClientRole.UNAUTHENTICATED;
    private Long parentId, childId;
    @Setter
    private String language = "en";
    private String deviceId = "";
    private String parentSessionToken = "";
    private int protocolVersion = 1;

    public void setHandshake(final int protocolVersion, final String deviceId) {
        this.protocolVersion = Math.max(1, protocolVersion);
        this.deviceId = deviceId == null ? "" : deviceId.trim();
    }

    public void requireSecondFactor() {
        role = ClientRole.PASSWORD_VERIFIED_PENDING_TOTP;
        parentId = null;
        childId = null;
        parentSessionToken = "";
    }

    public void authenticateParent(final Long parentId, final String parentSessionToken) {
        role = ClientRole.PARENT;
        this.parentId = parentId;
        childId = null;
        this.parentSessionToken = parentSessionToken == null ? "" : parentSessionToken;
    }

    public void authenticateChild(final Long childId, final Long parentId) {
        role = ClientRole.CHILD;
        this.childId = childId;
        this.parentId = parentId;
        parentSessionToken = "";
    }

    public void clearAuthentication() {
        role = ClientRole.UNAUTHENTICATED;
        parentId = null;
        childId = null;
        parentSessionToken = "";
    }
}
