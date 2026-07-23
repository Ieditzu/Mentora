package io.github.kawase.client;

import org.junit.jupiter.api.Test;

import java.util.Map;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class PacketAuthorizationPolicyTest {
    private static final Map<ClientRole, Set<Integer>> EXPECTED_PACKET_IDS = Map.of(
            ClientRole.UNAUTHENTICATED, Set.of(1, 2, 3, 19, 25, 76, 82, 91),
            ClientRole.PASSWORD_VERIFIED_PENDING_TOTP, Set.of(1, 2, 76, 82, 91),
            ClientRole.PARENT, Set.of(1, 4, 5, 11, 13, 15, 17, 19, 21, 26, 27, 32, 64, 66, 69, 76, 83, 85, 87, 88, 92),
            ClientRole.CHILD, Set.of(1, 8, 11, 13, 19, 23, 28, 30, 33, 34, 36, 38, 40, 45, 47, 58, 59, 65, 71, 74, 76, 77, 79)
    );

    @Test
    void packetRoleMatrixIsExhaustiveThroughCurrentProtocol() {
        final PacketAuthorizationPolicy policy = new PacketAuthorizationPolicy(false);

        for (final ClientRole role : ClientRole.values()) {
            for (int packetId = 1; packetId <= 92; packetId++)
                assertEquals(
                        EXPECTED_PACKET_IDS.get(role).contains(packetId),
                        policy.isAllowed(packetId, role),
                        "Unexpected authorization for packet " + packetId + " and role " + role
                );
        }
    }

    @Test
    void developmentPacketsRequireExplicitFlagAndParentRole() {
        final PacketAuthorizationPolicy disabledPolicy = new PacketAuthorizationPolicy(false);
        final PacketAuthorizationPolicy enabledPolicy = new PacketAuthorizationPolicy(true);

        for (final int packetId : Set.of(41, 43, 44)) {
            assertFalse(disabledPolicy.isAllowed(packetId, ClientRole.UNAUTHENTICATED));
            assertFalse(enabledPolicy.isAllowed(packetId, ClientRole.UNAUTHENTICATED));
            assertTrue(enabledPolicy.isAllowed(packetId, ClientRole.PARENT));
            assertFalse(enabledPolicy.isAllowed(packetId, ClientRole.CHILD));
        }
    }

    @Test
    void everyRequestPacketIsExplicitlyClassifiedAsInbound() {
        final PacketAuthorizationPolicy policy = new PacketAuthorizationPolicy(false);
        final Set<Integer> expectedInboundPacketIds = Set.of(
                1, 2, 3, 4, 5, 8, 11, 13, 15, 17, 19, 21, 23, 25, 26, 27, 28, 30, 32, 33,
                34, 36, 38, 40, 41, 43, 44, 45, 47, 58, 59, 64, 65, 66, 69, 71, 74, 76, 77,
                79, 82, 83, 85, 87, 88, 91, 92
        );

        for (int packetId = 1; packetId <= 92; packetId++)
            assertEquals(expectedInboundPacketIds.contains(packetId), policy.isInboundPacket(packetId));
    }
}
