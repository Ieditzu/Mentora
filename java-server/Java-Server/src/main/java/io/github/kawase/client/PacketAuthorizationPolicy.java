package io.github.kawase.client;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import java.util.EnumSet;
import java.util.Map;
import java.util.Set;

@Component
public class PacketAuthorizationPolicy {
    private static final Set<Integer> INBOUND_PACKET_IDS = Set.of(
            1, 2, 3, 4, 5, 8, 11, 13, 15, 17, 19, 21, 23, 25, 26, 27, 28, 30, 32, 33,
            34, 36, 38, 40, 41, 43, 44, 45, 47, 58, 59, 64, 65, 66, 69, 71, 74, 76, 77,
            79, 82, 83, 85, 87, 88, 91, 92
    );
    private static final Set<Integer> DEV_PACKET_IDS = Set.of(41, 43, 44);
    private static final Map<Integer, EnumSet<ClientRole>> ALLOWED_ROLES = Map.ofEntries(
            Map.entry(1, EnumSet.allOf(ClientRole.class)),
            Map.entry(2, EnumSet.of(ClientRole.UNAUTHENTICATED, ClientRole.PASSWORD_VERIFIED_PENDING_TOTP)),
            Map.entry(3, EnumSet.of(ClientRole.UNAUTHENTICATED)),
            Map.entry(4, EnumSet.of(ClientRole.PARENT)),
            Map.entry(5, EnumSet.of(ClientRole.PARENT)),
            Map.entry(8, EnumSet.of(ClientRole.CHILD)),
            Map.entry(11, EnumSet.of(ClientRole.PARENT, ClientRole.CHILD)),
            Map.entry(13, EnumSet.of(ClientRole.PARENT, ClientRole.CHILD)),
            Map.entry(15, EnumSet.of(ClientRole.PARENT)),
            Map.entry(17, EnumSet.of(ClientRole.PARENT)),
            Map.entry(19, EnumSet.of(ClientRole.UNAUTHENTICATED, ClientRole.PARENT, ClientRole.CHILD)),
            Map.entry(21, EnumSet.of(ClientRole.PARENT)),
            Map.entry(23, EnumSet.of(ClientRole.CHILD)),
            Map.entry(25, EnumSet.of(ClientRole.UNAUTHENTICATED)),
            Map.entry(26, EnumSet.of(ClientRole.PARENT)),
            Map.entry(27, EnumSet.of(ClientRole.PARENT)),
            Map.entry(28, EnumSet.of(ClientRole.CHILD)),
            Map.entry(30, EnumSet.of(ClientRole.CHILD)),
            Map.entry(32, EnumSet.of(ClientRole.PARENT)),
            Map.entry(33, EnumSet.of(ClientRole.CHILD)),
            Map.entry(34, EnumSet.of(ClientRole.CHILD)),
            Map.entry(36, EnumSet.of(ClientRole.CHILD)),
            Map.entry(38, EnumSet.of(ClientRole.CHILD)),
            Map.entry(40, EnumSet.of(ClientRole.CHILD)),
            Map.entry(45, EnumSet.of(ClientRole.CHILD)),
            Map.entry(47, EnumSet.of(ClientRole.CHILD)),
            Map.entry(58, EnumSet.of(ClientRole.CHILD)),
            Map.entry(59, EnumSet.of(ClientRole.CHILD)),
            Map.entry(64, EnumSet.of(ClientRole.PARENT)),
            Map.entry(65, EnumSet.of(ClientRole.CHILD)),
            Map.entry(66, EnumSet.of(ClientRole.PARENT)),
            Map.entry(69, EnumSet.of(ClientRole.PARENT)),
            Map.entry(71, EnumSet.of(ClientRole.CHILD)),
            Map.entry(74, EnumSet.of(ClientRole.CHILD)),
            Map.entry(76, EnumSet.allOf(ClientRole.class)),
            Map.entry(77, EnumSet.of(ClientRole.CHILD)),
            Map.entry(79, EnumSet.of(ClientRole.CHILD)),
            Map.entry(82, EnumSet.of(ClientRole.UNAUTHENTICATED, ClientRole.PASSWORD_VERIFIED_PENDING_TOTP)),
            Map.entry(83, EnumSet.of(ClientRole.PARENT)),
            Map.entry(85, EnumSet.of(ClientRole.PARENT)),
            Map.entry(87, EnumSet.of(ClientRole.PARENT)),
            Map.entry(88, EnumSet.of(ClientRole.PARENT)),
            Map.entry(91, EnumSet.of(ClientRole.UNAUTHENTICATED, ClientRole.PASSWORD_VERIFIED_PENDING_TOTP)),
            Map.entry(92, EnumSet.of(ClientRole.PARENT))
    );

    private final boolean devPacketsEnabled;

    public PacketAuthorizationPolicy(@Value("${mentora.dev-packets-enabled:false}") final boolean devPacketsEnabled) {
        this.devPacketsEnabled = devPacketsEnabled;
    }

    public boolean isAllowed(final int packetId, final ClientRole role) {
        if (DEV_PACKET_IDS.contains(packetId))
            return devPacketsEnabled && role == ClientRole.PARENT;

        final EnumSet<ClientRole> roles = ALLOWED_ROLES.get(packetId);
        return roles != null && roles.contains(role);
    }

    public boolean isDevPacket(final int packetId) {
        return DEV_PACKET_IDS.contains(packetId);
    }

    public boolean isInboundPacket(final int packetId) {
        return INBOUND_PACKET_IDS.contains(packetId);
    }
}
