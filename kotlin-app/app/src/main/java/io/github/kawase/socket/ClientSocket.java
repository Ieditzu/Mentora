package io.github.kawase.socket;

import io.github.kawase.socket.packet.Packet;
import io.github.kawase.socket.packet.PacketManager;
import io.github.kawase.socket.packet.impl.*;
import io.github.kawase.socket.utility.HashUtility;
import org.java_websocket.client.WebSocketClient;
import org.java_websocket.handshake.ServerHandshake;

import java.net.URI;
import java.nio.ByteBuffer;
import java.util.concurrent.CountDownLatch;

public class ClientSocket extends WebSocketClient {
    private final CountDownLatch connectLatch = new CountDownLatch(1);
    private final PacketManager packetManager = new PacketManager();
    
    // Test states
    private long loggedInParentId = -1;
    private String testEmail;

    public ClientSocket(final URI serverUri, String testEmail) {
        super(serverUri);
        this.testEmail = testEmail;
    }

    @Override
    public void onOpen(final ServerHandshake handshake) {
        System.out.println("Connected to server.");
        connectLatch.countDown();
    }

    @Override
    public void onMessage(String message) {
        /* w */
    }

    @Override
    public void onMessage(final ByteBuffer bytes) {
        try {
            final Packet packet = Packet.construct(bytes, packetManager);

            if (packet instanceof AuthResponsePacket) {
                AuthResponsePacket authResponsePacket = (AuthResponsePacket) packet;
                System.out.println("Auth response: Success=" + authResponsePacket.isSuccess() + ", Message='" + authResponsePacket.getMessage() + "', ParentID=" + authResponsePacket.getParentId());
                if (authResponsePacket.isSuccess()) {
                    loggedInParentId = authResponsePacket.getParentId();
                    System.out.println("Sending AddChild packet...");
                    send(new AddChildPacket("Timmy").encode());
                }
            } else if (packet instanceof ActionResponsePacket) {
                ActionResponsePacket actionResponsePacket = (ActionResponsePacket) packet;
                System.out.println("Action response: PacketID=" + actionResponsePacket.getRequestPacketId() + 
                        ", Success=" + actionResponsePacket.isSuccess() + 
                        ", Message='" + actionResponsePacket.getMessage() + "'" + 
                        ", ResultID=" + actionResponsePacket.getResultId());
                        
                // If it was a successful registration
                if (actionResponsePacket.getRequestPacketId() == 3 && actionResponsePacket.isSuccess()) {
                    System.out.println("Sending Auth packet to log in...");
                    send(new AuthPacket(testEmail, HashUtility.hash("mySecurePassword123")).encode());
                }
                
                // If it was a successful add child
                if (actionResponsePacket.getRequestPacketId() == 4 && actionResponsePacket.isSuccess()) {
                    long childId = actionResponsePacket.getResultId();
                    System.out.println("Sending AddGoal packet for Child ID: " + childId);
                    // Require 50 points to get a toy (-1 means points based)
                    send(new AddGoalPacket(childId, "Be a good boy", "New Lego Set", 50, -1).encode());
                }
            } else {
                System.out.println("Unexpected packet received: " + packet.getClass().getSimpleName());
            }
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    @Override
    public void onClose(int code, String reason, boolean remote) {
        System.out.println("Connection closed: " + reason);
    }

    @Override
    public void onError(Exception ex) {
        ex.printStackTrace();
    }

    public void awaitConnection() throws InterruptedException {
        connectLatch.await();
    }

    public static void main(String[] args) {
        try {
            String dynamicEmail = "parent" + System.currentTimeMillis() + "@email.com";
            final ClientSocket client = new ClientSocket(new URI("wss://neuro.serenityutils.club"), dynamicEmail);
            client.connect();

            client.awaitConnection();

            System.out.println("Sending Handshake...");
            client.send(new HandShakePacket("test_client").encode());

            System.out.println("Sending Registration packet...");
            client.send(new RegisterParentPacket(dynamicEmail, HashUtility.hash("mySecurePassword123")).encode());

            // Let the client thread run for a bit to receive the chained async responses
            Thread.sleep(5000);
            
            System.out.println("Test sequence complete. Closing connection.");
            client.close();

        } catch (Exception e) {
            System.out.println("Failed to connected to IRC server.");
            e.printStackTrace();
        }
    }
}