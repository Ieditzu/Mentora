# Mentora AI Handoff

Date: 2026-03-30

This file is a neutral project handoff intended for another AI or engineer. It summarizes the repository structure, the main runtime components, key entry points, the shared protocol, and the main feature flows.

## Repository Overview

The repository has three main subprojects:

- `java-server/Java-Server`
- `kotlin-app`
- `unity`

Top-level structure:

- `java-server/Java-Server`: Java backend using Spring Boot, Spring Data JPA, PostgreSQL, and a custom WebSocket server
- `kotlin-app`: Android parent-facing app written in Kotlin with Jetpack Compose
- `unity`: Unity child-facing game project

There is no meaningful top-level README. `README.md` currently only contains `persona`.

## Product Model

Mentora is structured as:

- a backend server that stores parent/child accounts, tasks, goals, completed tasks, child sessions, and learning-profile data
- a Kotlin Android app used by a parent
- a Unity game used by a child

Main product loop:

1. Parent registers or logs in through the Android app.
2. Parent creates one or more child profiles.
3. Child logs into the Unity game via QR pairing or session restore.
4. Unity gameplay completes tasks, runs code challenges, calls AI helpers/evaluation, and records learning events.
5. The server persists task progress, points, goals, and child learning telemetry.
6. The Android app displays children, history, goals, online state, and AI-generated learning summaries.

## Java Server

Path:

- `java-server/Java-Server`

### Build and Runtime

Build file:

- `java-server/Java-Server/build.gradle.kts`

Important dependencies:

- Spring Boot 3.2.x
- Spring Data JPA
- PostgreSQL driver
- Java-WebSocket
- Jackson
- org.json
- Hypersistence Hibernate JSON support

Java toolchain:

- Java 21

Main entry point:

- `java-server/Java-Server/src/main/java/io/github/kawase/StartServer.java`

Startup flow:

1. Spring Boot application context starts.
2. `Server.getInstance().init(49154, context)` is called.
3. `Server` creates a `ServerSocket` WebSocket server and starts it.
4. `TaskService.initializeGlobalTasks()` seeds global tasks if task table is empty.

Important files:

- `java-server/Java-Server/src/main/java/io/github/kawase/StartServer.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/Server.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/socket/ServerSocket.java`

### Configuration

Application properties:

- `java-server/Java-Server/src/main/resources/application.properties`

Current DB config:

- PostgreSQL URL: `jdbc:postgresql://localhost:5432/neuroKey`
- username: `kawase`
- password: `root`
- `spring.jpa.hibernate.ddl-auto=update`
- SQL logging enabled

### Server Runtime Shape

`Server` is a singleton container for:

- active WebSocket connections
- pending QR logins
- packet manager
- Spring context-derived services

Fields on `Server` include:

- `activeConnections`
- `pendingQRLogins`
- `packetManager`
- `parentService`
- `taskService`
- `childService`
- `goalService`
- `gameSessionService`
- `learningProfileService`

### Socket Layer

`ServerSocket` extends `WebSocketServer`.

On new connection:

1. A `Client` is created.
2. A `ClientHandler` is attached to the connection.
3. The `(Client, ClientHandler)` pair is inserted into `activeConnections`.

Binary messages are forwarded to `ClientHandler.onMessage(ByteBuffer)`.

String messages are treated as invalid and the connection is closed.

### ClientHandler

Main file:

- `java-server/Java-Server/src/main/java/io/github/kawase/client/ClientHandler.java`

This is the main message dispatch class. It:

- decrypts and constructs packets
- checks auth gating
- routes packets through a switch expression
- sends action or response packets
- performs ownership checks
- integrates with services

Packets allowed before auth:

- `1` HandShake
- `2` Auth
- `3` RegisterParent
- `19` GenerateQRLogin
- `25` VerifySession

Everything else requires the connection to be authenticated.

### Domain Entities

Entity files:

- `java-server/Java-Server/src/main/java/io/github/kawase/database/entity/Parent.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/database/entity/Child.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/database/entity/Task.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/database/entity/Goal.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/database/entity/CompletedTask.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/database/entity/GameSession.java`

