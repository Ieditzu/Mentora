package io.github.kawase.socket;

import io.github.kawase.Server;
import io.github.kawase.client.Client;
import io.github.kawase.client.ClientHandler;
import org.java_websocket.server.WebSocketServer;
import org.java_websocket.handshake.ClientHandshake;
import org.java_websocket.WebSocket;

import java.net.InetSocketAddress;
import java.nio.ByteBuffer;

public class ServerSocket extends WebSocketServer {

    public ServerSocket(int port) {
        super(new InetSocketAddress("0.0.0.0", port));
    }

    @Override
    public void onOpen(WebSocket conn, ClientHandshake handshake) {
        final String remoteID = conn.getRemoteSocketAddress().getHostString() + ":" +
                conn.getRemoteSocketAddress().getPort();

        final Client client = new Client(remoteID, Server.getInstance().getPacketManager());
        final ClientHandler handler = new ClientHandler(conn, client, this);

        conn.setAttachment(handler);

        Server.getInstance().getActiveConnections().put(client, handler);

        System.out.println("Client " + client.getHostID() + " connected.");

        handler.onOpen();
    }

    @Override
    public void onMessage(WebSocket conn, String message) {
        final ClientHandler handler = conn.getAttachment();

        System.out.println("Invalid message detected for " + handler.getClient().getHostID());
        conn.close();
    }

    @Override
    public void onMessage(WebSocket conn, ByteBuffer blob) {
        final ClientHandler handler = conn.getAttachment();

        handler.onMessage(blob);
    }

    @Override
    public void onClose(WebSocket conn, int code, String reason, boolean remote) {
        final ClientHandler handler = conn.getAttachment();

        Server.getInstance().getActiveConnections().remove(handler.getClient());
        handler.onClose();

        System.out.println("WebSocket closed " + handler.getClient().getHostID() + (reason != null ? " " + reason : ""));
    }

    @Override
    public void onError(WebSocket conn, Exception ex) {
        ex.printStackTrace();
    }

    @Override
    public void onStart() {
        System.out.println("Server started on port: " + getPort());
    }
}