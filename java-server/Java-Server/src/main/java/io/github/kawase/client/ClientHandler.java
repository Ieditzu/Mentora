package io.github.kawase.client;

import io.github.kawase.Server;
import io.github.kawase.cpp.CppExecutor;
import io.github.kawase.packet.Packet;
import io.github.kawase.packet.impl.ai.AiResponsePacket;
import io.github.kawase.packet.impl.ai.AskAiPacket;
import io.github.kawase.packet.impl.ai.GenerateAiTaskPacket;
import io.github.kawase.packet.impl.ai.GenerateAiTaskResponsePacket;
import io.github.kawase.packet.impl.companion.CompanionSpeakPacket;
import io.github.kawase.packet.impl.companion.CompanionSpeakResponsePacket;
import io.github.kawase.packet.impl.companion.CompanionVoiceAudioPacket;
import io.github.kawase.packet.impl.companion.CompanionVoiceTextPacket;
import io.github.kawase.packet.impl.auth.*;
import io.github.kawase.packet.impl.child.*;
import io.github.kawase.packet.impl.course.FetchCourseDetailPacket;
import io.github.kawase.packet.impl.course.FetchCourseDetailResponsePacket;
import io.github.kawase.packet.impl.course.FetchPublishedCoursesPacket;
import io.github.kawase.packet.impl.course.FetchPublishedCoursesResponsePacket;
import io.github.kawase.packet.impl.course.SubmitCourseCompletionPacket;
import io.github.kawase.packet.impl.core.*;
import io.github.kawase.packet.impl.game.*;
import io.github.kawase.packet.impl.language.ExecuteCPPCodePacket;
import io.github.kawase.packet.impl.language.ExecuteCPPCodeResponsePacket;
import io.github.kawase.packet.impl.language.CodeWorldPythonRunPacket;
import io.github.kawase.packet.impl.language.CodeWorldPythonResponsePacket;
import io.github.kawase.packet.impl.language.ExecutePythonCodePacket;
import io.github.kawase.packet.impl.language.ExecutePythonCodeResponsePacket;
import io.github.kawase.packet.impl.qr.*;
import io.github.kawase.socket.ServerSocket;
import io.github.kawase.utility.GroqSpeechToText;
import lombok.Getter;
import lombok.RequiredArgsConstructor;
import org.java_websocket.WebSocket;

import java.nio.ByteBuffer;

@RequiredArgsConstructor
@Getter
public class ClientHandler {
    private final WebSocket connection;
    private final Client client;
    private final ServerSocket server;