#### Parent

Fields:

- `id`
- `email`
- `passwordHash`
- `profilePicture`
- `childEntities`
- `goals`

#### Child

Fields:

- `id`
- `parent`
- `name`
- `profilePicture`
- `gameStats` as `jsonb`
- `totalPoints`
- `streak`
- `lastLoginDate`
- `completedTasks`
- `goals`

`gameStats` is the flexible storage used for learning analytics and AI summaries.

#### Task

Fields:

- `id`
- `title`
- `pointValue`

#### Goal

Fields:

- `id`
- `parent`
- `child`
- `title`
- `reward`
- `requiredPoints`
- `requiredTask`
- `isCompleted`
- `completedAt`

#### CompletedTask

Fields:

- `id`
- `child`
- `task`
- `completedAt`

#### GameSession

Used to persist child session tokens for QR login and auto-login.

### Services

Service files:

- `java-server/Java-Server/src/main/java/io/github/kawase/database/services/ParentService.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/database/services/ChildService.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/database/services/TaskService.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/database/services/GoalService.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/database/services/GameSessionService.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/database/services/LearningProfileService.java`

#### ParentService

Provides:

- create parent account
- login parent
- lookup by email or ID
- update parent profile picture
- fetch children for a parent

#### ChildService

Provides:

- add child to parent
- lookup child by ID
- fetch goals for a child
- update child profile picture
- delete child
- update streak
- fetch completed tasks

#### TaskService

Provides:

- seed global tasks
- fetch all tasks
- complete a task for a child

Task completion behavior:

1. Save `CompletedTask`
2. Add task point value to `child.totalPoints`
3. Increment `tasks_completed` in `child.gameStats`
4. Save child
5. Check goal completion through `GoalService`

#### GoalService

Provides:

- create points-based goal
- create task-based goal
- remove goal
- check and complete goals when a task is completed

#### GameSessionService

Provides:

- create or replace session token for a child
- verify session token by child ID

#### LearningProfileService

This is the main learning telemetry service.

It provides:

- `recordLearningEvent`
- `recordAiInteraction`
- `buildProfileSummary`
- `ensureAiSummaries`

It stores and updates nested maps inside `Child.gameStats`.

Used profile keys:

- `aiProfileCpp`
- `aiProfilePython`
- `aiProfileGeneral`

Stored/derived fields include:

- `totalInteractions`
- `correctCount`
- `incorrectCount`
- `hintsUsed`
- `chatTurns`
- `topics`
- `concepts`
- `mistakes`
- `recentEvents`
- `lastUpdated`
- `summaryText`
- `summaryOneLine`
- `summaryThreeLine`
- `summaryUpdated`

It also derives:

- strengths
- needs-help topics
- struggle concepts
- common mistakes
- help-request topics

AI summaries are generated lazily using `GroqAI`.

### Global Task Catalog

Task enum:

- `java-server/Java-Server/src/main/java/io/github/kawase/database/entity/enums/DefaultTaskType.java`

Default tasks cover:

- C++ starter quiz
- C++ medium debugging tasks
- C++ hard code challenges
- Python medium practice tasks
- Python hard visual tasks
- logic puzzles

The task titles are used by the Unity game when it auto-completes matching tasks.

### AI Utility

File:

- `java-server/Java-Server/src/main/java/io/github/kawase/utility/GroqAI.java`

Behavior:

- loads the Groq API key from `java-server/Java-Server/api-keys.json`
- caches responses in memory
- handles cooldowns after 429 responses

Methods:

- `ask(question, context)`
- `ask(question, context, profileSummary)`
- `generate(prompt)`

### Code Execution Utilities

Files:

- `java-server/Java-Server/src/main/java/io/github/kawase/cpp/CppExecutor.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/python/PythonExecutor.java`

Behavior:

- write submitted code into a temp directory
- compile C++ when needed
- run code under `unshare` and `ulimit`
- enforce timeouts
- collect stdout and stderr
- delete temp directory afterward

