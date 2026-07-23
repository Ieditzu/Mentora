package io.github.kawase.client;

import io.github.kawase.packet.Packet;
import io.github.kawase.packet.PacketManager;
import io.github.kawase.packet.impl.child.AddChildPacket;
import io.github.kawase.packet.impl.child.DevCreateChildProfilePacket;
import io.github.kawase.packet.impl.child.DevLoginAsChildPacket;
import io.github.kawase.packet.impl.child.FetchAllChildrenPacket;
import io.github.kawase.packet.impl.companion.CompanionSpeakPacket;
import io.github.kawase.packet.impl.companion.CompanionVoiceAudioPacket;
import io.github.kawase.packet.impl.companion.CompanionVoiceTextPacket;
import io.github.kawase.packet.impl.core.ActionResponsePacket;
import io.github.kawase.packet.impl.game.AddGoalPacket;
import io.github.kawase.packet.impl.game.FetchWeeklyReportPacket;
import io.github.kawase.packet.impl.game.SendParentChallengePacket;
import io.github.kawase.packet.impl.game.SubscribeLiveSessionPacket;
import io.github.kawase.socket.ServerSocket;
import org.java_websocket.WebSocket;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.MethodSource;
import org.mockito.ArgumentCaptor;

import java.nio.ByteBuffer;
import java.util.stream.Stream;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;

class ClientHandlerAuthorizationTest {
    @ParameterizedTest
    @MethodSource("sensitivePreAuthPackets")
    void sensitivePacketsAreRejectedBeforeDispatch(final Packet request) throws Exception {
        final WebSocket connection = mock(WebSocket.class);
        final Client client = new Client("test-client", new PacketManager());
        new ClientHandler(
                connection,
                client,
                mock(ServerSocket.class),
                new PacketAuthorizationPolicy(false)
        ).onMessage(request.encode());

        final ArgumentCaptor<ByteBuffer> captor = ArgumentCaptor.forClass(ByteBuffer.class);
        verify(connection).send(captor.capture());
        final ActionResponsePacket response = assertInstanceOf(
                ActionResponsePacket.class,
                Packet.construct(captor.getValue(), new PacketManager())
        );

        assertEquals(request.getId(), response.getRequestPacketId());
        assertFalse(response.isSuccess());
        assertEquals(
                new PacketAuthorizationPolicy(false).isDevPacket(request.getId())
                        ? "Development packets are disabled."
                        : "Unauthorized for the current client role.",
                response.getMessage()
        );
        assertEquals(-1, response.getResultId());
    }

    @ParameterizedTest
    @MethodSource("parentOnlyPackets")
    void childRoleCannotDispatchParentPackets(final Packet request) throws Exception {
        final WebSocket connection = mock(WebSocket.class);
        final Client client = new Client("test-child", new PacketManager());
        client.authenticateChild(7L, 3L);
        new ClientHandler(
                connection,
                client,
                mock(ServerSocket.class),
                new PacketAuthorizationPolicy(false)
        ).onMessage(request.encode());

        final ArgumentCaptor<ByteBuffer> captor = ArgumentCaptor.forClass(ByteBuffer.class);
        verify(connection).send(captor.capture());
        final ActionResponsePacket response = assertInstanceOf(
                ActionResponsePacket.class,
                Packet.construct(captor.getValue(), new PacketManager())
        );

        assertEquals(request.getId(), response.getRequestPacketId());
        assertFalse(response.isSuccess());
        assertEquals("Unauthorized for the current client role.", response.getMessage());
        assertEquals(-1, response.getResultId());
    }

    private static Stream<Packet> sensitivePreAuthPackets() {
        return Stream.of(
                new FetchAllChildrenPacket(),
                new DevLoginAsChildPacket(7L),
                new DevCreateChildProfilePacket("Injected child"),
                new CompanionSpeakPacket("greet"),
                new CompanionVoiceTextPacket("Run this", "```python\nprint('unsafe')\n```"),
                new CompanionVoiceAudioPacket(16_000, new byte[] { 0, 0 }, "voice")
        );
    }

    private static Stream<Packet> parentOnlyPackets() {
        return Stream.of(
                new AddChildPacket("Sibling"),
                new AddGoalPacket(7L, "Goal", "Reward", 10, -1),
                new SubscribeLiveSessionPacket(7L, true),
                new SendParentChallengePacket(7L, "Do the task"),
                new FetchWeeklyReportPacket(7L)
        );
    }
}