    public void onMessage(final ByteBuffer byteBuffer) {
        int currentPacketId = -1;
        try {
            final Packet packet = Packet.construct(byteBuffer, client.getPacketManager());
            currentPacketId = packet.getId();

            // Handshake and Registration/Auth/QR Login/Verify Session are the only ones allowed before auth
            // we should prob not hard code this but oh well.
            if (currentPacketId != 1
                    && currentPacketId != 2
                    && currentPacketId != 3
                    && currentPacketId != 11
                    && currentPacketId != 13
                    && currentPacketId != 15
                    && currentPacketId != 19
                    && currentPacketId != 25
                    && currentPacketId != 32
                    && currentPacketId != 41
                    && currentPacketId != 43
                    && currentPacketId != 44
                    && currentPacketId != 47 // CompanionSpeakPacket — ARIA greets before auth
                    && currentPacketId != 58 // CompanionVoiceTextPacket — Rudolf voice can run before auth
                    && currentPacketId != 59 // CompanionVoiceAudioPacket — Rudolf server-side STT can run before auth
                    && currentPacketId != 76
                    && !client.isAuth()) {
                connection.send(new ActionResponsePacket(currentPacketId, false, "Unauthorized. Please log in first.", -1).encode());
                return;
            }

            switch (packet) {
                case HandShakePacket handShakePacket -> {
                    System.out.println("Got handshake from " + client.getHostID());
                }

                case SetClientLanguagePacket setClientLanguagePacket -> {
                    client.setLanguage(normalizeLanguageTag(setClientLanguagePacket.getLanguageTag()));
                }

                case AuthPacket authPacket -> {
                    System.out.println("Auth attempt: " + authPacket.getEmailHash());
                    final boolean success = Server.getInstance().getParentService().loginParent(
                            authPacket.getEmailHash(), authPacket.getPasswordHash()
                    );
                    if (success) {
                        final var parentOpt = Server.getInstance().getParentService().findByEmail(authPacket.getEmailHash());
                        if (parentOpt.isPresent()) {
                            final var parent = parentOpt.get();
                            client.setAuth(true);
                            client.setParentId(parent.getId());
                            connection.send(new AuthResponsePacket(true, parent.getId(), "Login successful", parent.getProfilePicture()).encode());
                        } else {
                            connection.send(new AuthResponsePacket(false, -1, "User not found", "").encode());
                        }
                    } else {
                        connection.send(new AuthResponsePacket(false, -1, "Invalid credentials", "").encode());
                    }
                }

                case RegisterParentPacket registerParentPacket -> {
                    System.out.println("Register Parent: " + registerParentPacket.getEmail());
                    final var parent = Server.getInstance().getParentService().createParentAccount(
                            registerParentPacket.getEmail(),
                            registerParentPacket.getPasswordHash()
                    );
                    client.setAuth(true);
                    client.setParentId(parent.getId());
                    connection.send(new ActionResponsePacket(packet.getId(), true, "Registered successfully", parent.getId()).encode());
                }

                case AddChildPacket addChildPacket -> {
                    System.out.println("Add Child: " + addChildPacket.getChildName());
                    final var child = Server.getInstance().getChildService().addChildToParent(
                            client.getParentId(),
                            addChildPacket.getChildName()
                    );
                    connection.send(new ActionResponsePacket(packet.getId(), true, "Child added successfully", child.getId()).encode());
                }

                case AddGoalPacket addGoalPacket -> {
                    System.out.println("Add Goal: " + addGoalPacket.getTitle());
                    // Verify ownership
                    final var child = Server.getInstance().getChildService().findById(addGoalPacket.getChildId())
                            .orElseThrow(() -> new RuntimeException("Child not found"));

                    if (!child.getParent().getId().equals(client.getParentId())) {
                        throw new RuntimeException("Access denied: This child does not belong to you.");
                    }

                    io.github.kawase.database.entity.Goal goal;
                    if (addGoalPacket.getRequiredTaskId() != -1) {
                        goal = Server.getInstance().getGoalService().createTaskGoal(
                                client.getParentId(),
                                addGoalPacket.getChildId(),
                                addGoalPacket.getTitle(),
                                addGoalPacket.getReward(),
                                addGoalPacket.getRequiredTaskId()
                        );
                    } else {
                        goal = Server.getInstance().getGoalService().createPointsGoal(
                                client.getParentId(),
                                addGoalPacket.getChildId(),
                                addGoalPacket.getTitle(),
                                addGoalPacket.getReward(),
                                addGoalPacket.getRequiredPoints()
                        );
                    }
                    connection.send(new ActionResponsePacket(packet.getId(), true, "Goal added successfully", goal.getId()).encode());

                    // Push updated goals to the child if they're online
                    for (var entry : Server.getInstance().getActiveConnections().entrySet()) {
                        if (entry.getKey().getChildId() != null && entry.getKey().getChildId() == addGoalPacket.getChildId()) {
                            final var goals = Server.getInstance().getChildService().getGoals(addGoalPacket.getChildId());
                            final var goalDtos = new java.util.ArrayList<FetchGoalsResponsePacket.GoalDto>();
                            for (final var g : goals) {
                                goalDtos.add(new FetchGoalsResponsePacket.GoalDto(
                                        g.getId(), g.getTitle(), g.getReward(), g.getIsCompleted(),
                                        g.getRequiredPoints() != null ? g.getRequiredPoints() : 0,
                                        g.getRequiredTask() != null ? g.getRequiredTask().getId() : -1L
                                ));
                            }
                            try {
                                entry.getValue().getConnection().send(new FetchGoalsResponsePacket(goalDtos).encode());
                                System.out.println("Pushed updated goals to child " + addGoalPacket.getChildId());
                            } catch (Exception ignored) {}
                            break;
                        }
                    }
                }

                case CompleteTaskPacket completeTaskPacket -> {
                    System.out.println("Complete Task: Child " + completeTaskPacket.getChildId() + ", Task " + completeTaskPacket.getTaskId());
                    
                    if (client.getChildId() != null && !client.getChildId().equals(completeTaskPacket.getChildId())) {
                        throw new RuntimeException("Access denied: You can only complete tasks for yourself.");
                    }

                    if (client.getChildId() == null) {
                        final var child = Server.getInstance().getChildService().findById(completeTaskPacket.getChildId())
                                .orElseThrow(() -> new RuntimeException("Child not found"));

                        if (!child.getParent().getId().equals(client.getParentId())) {
                            throw new RuntimeException("Access denied.");
                        }
                    }

                    Server.getInstance().getTaskService().completeTask(
                            completeTaskPacket.getChildId(),
                            completeTaskPacket.getTaskId()
                    );

                    connection.send(new ActionResponsePacket(packet.getId(), true, "Task completed", -1).encode());

                    // Notify the child's parent if they're online
                    final var completedChild = Server.getInstance().getChildService().findById(completeTaskPacket.getChildId()).orElse(null);
                    if (completedChild != null) {
                        final Long parentIdToNotify = completedChild.getParent().getId();
                        for (var entry : Server.getInstance().getActiveConnections().entrySet()) {
                            if (parentIdToNotify.equals(entry.getKey().getParentId()) && entry.getKey().getChildId() == null) {
                                try {
                                    final var task = Server.getInstance().getTaskService().getAllTasks().stream()
                                            .filter(t -> t.getId().equals(completeTaskPacket.getTaskId())).findFirst().orElse(null);
                                    String taskName = task != null ? task.getTitle() : "a task";
                                    entry.getValue().getConnection().send(
                                            new ActionResponsePacket(8, true, completedChild.getName() + " completed: " + taskName, completeTaskPacket.getChildId()).encode()
                                    );
                                } catch (Exception ignored) {}
                                break;
                            }
                        }
                    }

                    notifyParentChallengeCompleted(completeTaskPacket.getChildId());
                }

                case FetchTasksPacket fetchTasksPacket -> {
                    System.out.println("Fetch Global Tasks");
                    final var tasks = Server.getInstance().getTaskService().getAllTasks();
                    final var dtos = new java.util.ArrayList<FetchTasksResponsePacket.TaskDto>();
                    for (final var task : tasks) {
                        dtos.add(new FetchTasksResponsePacket.TaskDto(task.getId(), task.getTitle(), task.getPointValue()));
                    }
                    connection.send(new FetchTasksResponsePacket(dtos).encode());
                }

                case FetchPublishedCoursesPacket fetchPublishedCoursesPacket -> {
                    final var courses = Server.getInstance().getCourseService().getPublishedCoursesForChild(client.getChildId());
                    String json = new com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(courses);
                    connection.send(new FetchPublishedCoursesResponsePacket(json).encode());
                }

                case FetchCourseDetailPacket fetchCourseDetailPacket -> {
                    final var course = Server.getInstance().getCourseService()
                            .getPublishedCourseDetail(fetchCourseDetailPacket.getCourseId(), client.getChildId());
                    String json = new com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(course);
                    connection.send(new FetchCourseDetailResponsePacket(json).encode());
                }

                case SubmitCourseCompletionPacket submitCourseCompletionPacket -> {
                    if (client.getChildId() == null) {
                        throw new RuntimeException("Not logged in as a child.");
                    }

                    final var progress = Server.getInstance().getCourseService().recordCourseCompletion(
                            client.getChildId(),
                            submitCourseCompletionPacket.getCourseId(),
                            submitCourseCompletionPacket.getScore(),
                            submitCourseCompletionPacket.getTotalQuestions()
                    );

                    connection.send(new ActionResponsePacket(
                            packet.getId(),
                            true,
                            "Course progress saved",
                            progress.getId() == null ? -1 : progress.getId()
                    ).encode());
                }

                case FetchChildStatsPacket fetchChildStatsPacket -> {
                    if (client.getChildId() == null) {
                        throw new RuntimeException("Not logged in as a child.");
                    }
                    final var child = Server.getInstance().getChildService().findById(client.getChildId())
                            .orElseThrow(() -> new RuntimeException("Child not found"));

                    Server.getInstance().getLearningProfileService().ensureAiSummaries(child.getId());

                    String json = "{}";
                    try {
                        json = new com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(child.getGameStats());
                    } catch (Exception e) {
                        e.printStackTrace();
                    }

                    int streak = Server.getInstance().getChildService().updateStreak(client.getChildId());
                    int completedCount = Server.getInstance().getChildService().getCompletedTasks(client.getChildId()).size();
                    int totalTasks = Server.getInstance().getTaskService().getAllTasks().size();

                    connection.send(new FetchChildStatsResponsePacket(child.getName(), child.getTotalPoints(), json, streak, completedCount, totalTasks).encode());
                }

                case FetchChildStatsByParentPacket fetchChildStatsByParentPacket -> {
                    if (client.getParentId() == null) {
                        throw new RuntimeException("Not logged in as a parent.");
                    }

                    final var child = Server.getInstance().getChildService().findById(fetchChildStatsByParentPacket.getChildId())
                            .orElseThrow(() -> new RuntimeException("Child not found"));

                    if (!child.getParent().getId().equals(client.getParentId())) {
                        throw new RuntimeException("Access denied: This child does not belong to you.");
                    }

                    Server.getInstance().getLearningProfileService().ensureAiSummaries(child.getId());

                    String json = "{}";
                    try {
                        json = new com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(child.getGameStats());
                    } catch (Exception e) {
                        e.printStackTrace();
                    }

                    int streak = child.getStreak() != null ? child.getStreak() : 0;
                    int completedCount = Server.getInstance().getChildService().getCompletedTasks(child.getId()).size();
                    int totalTasks = Server.getInstance().getTaskService().getAllTasks().size();

                    connection.send(new FetchChildStatsResponsePacket(child.getName(), child.getTotalPoints(), json, streak, completedCount, totalTasks).encode());
                }

                case FetchProgrammingProfileSummaryPacket fetchProgrammingProfileSummaryPacket -> {
                    if (client.getChildId() == null) {
                        throw new RuntimeException("Not logged in as a child.");
                    }

                    final var child = Server.getInstance().getChildService().findById(client.getChildId())
                            .orElseThrow(() -> new RuntimeException("Child not found"));

                    Server.getInstance().getLearningProfileService().ensureAiSummaries(child.getId());

                    int streak = Server.getInstance().getChildService().updateStreak(client.getChildId());
                    int completedCount = Server.getInstance().getChildService().getCompletedTasks(client.getChildId()).size();
                    int totalTasks = Server.getInstance().getTaskService().getAllTasks().size();
                    String profileSummary = Server.getInstance().getLearningProfileService()
                            .buildMultiplayerProgrammingProfileSummary(child.getId());

                    connection.send(new FetchProgrammingProfileSummaryResponsePacket(
                            child.getId(),
                            child.getName(),
                            child.getTotalPoints(),
                            streak,
                            completedCount,
                            totalTasks,
                            profileSummary
                    ).encode());
                }

                case FetchChildrenPacket fetchChildrenPacket -> {
                    System.out.println("Fetch Children for Parent: " + client.getParentId());
                    final var children = Server.getInstance().getParentService().getChildren(client.getParentId());
                    final var dtos = new java.util.ArrayList<FetchChildrenResponsePacket.ChildDto>();
                    
                    // Get all active child IDs from current connections
                    java.util.Set<Long> onlineChildIds = new java.util.HashSet<>();
                    for (var entry : Server.getInstance().getActiveConnections().keySet()) {
                        if (entry.getChildId() != null) {
                            onlineChildIds.add(entry.getChildId());
                        }
                    }

                    for (final var child : children) {
                        boolean isOnline = onlineChildIds.contains(child.getId());
                        dtos.add(new FetchChildrenResponsePacket.ChildDto(child.getId(), child.getName(), child.getTotalPoints(), isOnline, child.getProfilePicture()));
                    }
                    connection.send(new FetchChildrenResponsePacket(dtos).encode());
                }

                case FetchAllChildrenPacket fetchAllChildrenPacket -> {
                    System.out.println("Fetch All Children");
                    final var children = Server.getInstance().getChildService().findAllChildren();
                    final var dtos = new java.util.ArrayList<FetchAllChildrenResponsePacket.ChildDto>();

                    java.util.Set<Long> onlineChildIds = new java.util.HashSet<>();
                    for (var entry : Server.getInstance().getActiveConnections().keySet()) {
                        if (entry.getChildId() != null) {
                            onlineChildIds.add(entry.getChildId());
                        }
                    }

                    for (final var child : children) {
                        boolean isOnline = onlineChildIds.contains(child.getId());
                        dtos.add(new FetchAllChildrenResponsePacket.ChildDto(
                                child.getId(),
                                child.getName(),
                                child.getTotalPoints(),
                                isOnline,
                                child.getProfilePicture()
                        ));
                    }

                    connection.send(new FetchAllChildrenResponsePacket(dtos).encode());
                }

                case DevCreateChildProfilePacket devCreateChildProfilePacket -> {
                    final String requestedName = devCreateChildProfilePacket.getChildName() == null ? "" : devCreateChildProfilePacket.getChildName().trim();
                    final String safeName = requestedName.isEmpty() ? "DevKid" : requestedName;
                    final var child = Server.getInstance().getChildService().createDevChildProfile(safeName);
                    connection.send(new ActionResponsePacket(packet.getId(), true, "Dev child profile created", child.getId()).encode());
                }

                case DevLoginAsChildPacket devLoginAsChildPacket -> {
                    final var childOpt = Server.getInstance().getChildService().findById(devLoginAsChildPacket.getChildId());
                    if (childOpt.isPresent()) {
                        final var child = childOpt.get();
                        final String sessionToken = java.util.UUID.randomUUID().toString();
                        Server.getInstance().getGameSessionService().createOrUpdateSession(child.getId(), sessionToken);

                        client.setAuth(true);
                        client.setChildId(child.getId());
                        client.setParentId(child.getParent() != null ? child.getParent().getId() : null);

                        connection.send(new ChildAuthResponsePacket(true, child.getId(), child.getName(), sessionToken).encode());
                        notifyChildOnlineState(child.getId(), child.getName());
                        pushActiveParentChallengeToChild(child.getId());
                    } else {
                        connection.send(new ChildAuthResponsePacket(false, -1, "", "").encode());
                    }
                }

                case FetchGoalsPacket fetchGoalsPacket -> {
                    long targetChildId = fetchGoalsPacket.getChildId();

                    // If childId is -1 or 0, use the logged-in child's own ID
                    if (targetChildId <= 0 && client.getChildId() != null) {
                        targetChildId = client.getChildId();
                    }

                    System.out.println("Fetch Goals for Child ID: " + targetChildId);
                    final var child = Server.getInstance().getChildService().findById(targetChildId)
                            .orElseThrow(() -> new RuntimeException("Child not found"));

                    // Allow if: parent owns this child OR child is fetching their own goals
                    if (client.getChildId() != null) {
                        if (!client.getChildId().equals(targetChildId)) {
                            throw new RuntimeException("Access denied: You can only view your own goals.");
                        }
                    } else if (!child.getParent().getId().equals(client.getParentId())) {
                        throw new RuntimeException("Access denied.");
                    }

                    final var goals = Server.getInstance().getChildService().getGoals(targetChildId);
                    final var dtos = new java.util.ArrayList<FetchGoalsResponsePacket.GoalDto>();
                    for (final var goal : goals) {
                        dtos.add(new FetchGoalsResponsePacket.GoalDto(
                                goal.getId(),
                                goal.getTitle(),
                                goal.getReward(),
                                goal.getIsCompleted(),
                                goal.getRequiredPoints() != null ? goal.getRequiredPoints() : 0,
                                goal.getRequiredTask() != null ? goal.getRequiredTask().getId() : -1L
                        ));
                    }
                    connection.send(new FetchGoalsResponsePacket(dtos).encode());
                }

                case FetchCompletedTasksPacket fetchCompletedTasksPacket -> {
                    System.out.println("Fetch Completed Tasks for Child: " + fetchCompletedTasksPacket.getChildId());
                    final var child = Server.getInstance().getChildService().findById(fetchCompletedTasksPacket.getChildId())
                            .orElseThrow(() -> new RuntimeException("Child not found"));

                    if (!child.getParent().getId().equals(client.getParentId())) {
                        throw new RuntimeException("Access denied.");
                    }

                    final var completedTasks = Server.getInstance().getChildService().getCompletedTasks(fetchCompletedTasksPacket.getChildId());
                    final var dtos = new java.util.ArrayList<FetchCompletedTasksResponsePacket.CompletedTaskDto>();
                    for (final var ct : completedTasks) {
                        final var fmt = java.time.format.DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm");
                        dtos.add(new FetchCompletedTasksResponsePacket.CompletedTaskDto(
                                ct.getId(),
                                ct.getTask().getTitle(),
                                ct.getTask().getPointValue(),
                                ct.getCompletedAt().format(fmt)
                        ));
                    }
                    connection.send(new FetchCompletedTasksResponsePacket(dtos).encode());
                }

                case GenerateQRLoginPacket generateQRLoginPacket -> {
                    final String token = java.util.UUID.randomUUID().toString();
                    System.out.println("Generating QR token: " + token);
                    Server.getInstance().getPendingQRLogins().put(token, this);
                    connection.send(new QRLoginResponsePacket(token).encode());
                }

                case ClaimQRLoginPacket claimQRLoginPacket -> {
                    System.out.println("Claiming QR token: " + claimQRLoginPacket.getToken() + " for child " + claimQRLoginPacket.getChildId());
                    final ClientHandler gameHandler = Server.getInstance().getPendingQRLogins().remove(claimQRLoginPacket.getToken());
                    
                    if (gameHandler != null && gameHandler.getConnection().isOpen()) {
                        final var childOpt = Server.getInstance().getChildService().findById(claimQRLoginPacket.getChildId());
                        if (childOpt.isPresent()) {
                            final var child = childOpt.get();
                            
                            // Verify that the parent claiming the token actually owns this child
                            if (child.getParent().getId().equals(client.getParentId())) {
                                final String sessionToken = java.util.UUID.randomUUID().toString();
                                Server.getInstance().getGameSessionService().createOrUpdateSession(child.getId(), sessionToken);

                                gameHandler.getClient().setAuth(true);
                                gameHandler.getClient().setChildId(child.getId());
                                gameHandler.getClient().setParentId(child.getParent().getId());
                                
                                gameHandler.getConnection().send(new ChildAuthResponsePacket(true, child.getId(), child.getName(), sessionToken).encode());
                                gameHandler.notifyChildOnlineState(child.getId(), child.getName());
                                gameHandler.pushActiveParentChallengeToChild(child.getId());
                                connection.send(new ActionResponsePacket(packet.getId(), true, "Child logged into game successfully", child.getId()).encode());
                            } else {
                                connection.send(new ActionResponsePacket(packet.getId(), false, "Access denied: You don't own this child", -1).encode());
                            }
                        } else {
                            connection.send(new ActionResponsePacket(packet.getId(), false, "Child not found", -1).encode());
                        }
                    } else {
                        connection.send(new ActionResponsePacket(packet.getId(), false, "Invalid or expired QR code", -1).encode());
                    }
                }

                case VerifySessionPacket verifySessionPacket -> {
                    System.out.println("Verifying session for child " + verifySessionPacket.getChildId());
                    final boolean isValid = Server.getInstance().getGameSessionService().verifySession(
                            verifySessionPacket.getChildId(), 
                            verifySessionPacket.getSessionToken()
                    );
                    
                    if (isValid) {
                        final var childOpt = Server.getInstance().getChildService().findById(verifySessionPacket.getChildId());
                        if (childOpt.isPresent()) {
                            final var child = childOpt.get();
                            client.setAuth(true);
                            client.setChildId(child.getId());
                            client.setParentId(child.getParent().getId());
                            
                            connection.send(new ChildAuthResponsePacket(true, child.getId(), child.getName(), verifySessionPacket.getSessionToken()).encode());
                            notifyChildOnlineState(child.getId(), child.getName());
                            pushActiveParentChallengeToChild(child.getId());
                        } else {
                            connection.send(new ChildAuthResponsePacket(false, -1, "", "").encode());
                        }
                    } else {
                        connection.send(new ChildAuthResponsePacket(false, -1, "", "").encode());
                    }
                }

                case UpdatePfpPacket updatePfpPacket -> {
                    if (updatePfpPacket.getChildId() == -1) {
                        System.out.println("Update Parent PFP: " + client.getParentId());
                        Server.getInstance().getParentService().updatePfp(client.getParentId(), updatePfpPacket.getBase64Pfp());
                    } else {
                        System.out.println("Update Child PFP: " + updatePfpPacket.getChildId());
                        // Verify ownership
                        final var child = Server.getInstance().getChildService().findById(updatePfpPacket.getChildId())
                                .orElseThrow(() -> new RuntimeException("Child not found"));
                        if (!child.getParent().getId().equals(client.getParentId())) {
                            throw new RuntimeException("Access denied.");
                        }
                        Server.getInstance().getChildService().updatePfp(updatePfpPacket.getChildId(), updatePfpPacket.getBase64Pfp());
                    }
                    connection.send(new ActionResponsePacket(packet.getId(), true, "PFP updated successfully", -1).encode());
                }

                case RemoveChildPacket removeChildPacket -> {
                    System.out.println("Remove Child: " + removeChildPacket.getChildId());
                    final var child = Server.getInstance().getChildService().findById(removeChildPacket.getChildId())
                            .orElseThrow(() -> new RuntimeException("Child not found"));

                    if (!child.getParent().getId().equals(client.getParentId())) {
                        throw new RuntimeException("Access denied: This child does not belong to you.");
                    }

                    Server.getInstance().getChildService().deleteChild(removeChildPacket.getChildId());
                    connection.send(new ActionResponsePacket(packet.getId(), true, "Child removed successfully", removeChildPacket.getChildId()).encode());
                }

                case ExecuteCPPCodePacket executeCPPCodePacket -> {
                    final CppExecutor.ExecutionResult executionResult = CppExecutor.execute(
                            executeCPPCodePacket.getCode(), 120
                    );

                    connection.send(new ExecuteCPPCodeResponsePacket(executionResult.getOutput(), executionResult.getError()).encode());
                }

                case ExecutePythonCodePacket executePythonCodePacket -> {
                    final io.github.kawase.python.PythonExecutor.ExecutionResult executionResult =
                            io.github.kawase.python.PythonExecutor.execute(executePythonCodePacket.getCode(), 120);

                    connection.send(new ExecutePythonCodeResponsePacket(executionResult.getOutput(), executionResult.getError()).encode());
                }

                case CodeWorldPythonRunPacket codeWorldPythonRunPacket -> {
                    final io.github.kawase.python.CodeWorldPythonExecutor.ExecutionResult executionResult =
                            io.github.kawase.python.CodeWorldPythonExecutor.execute(codeWorldPythonRunPacket.getCode(), 12);

                    connection.send(new CodeWorldPythonResponsePacket(
                            codeWorldPythonRunPacket.getRequestId(),
                            executionResult.getCommandsText(),
                            executionResult.getOutput(),
                            executionResult.getError()
                    ).encode());
                }

                case AskAiPacket askAiPacket -> {
                    System.out.println("AI Question from " + (client.getChildId() != null ? "child " + client.getChildId() : "parent " + client.getParentId()) + ": " + askAiPacket.getQuestion());
                    
                    io.github.kawase.utility.GroqAI ai = new io.github.kawase.utility.GroqAI();
                    String profileContext = "";
                    if (client.getChildId() != null) {
                        Server.getInstance().getLearningProfileService().recordAiInteraction(client.getChildId(), askAiPacket.getContext(), askAiPacket.getQuestion());
                        String language = null;
                        if (askAiPacket.getContext() != null) {
                            String ctx = askAiPacket.getContext().toLowerCase();
                            if (ctx.contains("cpp")) {
                                language = "cpp";
                            } else if (ctx.contains("python") || ctx.contains("py")) {
                                language = "python";
                            }
                        }
                        profileContext = Server.getInstance().getLearningProfileService().buildAiHelpProfileContext(client.getChildId(), language);
                    }

                    String response = ai.ask(
                            askAiPacket.getQuestion(),
                            askAiPacket.getContext(),
                            profileContext,
                            client.getLanguage()
                    );
                    
                    connection.send(new AiResponsePacket(response).encode());
                }

                case RecordLearningEventPacket recordLearningEventPacket -> {
                    if (client.getChildId() == null) {
                        throw new RuntimeException("Not logged in as a child.");
                    }

                    Server.getInstance().getLearningProfileService().recordLearningEvent(
                            client.getChildId(),
                            recordLearningEventPacket.getEventType(),
                            recordLearningEventPacket.getTopic(),
                            recordLearningEventPacket.getCorrectness(),
                            recordLearningEventPacket.getDetails()
                    );
                }

                case SubscribeLiveSessionPacket subscribeLiveSessionPacket -> {
                    if (client.getParentId() == null) {
                        throw new RuntimeException("Not logged in as a parent.");
                    }

                    final var child = verifyParentOwnsChild(subscribeLiveSessionPacket.getChildId());
                    if (subscribeLiveSessionPacket.isSubscribe()) {
                        Server.getInstance().getLiveSessionSpectators()
                                .computeIfAbsent(child.getId(), id -> java.util.concurrent.ConcurrentHashMap.newKeySet())
                                .add(this);
                    } else {
                        final var spectators = Server.getInstance().getLiveSessionSpectators().get(child.getId());
                        if (spectators != null) {
                            spectators.remove(this);
                        }
                    }

                    sendCurrentLiveSession(child.getId(), child.getName());
                }

                case LiveSessionUpdatePacket liveSessionUpdatePacket -> {
                    if (client.getChildId() == null) {
                        throw new RuntimeException("Not logged in as a child.");
                    }
                    if (!client.getChildId().equals(liveSessionUpdatePacket.getChildId())) {
                        throw new RuntimeException("Access denied: You can only update your own live session.");
                    }
                    broadcastLiveSessionUpdate(liveSessionUpdatePacket);
                }

                case SendParentChallengePacket sendParentChallengePacket -> {
                    if (client.getParentId() == null) {
                        throw new RuntimeException("Not logged in as a parent.");
                    }

                    final var child = verifyParentOwnsChild(sendParentChallengePacket.getChildId());
                    String message = sendParentChallengePacket.getMessage() == null ? "" : sendParentChallengePacket.getMessage().trim();
                    if (message.isBlank()) {
                        throw new RuntimeException("Challenge message cannot be empty.");
                    }
                    if (message.length() > 240) {
                        message = message.substring(0, 240);
                    }

                    final String challengeId = java.util.UUID.randomUUID().toString();
                    final ParentChallengePacket challengePacket = new ParentChallengePacket(
                            challengeId,
                            child.getId(),
                            message,
                            java.time.Instant.now().toString()
                    );
                    Server.getInstance().getActiveParentChallenges().put(child.getId(), challengePacket);

                    boolean delivered = false;
                    for (var entry : Server.getInstance().getActiveConnections().entrySet()) {
                        if (child.getId().equals(entry.getKey().getChildId())) {
                            try {
                                entry.getValue().getConnection().send(challengePacket.encode());
                                delivered = true;
                            } catch (Exception ignored) {}
                        }
                    }

                    connection.send(new ActionResponsePacket(
                            packet.getId(),
                            true,
                            delivered ? "Challenge sent to the game" : "Challenge queued for the next game login",
                            child.getId()
                    ).encode());
                }

                case FetchWeeklyReportPacket fetchWeeklyReportPacket -> {
                    if (client.getParentId() == null) {
                        throw new RuntimeException("Not logged in as a parent.");
                    }

                    final var child = verifyParentOwnsChild(fetchWeeklyReportPacket.getChildId());
                    final java.time.LocalDate weekStart = java.time.LocalDate.now()
                            .with(java.time.temporal.TemporalAdjusters.previousOrSame(java.time.DayOfWeek.MONDAY));
                    final java.time.LocalDate weekEnd = weekStart.plusDays(6);
                    final String report = Server.getInstance().getLearningProfileService()
                            .generateWeeklyParentReport(child.getId(), client.getLanguage());
                    final boolean aiGenerated = report != null && !report.startsWith("AI Error");

                    connection.send(new WeeklyReportResponsePacket(
                            child.getId(),
                            child.getName(),
                            weekStart.toString(),
                            weekEnd.toString(),
                            report == null ? "" : report,
                            aiGenerated
                    ).encode());
                }

                case GenerateAiTaskPacket generateAiTaskPacket -> {
                    if (client.getChildId() == null) {
                        throw new RuntimeException("Not logged in as a child.");
                    }

                    System.out.println("GenerateAiTask for child " + client.getChildId() + ", lang=" + generateAiTaskPacket.getLanguage());
                    final java.util.Map<String, String> generated = Server.getInstance()
                            .getLearningProfileService()
                            .generatePersonalizedTask(client.getChildId(), generateAiTaskPacket.getLanguage());

                    if (generated == null) {
                        connection.send(new ActionResponsePacket(packet.getId(), false, "AI task generation failed — try again.", -1).encode());
                        return;
                    }

                    // Persist as a real task so it can be completed and tracked
                    io.github.kawase.database.entity.Task aiTask = new io.github.kawase.database.entity.Task();
                    aiTask.setTitle(generated.get("title"));
                    aiTask.setDescription(generated.get("description"));
                    aiTask.setCodeTemplate(generated.get("codeTemplate"));
                    aiTask.setAiGenerated(true);
                    int pts = 20;
                    try { pts = Integer.parseInt(generated.get("pointValue")); } catch (Exception ignored) {}
                    aiTask.setPointValue(pts);

                    io.github.kawase.database.entity.Task saved = Server.getInstance().getTaskService().saveTask(aiTask);

                    connection.send(new GenerateAiTaskResponsePacket(
                            saved.getId(),
                            saved.getTitle(),
                            generated.get("description"),
                            generated.get("codeTemplate"),
                            generated.getOrDefault("language", "python"),
                            saved.getPointValue()
                    ).encode());
                }

                case CompanionSpeakPacket companionSpeakPacket -> {
                    // Companion lines are generated even before full auth (companion greets on load),
                    // so we use childId if available, else null for a generic line.
                    System.out.println("CompanionSpeak trigger=" + companionSpeakPacket.getTrigger());
                    final String[] lineAndEmotion = Server.getInstance()
                            .getLearningProfileService()
                            .generateCompanionLine(client.getChildId(), companionSpeakPacket.getTrigger());

                    String line = (lineAndEmotion != null) ? lineAndEmotion[0] : "Hey, I'm here if you need me!";
                    String emotion = (lineAndEmotion != null) ? lineAndEmotion[1] : "encouraging";
                    connection.send(new CompanionSpeakResponsePacket(line, emotion).encode());
                }

                case CompanionVoiceTextPacket companionVoiceTextPacket -> {
                    System.out.println("CompanionVoice transcript=" + companionVoiceTextPacket.getTranscript());
                    final String[] lineAndEmotion = Server.getInstance()
                            .getLearningProfileService()
                            .generateCompanionVoiceReply(
                                    client.getChildId(),
                                    companionVoiceTextPacket.getTranscript(),
                                    companionVoiceTextPacket.getContext()
                            );

                    String line = (lineAndEmotion != null) ? lineAndEmotion[0] : "I heard you. Tell me a bit more.";
                    String emotion = (lineAndEmotion != null) ? lineAndEmotion[1] : "encouraging";
                    if (lineAndEmotion == null || line == null || line.isBlank()) {
                        connection.send(new CompanionSpeakResponsePacket("", "ignore", companionVoiceTextPacket.getTranscript()).encode());
                        return;
                    }
                    connection.send(new CompanionSpeakResponsePacket(line, emotion, companionVoiceTextPacket.getTranscript()).encode());
                }

                case CompanionVoiceAudioPacket companionVoiceAudioPacket -> {
                    final String transcript = new GroqSpeechToText().transcribePcm16(
                            companionVoiceAudioPacket.getPcm16(),
                            companionVoiceAudioPacket.getSampleRate()
                    );
                    if (transcript == null || transcript.isBlank()) {
                        connection.send(new CompanionSpeakResponsePacket("", "ignore", "").encode());
                        return;
                    }

                    System.out.println("CompanionVoice server transcript=" + transcript);
                    final String[] lineAndEmotion = Server.getInstance()
                            .getLearningProfileService()
                            .generateCompanionVoiceReply(
                                    client.getChildId(),
                                    transcript,
                                    companionVoiceAudioPacket.getContext()
                            );

                    String line = (lineAndEmotion != null) ? lineAndEmotion[0] : "";
                    String emotion = (lineAndEmotion != null) ? lineAndEmotion[1] : "encouraging";
                    if (lineAndEmotion == null || line == null || line.isBlank()) {
                        connection.send(new CompanionSpeakResponsePacket("", "ignore", transcript).encode());
                        return;
                    }
                    connection.send(new CompanionSpeakResponsePacket(line, emotion, transcript).encode());
                }

                default -> throw new IllegalStateException("Unexpected Packet: " + packet);
            }
        } catch (Exception e) {
            e.printStackTrace();
            if (currentPacketId != -1 && connection.isOpen()) {
                connection.send(new ActionResponsePacket(currentPacketId, false, e.getMessage() != null ? e.getMessage() : "Unknown error", -1).encode());
            }
        }
    }