Return model:

- `output`
- `error`
- `exitCode`
- `isTimeout`

## Kotlin Android App

Path:

- `kotlin-app`

### Build Shape

Files:

- `kotlin-app/build.gradle.kts`
- `kotlin-app/app/build.gradle.kts`
- `kotlin-app/gradle/libs.versions.toml`

App characteristics:

- Android application
- Jetpack Compose enabled
- CameraX + ML Kit for QR scanning
- Java-WebSocket client

### Android Manifest

File:

- `kotlin-app/app/src/main/AndroidManifest.xml`

Important permissions/features:

- `INTERNET`
- `ACCESS_NETWORK_STATE`
- `CAMERA`
- `POST_NOTIFICATIONS`

### MainActivity

File:

- `kotlin-app/app/src/main/java/io/github/kawase/MainActivity.kt`

Behavior:

- requests notification permission
- sets Compose content
- creates `SocketViewModel`
- chooses between auth screen and main dashboard
- starts socket connection
- shows toast messages from success and error flows

### UI Files

Main UI files:

- `kotlin-app/app/src/main/java/io/github/kawase/ui/AuthScreen.kt`
- `kotlin-app/app/src/main/java/io/github/kawase/ui/MainDashboard.kt`
- `kotlin-app/app/src/main/java/io/github/kawase/ui/SocketViewModel.kt`
- `kotlin-app/app/src/main/java/io/github/kawase/ui/theme/*`

### SocketViewModel

Main state file:

- `kotlin-app/app/src/main/java/io/github/kawase/ui/SocketViewModel.kt`

This is the main app state container.

It owns:

- socket connection
- reconnect loop
- login state
- parent ID
- saved email
- parent profile picture
- list of children
- list of tasks
- list of goals
- list of completed tasks
- parsed AI profiles per child
- app theme preferences
- success/error flows
- notification helpers

It also persists:

- `saved_email`
- `email_hash`
- `password_hash`
- theme settings

Current default socket URL:

- `wss://neuro.serenityutils.club`

Reconnect behavior:

- background reconnect loop retries every 5 seconds

Auth behavior:

- hashes email and password using local `HashUtility`
- stores those hashes in preferences
- auto-sends auth packet on reconnect if hashes exist

### App Data Models

Defined inside `SocketViewModel.kt`:

- `Child`
- `Task`
- `Goal`
- `CompletedTask`
- `AiProfile`

These are UI-side models, not shared code with the server.

### AuthScreen

File:

- `kotlin-app/app/src/main/java/io/github/kawase/ui/AuthScreen.kt`

Features:

- animated background
- login/register toggle
- email and password inputs
- connect status indicator

### MainDashboard

File:

- `kotlin-app/app/src/main/java/io/github/kawase/ui/MainDashboard.kt`

Contains:

- bottom navigation
- home screen
- QR scanner dialog
- QR camera view
- profile picture selection
- settings
- history screen
- goals screen
- AI insight cards and detail dialogs
- add-goal dialog

Screens:

- `Home`
- `History`
- `Goals`
- `Settings`

#### Home Screen

Shows:

- list of child cards
- online indicator
- point totals
- button to open QR login flow for the game

#### QR Flow in App

The app can:

- scan QR token using camera
- manually enter a token
- call `viewModel.claimQRLogin(token, child.id)`

Used to pair a child profile with a Unity game session.

#### Settings Screen

Allows:

- parent profile picture update
- dark mode toggle
- theme color selection
- child list management
- add child
- delete child
- update child profile picture
- logout
- developer/manual login fields

#### History Screen

Displays:

- grouped completed tasks
- points
- dates
- summary chips

#### Goals Screen

Displays:

- current goals for selected child
- AI insight cards for:
  - C++
  - Python
  - General

It calls `fetchChildProfile(childId)` on load.

### Android Packet Client

Packet layer files:

