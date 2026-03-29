package io.github.kawase;

import io.github.kawase.client.Client;
import io.github.kawase.client.ClientHandler;
import io.github.kawase.database.services.ChildService;
import io.github.kawase.database.services.GoalService;
import io.github.kawase.database.services.LearningProfileService;
import io.github.kawase.database.services.ParentService;
import io.github.kawase.database.services.TaskService;
import io.github.kawase.packet.PacketManager;
import io.github.kawase.socket.ServerSocket;
import lombok.Getter;
import org.springframework.context.ApplicationContext;

import java.util.concurrent.ConcurrentHashMap;

@Getter
public class Server {
    @Getter
    private final static Server instance = new Server();

    private ConcurrentHashMap<Client, ClientHandler> activeConnections;

    private ConcurrentHashMap<String, ClientHandler> pendingQRLogins = new ConcurrentHashMap<>();

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
    private io.github.kawase.database.services.GameSessionService gameSessionService;
    private LearningProfileService learningProfileService;

    public void init(final int port,  final ApplicationContext applicationContext) {
        this.packetManager = new PacketManager();
        this.activeConnections = new ConcurrentHashMap<>();

        // init spring boot context.
        this.context = applicationContext;

        this.parentService = context.getBean(ParentService.class);
        this.taskService = context.getBean(TaskService.class);
        this.childService = context.getBean(ChildService.class);
        this.goalService = context.getBean(GoalService.class);
        this.gameSessionService = context.getBean(io.github.kawase.database.services.GameSessionService.class);
        this.learningProfileService = context.getBean(LearningProfileService.class);

        this.socket = new ServerSocket(port);

        socket.setReuseAddr(true);
        socket.setTcpNoDelay(true);

        // start the socket itself.
        socket.start();

        this.taskService.initializeGlobalTasks();
    }
}
