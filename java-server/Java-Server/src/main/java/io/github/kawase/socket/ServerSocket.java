package io.github.kawase.socket;

import io.github.kawase.Server;
import io.github.kawase.client.Client;
import io.github.kawase.client.ClientHandler;
import org.java_websocket.WebSocket;
import org.java_websocket.handshake.ClientHandshake;
import org.java_websocket.server.WebSocketServer;

import java.net.InetSocketAddress;
import java.nio.ByteBuffer;

public class ServerSocket extends WebSocketServer {

    public ServerSocket(final int port) {
        super(new InetSocketAddress("0.0.0.0", port));
    }

    @Override
    public void onOpen(final WebSocket conn, final ClientHandshake handshake) {
        final String remoteID = conn.getRemoteSocketAddress().getHostString() + ":" +
                conn.getRemoteSocketAddress().getPort();

        final Client client = new Client(remoteID, Server.getInstance().getPacketManager());
        final ClientHandler handler = new ClientHandler(
                conn,
                client,
                this,
                Server.getInstance().getPacketAuthorizationPolicy()
        );

        conn.setAttachment(handler);

        Server.getInstance().getActiveConnections().put(client, handler);

        System.out.println("Client " + client.getHostID() + " connected.");

        handler.onOpen();
    }

    @Override
    public void onMessage(final WebSocket conn, final String message) {
        final ClientHandler handler = conn.getAttachment();

        System.out.println("Invalid message detected for " + handler.getClient().getHostID());
        conn.close();
    }

    @Override
    public void onMessage(final WebSocket conn, final ByteBuffer blob) {
        final ClientHandler handler = conn.getAttachment();

        handler.onMessage(blob);
    }

    @Override
    public void onClose(
            final WebSocket conn,
            final int code,
            final String reason,
            final boolean remote) {
        final ClientHandler handler = conn.getAttachment();

        Server.getInstance().getActiveConnections().remove(handler.getClient());
        Server.getInstance().getPendingQRLogins().entrySet()
                .removeIf(entry -> entry.getValue() == handler);
        handler.onClose();

        System.out.println("WebSocket closed " + handler.getClient().getHostID() + (reason != null ? " " + reason : ""));
    }

    @Override
    public void onError(final WebSocket conn, final Exception ex) {
        ex.printStackTrace();
    }

    @Override
    public void onStart() {
        System.out.println("Server started on port: " + getPort());
    }
}
