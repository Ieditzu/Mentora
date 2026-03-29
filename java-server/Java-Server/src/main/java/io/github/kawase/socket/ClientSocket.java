package io.github.kawase.socket;

import io.github.kawase.packet.Packet;
import io.github.kawase.packet.PacketManager;
import io.github.kawase.packet.impl.ai.AiResponsePacket;
import io.github.kawase.packet.impl.ai.AskAiPacket;
import io.github.kawase.packet.impl.auth.*;
import io.github.kawase.packet.impl.child.*;
import io.github.kawase.packet.impl.core.*;
import io.github.kawase.packet.impl.game.*;
import io.github.kawase.packet.impl.language.ExecuteCPPCodePacket;
import io.github.kawase.packet.impl.language.ExecuteCPPCodeResponsePacket;
import io.github.kawase.packet.impl.qr.*;
import io.github.kawase.utility.HashUtility;
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

            switch (packet) {
                case ExecuteCPPCodeResponsePacket executeCPPCodeResponsePacket -> {
                    System.out.println("Received response for cpp code execution: " + executeCPPCodeResponsePacket.getOutput() + ", error: " + executeCPPCodeResponsePacket.getError());
                }

                case AiResponsePacket aiResponsePacket -> {
                    System.out.println("Ai response: " + aiResponsePacket.getResponse());
                }

                case AuthResponsePacket authResponsePacket -> {
                    System.out.println("Auth response: Success=" + authResponsePacket.isSuccess() + ", Message='" + authResponsePacket.getMessage() + "', ParentID=" + authResponsePacket.getParentId());
                    if (authResponsePacket.isSuccess()) {
                        loggedInParentId = authResponsePacket.getParentId();
                        System.out.println("Sending AddChild packet...");
                        send(new AddChildPacket("Timmy").encode());
                    }
                }

                case ActionResponsePacket actionResponsePacket -> {
                    System.out.println("Action response: PacketID=" + actionResponsePacket.getRequestPacketId() +
                            ", Success=" + actionResponsePacket.isSuccess() +
                            ", Message='" + actionResponsePacket.getMessage() + "'" +
                            ", ResultID=" + actionResponsePacket.getResultId());

                    if (actionResponsePacket.getRequestPacketId() == 3 && actionResponsePacket.isSuccess()) {
                        System.out.println("Sending Auth packet to log in...");
                        send(new AuthPacket(testEmail, HashUtility.hash("mySecurePassword123")).encode());
                    }

                    if (actionResponsePacket.getRequestPacketId() == 4 && actionResponsePacket.isSuccess()) {
                        long childId = actionResponsePacket.getResultId();
                        System.out.println("Sending AddGoal packet for Child ID: " + childId);
                        send(new AddGoalPacket(childId, "Be a good boy", "New Lego Set", 50, -1).encode());

                        System.out.println("Fetching Tasks...");
                        send(new FetchTasksPacket().encode());

                        System.out.println("Fetching Children...");
                        send(new FetchChildrenPacket().encode());
                    }
                }

                case FetchChildrenResponsePacket fetchChildrenResponsePacket -> {
                    System.out.println("Received Children: " + fetchChildrenResponsePacket.getChildren().size());
                    for (var c : fetchChildrenResponsePacket.getChildren()) {
                        System.out.println(" - [" + c.id + "] " + c.name + " (" + c.totalPoints + " pts)");
                        System.out.println("Fetching History for " + c.name + "...");
                        send(new FetchCompletedTasksPacket(c.id).encode());
                    }
                }

                case FetchCompletedTasksResponsePacket fetchCompletedTasksResponsePacket -> {
                    System.out.println("Received Task History: " + fetchCompletedTasksResponsePacket.getCompletedTasks().size());
                    for (var ct : fetchCompletedTasksResponsePacket.getCompletedTasks()) {
                        System.out.println(" - [" + ct.id + "] " + ct.taskTitle + " at " + ct.completedAt);
                    }
                }

                case FetchTasksResponsePacket fetchTasksResponsePacket -> {
                    System.out.println("Received Tasks: " + fetchTasksResponsePacket.getTasks().size());
                    for (var t : fetchTasksResponsePacket.getTasks()) {
                        System.out.println(" - [" + t.id + "] " + t.title + " (" + t.pointValue + " pts)");
                    }
                }

                case FetchGoalsResponsePacket fetchGoalsResponsePacket -> {
                    System.out.println("Received Goals: " + fetchGoalsResponsePacket.getGoals().size());
                    for (var g : fetchGoalsResponsePacket.getGoals()) {
                        System.out.println(" - [" + g.id + "] " + g.title + " -> " + g.reward + " (Completed: " + g.isCompleted + ")");
                    }
                }

                default -> System.out.println("Unexpected packet received: " + packet.getClass().getSimpleName());
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
            final ClientSocket client = new ClientSocket(new URI("ws://localhost:49154"), dynamicEmail);
            client.connect();

            client.awaitConnection();

            client.send(new RegisterParentPacket(dynamicEmail, HashUtility.hash("mySecurePassword123")).encode());
            client.send(new AuthPacket(dynamicEmail, HashUtility.hash("mySecurePassword123")).encode());

            String cppCode = """
                    #include <iostream>
                    
                    int main() {
                        std::cout << "Hello, World!" << std::endl;
                        return 0;
                    }
                    """;

            client.send(new HandShakePacket("nigger").encode());
            client.send(new AskAiPacket("Help fr", "idk").encode());
            client.send(new ExecuteCPPCodePacket(cppCode).encode());

        } catch (Exception e) {
            System.out.println("Failed to connected to IRC server.");
            e.printStackTrace();
        }
    }
}