- `kotlin-app/app/src/main/java/io/github/kawase/socket/packet/Packet.java`
- `kotlin-app/app/src/main/java/io/github/kawase/socket/packet/PacketManager.java`
- `kotlin-app/app/src/main/java/io/github/kawase/socket/packet/impl/...`

Utility files:

- `kotlin-app/app/src/main/java/io/github/kawase/socket/utility/EncryptionUtility.java`
- `kotlin-app/app/src/main/java/io/github/kawase/socket/utility/HashUtility.java`
- `kotlin-app/app/src/main/java/io/github/kawase/socket/interfaces/Data.java`

Important note:

- Packet handling is implemented in Java inside the Android project, while the app UI and ViewModel are in Kotlin.

## Unity Game

Path:

- `unity`

### Unity Version

File:

- `unity/ProjectSettings/ProjectVersion.txt`

Version:

- `2022.3.62f3`

### Packages

File:

- `unity/Packages/manifest.json`

Important packages:

- URP
- Input System
- TextMesh Pro
- Timeline
- UGUI
- Visual Scripting
- XR Management

### Build Settings

File:

- `unity/ProjectSettings/EditorBuildSettings.asset`

Enabled scene:

- `Assets/Scenes/SampleScene.unity`

### General Runtime Scripts

There are many runtime scripts in:

- `unity/Assets/Scripts/Runtime`

The project includes:

- custom first-person / bean movement systems
- environmental interactions
- pause menu UI
- challenge and cinematic scripts
- network client

### Network Layer

Files:

- `unity/Assets/Scripts/Runtime/Network/GameClient.cs`
- `unity/Assets/Scripts/Runtime/Network/Packet.cs`
- `unity/Assets/Scripts/Runtime/Network/PacketManager.cs`
- `unity/Assets/Scripts/Runtime/Network/EncryptionUtility.cs`

`GameClient` is a persistent singleton `MonoBehaviour` with:

- `ClientWebSocket`
- `Connect()`
- `SendPacket(Packet)`
- background receive loop
- `OnPacketReceived` event

Default server URL:

- `wss://neuro.serenityutils.club`

Behavior:

- sends `HandShakePacket("unity_game")` after connection
- decodes packets and emits them through `OnPacketReceived`

### PauseMenuManager

File:

- `unity/Assets/Scripts/Runtime/PauseMenuManager.cs`

This is the main game-side account/session UI.

Responsibilities:

- bootstraps `PauseMenuManager` and `GameClient` at scene load
- attempts session restore from `Application.persistentDataPath/session.json`
- handles QR login generation
- handles child auth response
- saves session token to disk
- fetches stats/tasks/goals after login
- shows progress, streak, tasks, goals
- can complete tasks by title match

Session restore flow:

1. `ConnectAndTryAutoLogin()`
2. connect to server
3. read local `session.json` if present
4. send `VerifySessionPacket`

QR login flow:

1. Unity sends `GenerateQRLoginPacket`
2. server returns token
3. Unity downloads a QR image from `https://api.qrserver.com/...`
4. parent app scans or enters token
5. app sends `ClaimQRLoginPacket`
6. Unity receives `ChildAuthResponsePacket`

After successful child auth Unity sends:

- `FetchChildStatsPacket`
- `FetchTasksPacket`
- `FetchGoalsPacket(-1)`

### Challenge Scripts

Important files:

- `unity/Assets/Scripts/Runtime/CodeChallengePadCinematic.cs`
- `unity/Assets/Scripts/Runtime/PythonDebugPadCinematic.cs`
- `unity/Assets/Scripts/Runtime/CppQuestionPadCinematic.cs`
- `unity/Assets/Scripts/Runtime/CppQuestionTrigger.cs`
- `unity/Assets/Scripts/Runtime/CppAnswerPad.cs`

These scripts implement educational gameplay.

#### CodeChallengePadCinematic

C++ challenge flow.

Contains:

- medium and hard challenge sets
- portal and cinematic transitions
- language choice
- code input UI
- AI chat support
- hint flow
- code execution requests
- AI evaluation requests
- event recording
- task auto-completion when a challenge is solved

Mode behavior:

