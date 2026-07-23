package io.github.kawase;

import io.github.kawase.client.Client;
import io.github.kawase.client.ClientHandler;
import io.github.kawase.client.PacketAuthorizationPolicy;
import io.github.kawase.client.ParentSecuritySessionCoordinator;
import io.github.kawase.database.services.ChildService;
import io.github.kawase.database.services.CourseService;
import io.github.kawase.database.services.GameSessionService;
import io.github.kawase.database.services.GoalService;
import io.github.kawase.database.services.LearningProfileService;
import io.github.kawase.database.services.ParentService;
import io.github.kawase.database.services.TaskService;
import io.github.kawase.packet.impl.game.LiveSessionUpdatePacket;
import io.github.kawase.packet.impl.game.ParentChallengePacket;
import io.github.kawase.packet.PacketManager;
import io.github.kawase.machinelearning.MachineLearningService;
import io.github.kawase.utility.ContainerExecution;
import io.github.kawase.socket.ServerSocket;
import io.github.kawase.security.ParentAuthenticationService;
import io.github.kawase.security.ParentSessionService;
import lombok.Getter;
import org.springframework.context.ApplicationContext;

import java.util.concurrent.ConcurrentHashMap;
import java.util.Set;

@Getter
public class Server {
    @Getter
    private static final Server instance = new Server();

    private ConcurrentHashMap<Client, ClientHandler> activeConnections;

    private final ConcurrentHashMap<String, ClientHandler> pendingQRLogins = new ConcurrentHashMap<>();
    private ConcurrentHashMap<Long, LiveSessionUpdatePacket> latestLiveSessionStates;
    private ConcurrentHashMap<Long, Set<ClientHandler>> liveSessionSpectators;
    private ConcurrentHashMap<Long, ParentChallengePacket> activeParentChallenges;

    private ServerSocket socket;

    // we will init every manager here :pray:.
    private PacketManager packetManager = new PacketManager();

    // sprint boot stuff.
    private ApplicationContext context;

    // services.
    private ParentService parentService;
    private TaskService taskService;
    private ChildService childService;
    private GoalService goalService;
    private GameSessionService gameSessionService;
    private LearningProfileService learningProfileService;
    private CourseService courseService;
    private MachineLearningService machineLearningService;
    private ParentAuthenticationService parentAuthenticationService;
    private ParentSessionService parentSessionService;
    private PacketAuthorizationPolicy packetAuthorizationPolicy;
    private ParentSecuritySessionCoordinator parentSecuritySessionCoordinator;

    public void init(final int port, final ApplicationContext applicationContext) {
        packetManager = new PacketManager();
        activeConnections = new ConcurrentHashMap<>();
        latestLiveSessionStates = new ConcurrentHashMap<>();
        liveSessionSpectators = new ConcurrentHashMap<>();
        activeParentChallenges = new ConcurrentHashMap<>();

        // init spring boot context.
        context = applicationContext;

        parentService = context.getBean(ParentService.class);
        taskService = context.getBean(TaskService.class);
        childService = context.getBean(ChildService.class);
        goalService = context.getBean(GoalService.class);
        gameSessionService = context.getBean(GameSessionService.class);
        learningProfileService = context.getBean(LearningProfileService.class);
        courseService = context.getBean(CourseService.class);
        machineLearningService = context.getBean(MachineLearningService.class);
        parentAuthenticationService = context.getBean(ParentAuthenticationService.class);
        parentSessionService = context.getBean(ParentSessionService.class);
        packetAuthorizationPolicy = context.getBean(PacketAuthorizationPolicy.class);
        parentSecuritySessionCoordinator = context.getBean(ParentSecuritySessionCoordinator.class);

        ContainerExecution.logRunnerHealth();

        socket = new ServerSocket(port);

        socket.setReuseAddr(true);
        socket.setTcpNoDelay(true);

        // start the socket itself.
        socket.start();

        taskService.initializeGlobalTasks();
    }
}
