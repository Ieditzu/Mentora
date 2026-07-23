package io.github.kawase.client;

import io.github.kawase.packet.PacketManager;
import org.java_websocket.WebSocket;
import org.junit.jupiter.api.Test;

import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class ParentConnectionRevokerTest {
    @Test
    void securityChangeClearsAndClosesOtherParentConnections() {
        final Client firstParentClient = new Client("first-parent", new PacketManager());
        final Client secondParentClient = new Client("second-parent", new PacketManager());
        final Client childClient = new Client("child", new PacketManager());
        firstParentClient.authenticateParent(3L, "first-token");
        secondParentClient.authenticateParent(3L, "second-token");
        childClient.authenticateChild(7L, 3L);
        final WebSocket firstConnection = mock(WebSocket.class);
        final WebSocket secondConnection = mock(WebSocket.class);
        final WebSocket childConnection = mock(WebSocket.class);
        when(firstConnection.isOpen()).thenReturn(true);
        when(secondConnection.isOpen()).thenReturn(true);
        when(childConnection.isOpen()).thenReturn(true);
        final ClientHandler firstHandler = mock(ClientHandler.class);
        final ClientHandler secondHandler = mock(ClientHandler.class);
        final ClientHandler childHandler = mock(ClientHandler.class);
        when(firstHandler.getConnection()).thenReturn(firstConnection);
        when(secondHandler.getConnection()).thenReturn(secondConnection);
        when(childHandler.getConnection()).thenReturn(childConnection);

        ParentConnectionRevoker.revokeOthers(Map.of(
                firstParentClient, firstHandler,
                secondParentClient, secondHandler,
                childClient, childHandler
        ), 3L, firstParentClient);

        assertEquals(ClientRole.PARENT, firstParentClient.getRole());
        assertEquals(ClientRole.UNAUTHENTICATED, secondParentClient.getRole());
        assertEquals(ClientRole.CHILD, childClient.getRole());
        verify(firstConnection, never()).close(1008, "Parent security settings changed");
        verify(secondConnection).close(1008, "Parent security settings changed");
        verify(childConnection, never()).close(1008, "Parent security settings changed");
    }

    @Test
    void revokeAllClearsEveryLiveParentConnectionButNotTheChild() {
        final Client currentParentClient = new Client("current-parent", new PacketManager());
        final Client otherParentClient = new Client("other-parent", new PacketManager());
        final Client childClient = new Client("child", new PacketManager());
        currentParentClient.authenticateParent(3L, "current-token");
        otherParentClient.authenticateParent(3L, "other-token");
        childClient.authenticateChild(7L, 3L);
        final WebSocket currentConnection = mock(WebSocket.class);
        final WebSocket otherConnection = mock(WebSocket.class);
        final WebSocket childConnection = mock(WebSocket.class);
        when(currentConnection.isOpen()).thenReturn(true);
        when(otherConnection.isOpen()).thenReturn(true);
        when(childConnection.isOpen()).thenReturn(true);
        final ClientHandler currentHandler = mock(ClientHandler.class);
        final ClientHandler otherHandler = mock(ClientHandler.class);
        final ClientHandler childHandler = mock(ClientHandler.class);
        when(currentHandler.getConnection()).thenReturn(currentConnection);
        when(otherHandler.getConnection()).thenReturn(otherConnection);
        when(childHandler.getConnection()).thenReturn(childConnection);

        ParentConnectionRevoker.revokeSessions(Map.of(
                currentParentClient, currentHandler,
                otherParentClient, otherHandler,
                childClient, childHandler
        ), 3L, "current-token", true, currentParentClient);

        assertEquals(ClientRole.UNAUTHENTICATED, currentParentClient.getRole());
        assertEquals(ClientRole.UNAUTHENTICATED, otherParentClient.getRole());
        assertEquals(ClientRole.CHILD, childClient.getRole());
        verify(currentConnection, never()).close(1008, "Parent session revoked");
        verify(otherConnection).close(1008, "Parent session revoked");
        verify(childConnection, never()).close(1008, "Parent session revoked");
    }

    @Test
    void revokeOneSessionOnlyClearsTheMatchingLiveConnection() {
        final Client currentParentClient = new Client("current-parent", new PacketManager());
        final Client otherParentClient = new Client("other-parent", new PacketManager());
        currentParentClient.authenticateParent(3L, "current-token");
        otherParentClient.authenticateParent(3L, "other-token");
        final WebSocket currentConnection = mock(WebSocket.class);
        final WebSocket otherConnection = mock(WebSocket.class);
        when(currentConnection.isOpen()).thenReturn(true);
        when(otherConnection.isOpen()).thenReturn(true);
        final ClientHandler currentHandler = mock(ClientHandler.class);
        final ClientHandler otherHandler = mock(ClientHandler.class);
        when(currentHandler.getConnection()).thenReturn(currentConnection);
        when(otherHandler.getConnection()).thenReturn(otherConnection);

        ParentConnectionRevoker.revokeSessions(Map.of(
                currentParentClient, currentHandler,
                otherParentClient, otherHandler
        ), 3L, "other-token", false, currentParentClient);

        assertEquals(ClientRole.PARENT, currentParentClient.getRole());
        assertEquals(ClientRole.UNAUTHENTICATED, otherParentClient.getRole());
        verify(currentConnection, never()).close(1008, "Parent session revoked");
        verify(otherConnection).close(1008, "Parent session revoked");
    }
}
