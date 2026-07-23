package io.github.kawase.client;

import io.github.kawase.packet.PacketManager;
import io.github.kawase.security.ParentSessionService;
import org.java_websocket.WebSocket;
import org.junit.jupiter.api.Test;

import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class ParentSecuritySessionCoordinatorTest {
    @Test
    void currentConnectionReceivesFreshSessionWhileOtherSessionsClose() {
        final ParentSessionService parentSessionService = mock(ParentSessionService.class);
        final Client currentClient = new Client("current", new PacketManager());
        final Client otherClient = new Client("other", new PacketManager());
        currentClient.setHandshake(2, "phone-1");
        currentClient.authenticateParent(3L, "old-current-token");
        otherClient.authenticateParent(3L, "old-other-token");
        final WebSocket otherConnection = mock(WebSocket.class);
        when(otherConnection.isOpen()).thenReturn(true);
        final ClientHandler currentHandler = mock(ClientHandler.class);
        final ClientHandler otherHandler = mock(ClientHandler.class);
        when(otherHandler.getConnection()).thenReturn(otherConnection);
        final ParentSessionService.SessionToken freshSession = new ParentSessionService.SessionToken(
                3L,
                "",
                "fresh-token",
                99_999L
        );
        when(parentSessionService.issue(3L, "phone-1")).thenReturn(freshSession);

        final var result = new ParentSecuritySessionCoordinator(parentSessionService)
                .rotateAfterSecurityChange(currentClient, Map.of(
                        currentClient, currentHandler,
                        otherClient, otherHandler
                ));

        assertEquals(freshSession, result);
        assertEquals(ClientRole.PARENT, currentClient.getRole());
        assertEquals("fresh-token", currentClient.getParentSessionToken());
        assertNotEquals("old-current-token", currentClient.getParentSessionToken());
        assertEquals(ClientRole.UNAUTHENTICATED, otherClient.getRole());
        verify(otherConnection).close(1008, "Parent security settings changed");
        verify(parentSessionService).issue(3L, "phone-1");
    }
}