    private io.github.kawase.database.entity.Child verifyParentOwnsChild(final long childId) {
        final var child = Server.getInstance().getChildService().findById(childId)
                .orElseThrow(() -> new RuntimeException("Child not found"));

        if (child.getParent() == null || !child.getParent().getId().equals(client.getParentId())) {
            throw new RuntimeException("Access denied: This child does not belong to you.");
        }

        return child;
    }

    private String normalizeLanguageTag(final String languageTag) {
        if (languageTag == null) return "en";

        return switch (languageTag) {
            case "en", "ro", "es", "fr", "de", "it", "pt-BR", "pl", "tr", "uk" -> languageTag;
            default -> "en";
        };
    }

    private void sendCurrentLiveSession(final long childId, final String childName) {
        LiveSessionUpdatePacket current = Server.getInstance().getLatestLiveSessionStates().get(childId);
        if (current == null) {
            current = new LiveSessionUpdatePacket(
                    childId,
                    childName,
                    isChildOnline(childId),
                    "",
                    "",
                    0,
                    false,
                    isChildOnline(childId) ? "Online" : "Offline",
                    java.time.Instant.now().toString()
            );
        }

        connection.send(current.encode());
    }

    private boolean isChildOnline(final long childId) {
        for (var activeClient : Server.getInstance().getActiveConnections().keySet()) {
            if (Long.valueOf(childId).equals(activeClient.getChildId())) {
                return true;
            }
        }
        return false;
    }