- medium mode uses server code execution and AI evaluation
- hard mode appears to rely on local validation for some flows

#### PythonDebugPadCinematic

Python challenge flow.

Contains:

- medium and hard challenge sets
- portal and cinematic transitions
- code input
- server-side Python execution requests
- AI evaluation
- AI chat and hints
- learning-event recording
- task auto-completion when challenge is solved

#### CppQuestionTrigger / CppAnswerPad

Used for non-code multiple-choice or movement-based answer gameplay:

- question text and code snippet are displayed in-world
- player enters pads or triggers answers
- feedback is shown
- continuation objects are revealed on success

### Runtime Bootstrap

File:

- `unity/Assets/Scripts/Runtime/FpsBootstrap.cs`

This spawns a simple FPS capsule if no existing bean or first-person controller is present.

## Shared Packet Protocol

All three runtimes implement a custom encrypted binary protocol.

### Encryption and Base Key

Key files:

- `java-server/Java-Server/src/main/java/io/github/kawase/interfaces/Data.java`
- `kotlin-app/app/src/main/java/io/github/kawase/socket/interfaces/Data.java`
- `unity/Assets/Scripts/Runtime/Network/Packet.cs`

Shared base key constant:

- `CIOCLIKESKIDSIJIJSDJ1J2313J8123869699696`

Packet flow:

1. Create dynamic seed
2. Encrypt seed using base key
3. Write packet payload with packet ID and fields
4. Encrypt payload using dynamic seed string
5. Prefix packet with encrypted seed length

### Packet IDs

Server packet registry:

- `1` HandShake
- `2` Auth
- `3` RegisterParent
- `4` AddChild
- `5` AddGoal
- `8` CompleteTask
- `9` ActionResponse
- `10` AuthResponse
- `11` FetchTasks
- `12` FetchTasksResponse
- `13` FetchGoals
- `14` FetchGoalsResponse
- `15` FetchChildren
- `16` FetchChildrenResponse
- `17` FetchCompletedTasks
- `18` FetchCompletedTasksResponse
- `19` GenerateQRLogin
- `20` QRLoginResponse
- `21` ClaimQRLogin
- `22` ChildAuthResponse
- `23` FetchChildStats
- `24` FetchChildStatsResponse
- `25` VerifySession
- `26` UpdatePfp
- `27` RemoveChild
- `28` ExecuteCPPCode
- `29` ExecuteCPPCodeResponse
- `30` AskAi
- `31` AiResponse
- `32` FetchChildStatsByParent
- `33` RecordLearningEvent
- `34` ExecutePythonCode
- `35` ExecutePythonCodeResponse

Android client only implements the subset it needs.
Unity client only implements the subset it needs.

## Main Cross-Component Flows

### Parent Registration / Login

Android app:

1. hashes email and password locally
2. sends `RegisterParentPacket` or `AuthPacket`

Server:

1. creates or validates parent
2. returns `ActionResponsePacket` or `AuthResponsePacket`

Android app:

1. sets login state
2. fetches children and tasks

### Add Child

Android app sends:

- `AddChildPacket`

Server:

- creates child attached to parent

Android app:

- refreshes child list on action response

### Goal Creation

Android app sends:

- `AddGoalPacket`

Server:

- verifies parent owns child
- creates task-goal or points-goal
- sends success action response
- pushes updated goals to online child if connected

### QR Login

Unity:

- sends `GenerateQRLoginPacket`

Server:

- creates token
- stores token -> handler mapping in `pendingQRLogins`
- returns `QRLoginResponsePacket`

Android:

- scans or enters token
- sends `ClaimQRLoginPacket(token, childId)`

Server:

- verifies token exists
- verifies parent owns child
- creates/replaces session token
- authenticates Unity client as child
- sends `ChildAuthResponsePacket`
- sends action success to Android app

### Session Restore

Unity:

- loads `session.json`
- sends `VerifySessionPacket(childId, token)`

Server:

- validates against `GameSession`
- if valid, authenticates client and returns `ChildAuthResponsePacket`

