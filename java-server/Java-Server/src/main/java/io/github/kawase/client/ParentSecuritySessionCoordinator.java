package io.github.kawase.client;

import io.github.kawase.security.ParentSessionService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

import java.util.Map;

@Service
@RequiredArgsConstructor
public class ParentSecuritySessionCoordinator {
    private final ParentSessionService parentSessionService;

    public ParentSessionService.SessionToken rotateAfterSecurityChange(
            final Client currentClient,
            final Map<Client, ClientHandler> activeConnections) {
        final Long parentId = currentClient.getParentId();
        ParentConnectionRevoker.revokeOthers(activeConnections, parentId, currentClient);
        final ParentSessionService.SessionToken session = parentSessionService.issue(
                parentId,
                currentClient.getDeviceId()
        );
        currentClient.authenticateParent(parentId, session.rawToken());
        return session;
    }
}