    private void broadcastLiveSessionUpdate(final LiveSessionUpdatePacket update) {
        Server.getInstance().getLatestLiveSessionStates().put(update.getChildId(), update);
        final var spectators = Server.getInstance().getLiveSessionSpectators().get(update.getChildId());
        if (spectators == null || spectators.isEmpty()) {
            return;
        }

        for (ClientHandler spectator : spectators) {
            if (spectator == null || spectator.getConnection() == null || !spectator.getConnection().isOpen()) {
                spectators.remove(spectator);
                continue;
            }
            try {
                spectator.getConnection().send(update.encode());
            } catch (Exception ignored) {
                spectators.remove(spectator);
            }
        }
    }

    private void notifyChildOnlineState(final long childId, final String childName) {
        LiveSessionUpdatePacket update = new LiveSessionUpdatePacket(
                childId,
                childName,
                true,
                "",
                "",
                0,
                false,
                "Online",
                java.time.Instant.now().toString()
        );
        broadcastLiveSessionUpdate(update);
    }

    private void notifyChildOfflineState() {
        if (client.getChildId() == null) {
            return;
        }

        long childId = client.getChildId();
        String childName = Server.getInstance().getChildService().findById(childId)
                .map(io.github.kawase.database.entity.Child::getName)
                .orElse("");
        LiveSessionUpdatePacket update = new LiveSessionUpdatePacket(
                childId,
                childName,
                false,
                "",
                "",
                0,
                false,
                "Offline",
                java.time.Instant.now().toString()
        );
        broadcastLiveSessionUpdate(update);
    }