### Task Completion

Unity:

- calls `CompleteTaskPacket`

Server:

- verifies ownership or self-completion
- saves completed task
- adds points
- updates game stats
- checks goals
- notifies parent connection using an `ActionResponsePacket`

Android app:

- handles action response for request packet `8`
- sends local notification
- refreshes children

### Child Stats / AI Profile Flow

Unity child client:

- sends `FetchChildStatsPacket`

Server:

- ensures AI summaries exist
- serializes `child.gameStats`
- returns points, streak, completed count, total task count, and JSON stats

Android parent app:

- sends `FetchChildStatsByParentPacket(childId)`

Server:

- verifies ownership
- ensures AI summaries exist
- returns the same stats payload

Android app:

- parses `gameStatsJson`
- populates `AiProfile` maps for C++, Python, and General

### Code Challenge Evaluation

Unity C++ medium / Python flows:

1. send `ExecuteCPPCodePacket` or `ExecutePythonCodePacket`
2. receive execution result
3. send `AskAiPacket` for evaluation context
4. parse AI response
5. record event through `RecordLearningEventPacket`
6. auto-complete matching task if solved

## Important File List

### Server

- `java-server/Java-Server/src/main/java/io/github/kawase/StartServer.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/Server.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/client/Client.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/client/ClientHandler.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/socket/ServerSocket.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/packet/Packet.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/packet/PacketManager.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/database/services/*`
- `java-server/Java-Server/src/main/java/io/github/kawase/database/entity/*`
- `java-server/Java-Server/src/main/java/io/github/kawase/cpp/CppExecutor.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/python/PythonExecutor.java`
- `java-server/Java-Server/src/main/java/io/github/kawase/utility/GroqAI.java`

### Android

- `kotlin-app/app/src/main/java/io/github/kawase/MainActivity.kt`
- `kotlin-app/app/src/main/java/io/github/kawase/ui/SocketViewModel.kt`
- `kotlin-app/app/src/main/java/io/github/kawase/ui/AuthScreen.kt`
- `kotlin-app/app/src/main/java/io/github/kawase/ui/MainDashboard.kt`
- `kotlin-app/app/src/main/java/io/github/kawase/socket/packet/*`
- `kotlin-app/app/src/main/java/io/github/kawase/socket/utility/*`

### Unity

- `unity/Assets/Scripts/Runtime/Network/GameClient.cs`
- `unity/Assets/Scripts/Runtime/Network/Packet.cs`
- `unity/Assets/Scripts/Runtime/Network/PacketManager.cs`
- `unity/Assets/Scripts/Runtime/PauseMenuManager.cs`
- `unity/Assets/Scripts/Runtime/CodeChallengePadCinematic.cs`
- `unity/Assets/Scripts/Runtime/PythonDebugPadCinematic.cs`
- `unity/Assets/Scripts/Runtime/CppQuestionTrigger.cs`
- `unity/Assets/Scripts/Runtime/CppAnswerPad.cs`
- `unity/Assets/Scripts/Runtime/FpsBootstrap.cs`

## Current Tests

Meaningful automated coverage was not found during inspection.

Only template Android tests are present:

- `kotlin-app/app/src/test/java/io/github/kawase/ExampleUnitTest.kt`
- `kotlin-app/app/src/androidTest/java/io/github/kawase/ExampleInstrumentedTest.kt`

No meaningful server tests were found.
No Unity tests were found.

## Notes for Another AI

If continuing work on this repo, the fastest way to build context is:

1. Read `ClientHandler.java` to understand all server behaviors.
2. Read `SocketViewModel.kt` to understand the parent app state flow.
3. Read `PauseMenuManager.cs` and one challenge file to understand game-side flow.
4. Check `PacketManager` in all three runtimes when adding or changing protocol messages.
5. Check `DefaultTaskType.java` whenever task names or challenge-to-task mapping matter.

Important implementation fact:

- The codebase is conceptually one product, but there is no shared generated protocol layer. Any packet change likely requires edits in Java server, Kotlin client, and Unity client.
