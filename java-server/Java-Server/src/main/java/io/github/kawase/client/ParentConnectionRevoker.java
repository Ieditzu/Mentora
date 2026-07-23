package io.github.kawase.client;

import java.util.Map;
import java.util.Objects;

final class ParentConnectionRevoker {
    private ParentConnectionRevoker() {
        /* w */
    }

    static void revokeOthers(
            final Map<Client, ClientHandler> activeConnections,
            final Long parentId,
            final Client currentClient) {
        for (final var entry : activeConnections.entrySet()) {
            if (entry.getKey().getRole() != ClientRole.PARENT
                    || !parentId.equals(entry.getKey().getParentId())
                    || entry.getKey() == currentClient)
                continue;

            entry.getKey().clearAuthentication();
            if (entry.getValue().getConnection().isOpen())
                entry.getValue().getConnection().close(1008, "Parent security settings changed");
        }
    }

    static void revokeSessions(
            final Map<Client, ClientHandler> activeConnections,
            final Long parentId,
            final String sessionToken,
            final boolean revokeAll,
            final Client currentClient) {
        for (final var entry : activeConnections.entrySet()) {
            final Client connectedClient = entry.getKey();
            if (connectedClient.getRole() != ClientRole.PARENT
                    || !parentId.equals(connectedClient.getParentId())
                    || !revokeAll && !Objects.equals(sessionToken, connectedClient.getParentSessionToken()))
                continue;

            connectedClient.clearAuthentication();
            if (connectedClient != currentClient && entry.getValue().getConnection().isOpen())
                entry.getValue().getConnection().close(1008, "Parent session revoked");
        }
    }
}
