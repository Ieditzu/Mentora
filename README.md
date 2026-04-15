# Mentora

**Mentora** is an AI-powered educational learning game designed to teach children programming concepts in Python and C++. It combines a 3D Unity game world, a real-time Java backend, an Android parent dashboard, and a web-based course creator into a single cohesive platform. The AI adapts its tutoring style dynamically based on each student's evolving knowledge profile, tracked across every interaction.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Component Breakdown](#component-breakdown)
  - [Java/Spring Backend](#javaspring-backend)
  - [Unity Game Client](#unity-game-client)
  - [Kotlin Android App](#kotlin-android-app)
  - [Web Course Creator](#web-course-creator)
- [Core Concepts](#core-concepts)
  - [AI Learning Profile](#ai-learning-profile)
  - [Adaptive AI Tutoring (Groq + LLaMA)](#adaptive-ai-tutoring-groq--llama)
  - [Real-Time Binary WebSocket Protocol](#real-time-binary-websocket-protocol)
  - [Authentication Flows](#authentication-flows)
  - [Course & Quiz System](#course--quiz-system)
  - [Task & Goal System](#task--goal-system)
- [Database Schema](#database-schema)
- [API Reference](#api-reference)
  - [HTTP REST (Web Creator)](#http-rest-web-creator)
  - [WebSocket Packet Protocol](#websocket-packet-protocol)
- [Technology Stack](#technology-stack)
- [Getting Started](#getting-started)

---

## Architecture Overview

Mentora follows a **multi-client, single-server** architecture. One Java backend serves all clients simultaneously over two separate protocols: an HTTP REST API for the web course creator and a binary WebSocket connection for real-time game and mobile clients.

```
┌─────────────────────────────────────────────────────────────────┐
│                        Java/Spring Backend                      │
│                                                                 │
│   HTTP REST :8085          Binary WebSocket :49154              │
│   (Web Creator API)        (Game + Mobile Protocol)             │
│         │                          │                            │
│   WebSessionService         ClientHandler                       │
│   CourseController          Server (singleton)                  │
│         │                          │                            │
│   PostgreSQL (JPA)   ←─────────────┤                            │
│         │                 LearningProfileService                │
│         │                 CourseService, ChildService           │
│         │                 GroqAI (LLaMA-3.3-70B via Groq)       │
└─────────────────────────────────────────────────────────────────┘
         ▲                           ▲               ▲
         │                           │               │
   Web Creator              Unity Game Client    Android App
   (Vite + JS)              (C# / HDRP)         (Kotlin Compose)
```

---

## Component Breakdown

### Java/Spring Backend

**Location:** `java-server/Java-Server/`

The backbone of the entire platform. Built with **Spring Boot 3.2** on **Java 21**, it manages all persistent data, business logic, AI calls, and real-time communication.

**Key responsibilities:**
- Handles all client connections (game, mobile) over a persistent binary WebSocket on port **49154**
- Exposes a REST API on port **8085** exclusively for the web course creator
- Persists all data (children, parents, courses, tasks, goals) to **PostgreSQL** via Spring Data JPA
- Maintains per-child AI learning profiles as **JSONB** columns
- Executes student-submitted Python and C++ code server-side via sandboxed runners
- Calls the **Groq AI API** (LLaMA-3.3-70B) for adaptive tutoring responses and profile summaries

**Server bootstrap:**

The application starts via `StartServer.java`, which boots the Spring context and then manually initialises the WebSocket server on its dedicated port:

```java
// StartServer.java - WebSocket starts after Spring context is ready
Server.init(49154, context);
```

The `Server` class is a singleton that holds references to all Spring services and the live `ServerSocket`, acting as the bridge between the stateless Spring DI world and the stateful WebSocket connections.

---

### Unity Game Client

**Location:** `unity/Assets/Scripts/`

A 3D game environment built in **Unity** with the **High Definition Render Pipeline (HDRP)**. Students navigate an interactive world with dedicated coding pads for Python and C++, a community island with published courses, and AI mentor interactions.

**Key scripts:**

| Script | Purpose |
|--------|---------|
| `GameClient.cs` | Singleton WebSocket client; connects to `wss://neuro.serenityutils.club` by default, serialises/deserialises all binary packets |
| `PythonDebugPadCinematic.cs` | Handles the Python coding challenge pad; sends `RecordLearningEventPacket` on run and `AskAiPacket` when the student asks for help |
| `CppQuestionPadCinematic.cs` | Same flow for C++ challenges |
| `CommunityIslandMenu.cs` | Loads published courses from the server via `FetchPublishedCoursesPacket` |

The Unity client exclusively uses the binary WebSocket protocol — it never calls the HTTP REST API.

---

### Kotlin Android App

**Location:** `kotlin-app/`

A **Jetpack Compose** Android application (target SDK 36) used by **parents** to monitor their child's learning progress and manage goals. It mirrors the same binary WebSocket protocol as the Unity client.

**Key features:**
- Parent authentication and child profile management
- Real-time progress dashboards pulling from the server
- **QR code scanning** (via **ML Kit** barcode scanning + **CameraX**) to link a child's game account
- Goal creation and tracking
- Image loading via **Coil**

---

### Web Course Creator

**Location:** `web-creator/`

A lightweight **Vite**-powered single-page application built in vanilla JavaScript. Parents and educators use it to author custom course content that gets published into the game.

**Key features:**
- Parent registration, login, and session management
- Full CRUD for courses: title, language (Python/C++), difficulty, description, quiz questions
- Each question supports four multiple-choice options with a designated correct answer and optional explanation
- Publishing/unpublishing courses so they appear in-game on the Community Island

The creator communicates exclusively with the **HTTP REST API** on port 8085, authenticated via a Bearer token stored in the browser.

---

## Core Concepts

### AI Learning Profile

Every child has a **`game_stats` JSONB column** in the database that acts as their persistent, evolving knowledge fingerprint. The `LearningProfileService` maintains three sub-profiles within this column:

| Profile Key | Scope |
|-------------|-------|
| `aiProfileCpp` | C++ specific coding history |
| `aiProfilePython` | Python specific coding history |
| `aiProfileGeneral` | Cross-language general stats |

Each profile tracks the following counters:

```json
{
  "correctCount": 14,
  "incorrectCount": 7,
  "hintsUsed": 5,
  "chatTurns": 12,
  "totalInteractions": 33,
  "topics": {
    "cpp_course:LOOPS": { "correct": 4, "incorrect": 1 }
  },
  "concepts": { ... },
  "mistakes": [ ... ],
  "recentEvents": [ ... ],
  "summaryText": "Strong grasp of loops, struggles with pointers.",
  "summaryOneLine": "Confident in iteration, developing pointer skills.",
  "summaryUpdated": 1713200000
}
```

**`recentEvents`** is a rolling window of the last 10 learning events, giving the AI recency-weighted context about what the student has been working on.

**Learning events are recorded from multiple sources:**

1. **Coding pads in Unity** — when a student runs code, `RecordLearningEventPacket` is sent with the language, topic, correctness, and any error details
2. **AI chat interactions** — every question asked to the tutor is logged via `recordAiInteraction`
3. **Course quiz submissions** — `SubmitCourseCompletionPacket` triggers `recordLearningEvent` with a synthetic topic like `python_course:FUNCBASICS`

**LLM-generated narrative summaries** (`summaryText`, `summaryOneLine`, `summaryThreeLine`) are regenerated by calling Groq when stats change, but are throttled — no more than once every 300 seconds — to avoid excessive API usage.

---

### Adaptive AI Tutoring (Groq + LLaMA)

The AI tutor is powered by **Meta's LLaMA 3.3 70B** model, accessed via the **Groq** inference API for low-latency responses.

**How adaptation works:**

When a student asks a question in-game, the server:

1. Identifies the relevant language context from the question metadata (`cpp` or `python`)
2. Calls `buildAiHelpProfileContext(childId, language)` to serialize the child's learning profile into a readable text block
3. Injects that profile block into the system prompt alongside the student's question

The constructed prompt follows this structure:

```
You are an educational AI mentor inside the Mentora learning game.
Respond in a supportive, concise way that helps the student keep thinking.
Do not give away the full solution unless the student explicitly asks for the final answer.

Student progress profile:
  [serialised LearningProfile — correct/incorrect counts, recent mistakes, topic strengths]

Context: [pad context, e.g. "CppPad - pointers exercise"]
Student request:
  [the student's question]

Keep the answer to 1-4 short sentences.
```

This means the AI *knows* whether the student has been struggling with a specific topic, how many hints they've already used, and what their recent mistakes were — and tailors its guidance accordingly, without the student needing to re-explain their situation each time.

**Parent-facing summaries** are generated with a separate structured prompt that produces `ONE:` (one-line) and `THREE:` (three-line) narrative summaries of the child's progress, intended for display in the Android parent dashboard.

**Rate limiting and caching** are handled inside `GroqAI.java` with a configurable cooldown to prevent redundant calls when the same question is asked in quick succession.

---

### Real-Time Binary WebSocket Protocol

All game and mobile communication uses a **custom binary packet protocol** over WebSocket (port 49154). This avoids JSON serialisation overhead and allows tight control over the message format.

**Packet lifecycle:**

1. Client sends a binary frame; the first byte is the **packet ID**
2. `ClientHandler.onMessage` reads the ID, delegates to `PacketManager.createPacket(id)` to deserialise the payload
3. A large `switch` expression dispatches on the concrete packet type
4. The handler checks authorisation (most packets require an authenticated session), processes business logic, then optionally writes a response packet back to the same client or broadcasts to related clients

**Key packet IDs:**

| ID | Packet | Direction |
|----|--------|-----------|
| 1 | `HandShakePacket` | Client → Server |
| 2 | `AuthPacket` | Client → Server |
| 3 | `RegisterParentPacket` | Client → Server |
| 4 | `AddChildPacket` | Client → Server |
| 5 | `AddGoalPacket` | Client → Server |
| 8 | `CompleteTaskPacket` | Client → Server |
| 30 | `AskAiPacket` | Client → Server |
| 31 | `AiResponsePacket` | Server → Client |
| 33 | `RecordLearningEventPacket` | Client → Server |
| 36 | `FetchPublishedCoursesPacket` | Client → Server |
| 37 | `FetchPublishedCoursesResponsePacket` | Server → Client |
| 38 | `FetchCourseDetailPacket` | Client → Server |
| 39 | `FetchCourseDetailResponsePacket` | Server → Client |
| 40 | `SubmitCourseCompletionPacket` | Client → Server |

Packets that do not require authentication (e.g. handshake, QR login, verify session, dev shortcuts) are whitelisted at the top of `ClientHandler.onMessage` and processed before any session check.

---

### Authentication Flows

Mentora has two distinct user types with separate authentication flows.

#### Parent Authentication (Web Creator & Android)

Parents authenticate via the HTTP REST API:

1. **Register**: `POST /api/web/auth/register` — email + password; password is stored as a **SHA-256 hash** (via `HashUtility`)
2. **Login**: `POST /api/web/auth/login` — returns a `token` (UUID) and `parentId`
3. Subsequent REST requests include `Authorization: Bearer <token>`; `WebSessionService` validates the token against an in-memory map with a **7-day TTL**

#### Child / Game Authentication (QR Flow)

Children are linked to parent accounts via a QR code pairing flow designed to work without the child needing to type credentials:

1. The Unity game sends `GenerateQRLoginPacket` → server returns a short-lived token
2. The parent scans the QR code displayed in-game using the Android app
3. The Android app sends `ClaimQRLoginPacket` (with `childId` + token) to the server
4. The server issues a `ChildAuthResponsePacket` back to the game client with a persistent `GameSession` token
5. Future sessions use `VerifySessionPacket` (childId + token) to resume without re-pairing

**Dev shortcuts** (`DevCreateChildProfilePacket`, `DevLoginAsChildPacket`) allow rapid testing without a parent account.

---

### Course & Quiz System

Courses are authored in the web creator and played by students in the Community Island area of the Unity game.

**Course structure:**

- A `Course` has metadata (title, acronym, language, difficulty, summary, description, point reward) and an ordered list of `CourseQuizQuestion` entries
- Each question has a prompt, four answer options, a `correctIndex` (0–3), and an optional explanation
- Courses are only visible in-game after a parent explicitly **publishes** them

**Completion rules:**

A course run is only considered **completed** (and rewards granted) if the student achieves a **perfect score** — all questions answered correctly:

```java
boolean completedNow = totalQuestions > 0 && score >= totalQuestions;
```

On completion:
- `child.totalPoints` is incremented by `course.pointReward` (granted only once per course)
- A `course_quiz_attempt` learning event is recorded against the child's AI profile with the topic `{language}_course:{acronym}`
- Progress is tracked in `child_course_progress` with attempt counts, scores, and timestamps

**Global Tasks** (separate from courses) are pre-seeded entries in the `tasks` table covering common programming exercises (C++ quizzes, debugging, Python practice, logic puzzles). Completing a task increments points and updates `game_stats.tasks_completed`.

---

### Task & Goal System

**Tasks** are pre-defined programming challenges seeded from `DefaultTaskType` into the `tasks` table. Any child can complete a task once for its associated point value.

**Goals** are created by parents and tied to a child. A goal specifies:
- A title and description
- A reward message for the child
- Either a **point threshold** (`required_points`) or a **specific task** (`required_task_id`) as the completion condition
- The server checks goal completion automatically when a task is completed or points are updated

---

## Database Schema

Schema is managed automatically by **Hibernate DDL auto-update**. The logical schema is:

```
parents
  ├── id (PK)
  ├── email (unique)
  └── password_hash

children
  ├── id (PK)
  ├── parent_id (FK → parents)
  ├── name
  ├── profile_picture
  ├── total_points
  ├── streak
  ├── last_login_date
  └── game_stats (JSONB) ← AI learning profiles live here

game_sessions
  ├── id (PK)
  ├── child_id (unique FK → children)
  └── session_token

tasks
  ├── id (PK)
  ├── title
  └── point_value

completed_tasks
  ├── child_id (FK → children)
  └── task_id (FK → tasks)

goals
  ├── id (PK)
  ├── parent_id (FK → parents)
  ├── child_id (FK → children)
  ├── title, reward
  ├── required_points
  ├── required_task_id (nullable FK → tasks)
  └── completed, completed_at

courses
  ├── id (PK)
  ├── parent_id (FK → parents)
  ├── title, acronym, language, difficulty
  ├── summary, description
  ├── point_reward
  ├── is_published
  └── created_at, updated_at

course_quiz_questions
  ├── id (PK)
  ├── course_id (FK → courses)
  ├── order_index
  ├── prompt
  ├── option_a/b/c/d
  ├── correct_index (0–3)
  └── explanation

child_course_progress
  ├── id (PK)
  ├── child_id (FK → children)
  ├── course_id (FK → courses)
  ├── attempts
  ├── best_score
  ├── completed, completed_at
  └── reward_granted
```

---

## API Reference

### HTTP REST (Web Creator)

Base URL: `https://neuro.serenityutils.club` (configurable via `VITE_API_BASE` env var)

**Authentication**

| Method | Endpoint | Body | Response |
|--------|----------|------|----------|
| POST | `/api/web/auth/lookup` | `{ email }` | `{ exists: bool }` |
| POST | `/api/web/auth/register` | `{ email, password }` | `{ token, parentId }` |
| POST | `/api/web/auth/login` | `{ email, password }` | `{ token, parentId }` |

**Courses** *(require `Authorization: Bearer <token>`)*

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/web/courses/mine` | List all courses owned by the authenticated parent |
| GET | `/api/web/courses/{courseId}` | Get full course detail including questions |
| POST | `/api/web/courses` | Create a new course |
| PUT | `/api/web/courses/{courseId}` | Update a course (replaces questions list) |
| DELETE | `/api/web/courses/{courseId}` | Delete a course |

**Course upsert body:**

```json
{
  "title": "Python Basics",
  "acronym": "PYBASICS",
  "language": "python",
  "difficulty": "beginner",
  "summary": "Core Python syntax",
  "description": "Learn variables, loops, and functions.",
  "pointReward": 50,
  "isPublished": false,
  "questions": [
    {
      "orderIndex": 0,
      "prompt": "What keyword defines a function in Python?",
      "optionA": "func",
      "optionB": "def",
      "optionC": "define",
      "optionD": "fn",
      "correctIndex": 1,
      "explanation": "'def' is the Python keyword for defining functions."
    }
  ]
}
```

---

### WebSocket Packet Protocol

**Connection:** `wss://<host>:49154`

All frames are binary. The first byte is the packet ID. Clients must send a `HandShakePacket` (ID 1) immediately after connecting. Most subsequent packets require an authenticated session.

See [Core Concepts → Real-Time Binary WebSocket Protocol](#real-time-binary-websocket-protocol) for the packet ID table and dispatch flow.

---

## Technology Stack

| Layer | Technology | Version/Notes |
|-------|-----------|---------------|
| Backend language | Java | 21 |
| Backend framework | Spring Boot | 3.2.4 |
| ORM | Spring Data JPA + Hibernate | `ddl-auto=update` |
| Database | PostgreSQL | — |
| Real-time transport | Java-WebSocket (`org.java-websocket`) | Port 49154 |
| JSON (JSONB) | Hypersistence Utils (`io.hypersistence`) | JSONB column mapping |
| AI inference | Groq API | `llama-3.3-70b-versatile` |
| Game engine | Unity | HDRP, XR Toolkit |
| Mobile | Kotlin + Jetpack Compose | Target SDK 36 |
| Mobile scanning | Google ML Kit (Barcode) | QR pairing |
| Mobile images | Coil | — |
| Web creator | Vite 7 + Vanilla JS | — |
| Code execution | Server-side `PythonExecutor`, `CppExecutor` | Java process spawning |
| Password hashing | SHA-256 (`HashUtility`) | — |
| Session management | In-memory UUID map | 7-day TTL |

---

## Getting Started

### Prerequisites

- **Java 21** and Gradle
- **PostgreSQL** running locally (or update `application.properties`)
- A **Groq API key** placed in `java-server/Java-Server/api-keys.json`
- **Node.js 18+** for the web creator
- **Android Studio** for the Kotlin app
- **Unity 2022+** (HDRP) for the game client

### Backend

```bash
cd java-server/Java-Server
# Configure database in src/main/resources/application.properties
# Add your Groq key to api-keys.json
./gradlew bootRun
```

The server starts HTTP on **:8085** and WebSocket on **:49154**.

### Web Course Creator

```bash
cd web-creator
npm install
npm run dev
# Runs on http://localhost:5173
# Set VITE_API_BASE to point at your backend
```

### Android App

Open `kotlin-app/` in Android Studio and run on a device or emulator. Update the WebSocket server URL in the app's configuration to match your backend host.

### Unity Game

Open the `unity/` folder in Unity Hub. Update the default server URL in `GameClient.cs` from `wss://neuro.serenityutils.club` to your local or deployed backend before running in the editor.

---

> **Security note:** `application.properties` contains database credentials and `api-keys.json` contains the Groq API key. Neither file should be committed to a public repository. Add them to `.gitignore` and use environment variables or secrets management in production.