    private void pushActiveParentChallengeToChild(final long childId) {
        ParentChallengePacket challengePacket = Server.getInstance().getActiveParentChallenges().get(childId);
        if (challengePacket == null || connection == null || !connection.isOpen()) {
            return;
        }

        try {
            connection.send(challengePacket.encode());
        } catch (Exception ignored) {}
    }

    private void notifyParentChallengeCompleted(final long childId) {
        ParentChallengePacket activeChallenge = Server.getInstance().getActiveParentChallenges().remove(childId);
        if (activeChallenge == null) {
            return;
        }

        ParentChallengeCompletedPacket completedPacket = new ParentChallengeCompletedPacket(
                activeChallenge.getChallengeId(),
                childId,
                activeChallenge.getMessage(),
                java.time.Instant.now().toString()
        );

        final var child = Server.getInstance().getChildService().findById(childId).orElse(null);
        if (child == null || child.getParent() == null) {
            return;
        }

        Long parentId = child.getParent().getId();
        for (var entry : Server.getInstance().getActiveConnections().entrySet()) {
            if (parentId.equals(entry.getKey().getParentId()) && entry.getKey().getChildId() == null) {
                try {
                    entry.getValue().getConnection().send(completedPacket.encode());
                } catch (Exception ignored) {}
            }
        }
    }

    private void removeFromLiveSessionSpectators() {
        for (var spectators : Server.getInstance().getLiveSessionSpectators().values()) {
            spectators.remove(this);
        }
    }

    public void onOpen() {

    }

    public void onClose() {
        removeFromLiveSessionSpectators();
        notifyChildOfflineState();
    }
}
