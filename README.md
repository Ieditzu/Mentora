# Mentora

**Mentora** is an AI-powered educational platform that teaches children aged 9–13 programming through hands-on experience rather than passive lessons. Students write and execute real Python and C++ code inside a 3D Unity game world, receiving instant AI-graded feedback on their output. Every mistake a student makes is remembered — the backend builds a persistent per-student knowledge profile tracking strengths, weaknesses, hint usage, and chat history across topics and languages. An in-game AI tutor (powered by Meta's LLaMA 3.3 70B via Groq) uses that profile before every single response, so hints and explanations are always calibrated to exactly what that student knows and where they are struggling.

Beyond the game, Mentora is an ecosystem: a **companion Android app** lets parents monitor their child's progress in real time, view AI-generated summaries of what their child is good at in Python and C++ respectively, see completed tasks, and set custom goals to keep the child motivated. A **community web platform** allows parents and educators to author and publish their own quiz-based courses, which students can discover and play directly inside the game world.

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
- Maintains per-child AI learning profiles as **JSONB** columns, updated after every code run, hint request, and AI chat turn
- **Executes student-submitted Python and C++ code server-side** via `PythonExecutor` and `CppExecutor` inside sandboxed Linux environments — network isolation via `unshare`, strict `ulimit` constraints (256 MB memory cap, CPU time limit, 64 max processes to prevent fork bombs, 2 MB file size limit)
- **AI-verifies code output** — there are no hardcoded expected answers; the AI reads the task description, the student's code, and the actual program output to judge correctness, so creative solutions that produce the right result are accepted
- Calls the **Groq AI API** (LLaMA-3.3-70B) for adaptive tutoring responses, hint generation, and parent-facing progress summaries
- Response caching with a configurable TTL and per-model rate-limit cooldown timers prevent redundant API calls

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

A 3D game environment built in **Unity** with the **High Definition Render Pipeline (HDRP)**. Students explore an interactive island world containing dedicated coding pads for Python and C++, a community island with published courses, and an AI mentor they can chat with at any point during a challenge.

**What students do in the game:**
- Work through **22 progressive tasks** across three tiers (see [Task & Goal System](#task--goal-system) for the full list)
- Write and run real Python and C++ code directly in-game — output comes back from the server live; the AI then evaluates whether the result is correct (no hardcoded expected outputs)
- Solve **interactive logic puzzles** where they manipulate in-game variables (e.g. `jumpVelocity`, `islandVisible`, `boxRigidbody`) through a real-time variable editor to unlock new areas — requires lateral thinking, not rote answers
- Take a **5-question C++ multiple choice quiz** (bilingual Romanian/English) with a portal cinematic entry sequence
- Ask the AI mentor for hints at any challenge via a chat panel — the AI knows the current task, the student's code, and their full learning profile
- Track daily login streaks and a real-time progress bar showing overall task completion
- Set a custom server URL via the pause menu (for dev/local testing) without rebuilding

**Python code verification flow:**

Python challenges use a two-step AI evaluation that is intentionally separate from the learning profile's hint/chat counters:

1. Student submits code → `ExecutePythonCodePacket` → server runs it → stdout back to client
2. Client sends `AskAiPacket` with context `"python_eval"` — Groq reads the task, the code, and the output to return `CORRECT` or `INCORRECT`
3. Server-side `recordAiInteraction` skips the `"eval"` context, so AI evaluation calls do **not** inflate the student's hint or chat turn counts in their learning profile
4. A fallback `IsChallengeCorrect` heuristic (string comparison + `CompactCode` normalisation per `ValidationId`) handles cases where Groq is unavailable

**Key scripts:**

| Script | Purpose |
|--------|---------|
| `GameClient.cs` | Singleton WebSocket client; connects to `wss://neuro.serenityutils.club` by default, handles all binary packet send/receive |
| `PauseMenuManager.cs` | Central in-game UI hub — QR code generation and display, `session.json` auto-login on startup, `VerifySessionPacket` resume, task/goal/children lists, `CompleteTaskByTitle`, streak display, dev unlock via code `dvlp`, server URL override |
| `PythonDebugPadCinematic.cs` | Python coding pads (medium + hard modes); runs code, AI-evaluates output, records learning events, provides AI hint chat |
| `CppQuestionPadCinematic.cs` | 5-question bilingual C++ MCQ; records per-answer learning events, AI hint chat, awards task on perfect score |
| `CommunityIslandMenu.cs` | Loads and displays published community courses via `FetchPublishedCoursesPacket` |
| `Network/EncryptionUtility.cs` | Client-side AES-256-CBC encryption matching the server's per-packet dynamic seed scheme |

The Unity client exclusively uses the binary WebSocket protocol — it never calls the HTTP REST API.

---

### Kotlin Android App

**Location:** `kotlin-app/`

A **Jetpack Compose** Android application (target SDK 36) used by **parents** to monitor their child's learning progress and manage goals. It mirrors the same binary WebSocket protocol as the Unity client.

**Screens:**

| Screen | What it shows |
|--------|--------------|
| **Home** | List of parent's children with total points, **live online dot** (whether the child is currently connected to the game server), QR link action, and tap-through to goals |
| **History** | Completed tasks grouped by date with daily totals and task point values |
| **Goals / AI Insights** | Per-child goals (locked/completed state) plus expandable **AI Insights cards** for C++, Python, and General — each card shows the one-line AI summary; tapping opens a `ProfileDetailDialog` with the full three-line breakdown and raw stats |
| **Settings** | Parent profile picture, dark mode toggle, colour theme selection, add/remove children, **dev "Force Game Login"** (manually enter child id + session token for testing), logout |

**Key features:**
- **Per-language AI summaries** — AI-generated one-line → three-line → full stats, refreshed automatically as the child's profile evolves (throttled to once per 5 minutes)
- **Live online presence** — the home screen shows which children are currently active in the game in real time
- **Custom goal setting** — parents define point-threshold or task-completion goals with a reward message; the server pushes a live completion event to the app the moment the child hits the target
- **System notifications** (Android Tiramisu+ permission requested at launch) — the app is notified in real time when a child completes a task
- **Task completion history** grouped by date with daily stats
- **QR code scanning** (via **ML Kit** barcode + **CameraX**) to link a child's game account without the child typing credentials
- **Profile pictures** for both parents and children, stored as Base64 on the server
- Dark mode and colour theme customisation
- Auth state and server token persisted across app restarts

---

### Web Course Creator

**Location:** `web-creator/`

A lightweight **Vite**-powered single-page application built in vanilla JavaScript. Parents and educators use it to author custom course content that gets published into the game.

**Key features:**
- Parent registration, login, and session management
- Full CRUD for courses: title, language (Python/C++), difficulty, description, quiz questions
- Each question supports four multiple-choice options with a designated correct answer and optional explanation
- Publishing/unpublishing courses so they appear in-game on the Community Island
- Published courses integrate directly with the AI learning profile — completion attempts are recorded as learning events under the topic `{language}_course:{acronym}`, feeding back into the student's adaptive profile

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

**Skill level classification** — `buildProfileSummary` classifies each student into a level based on their accuracy and interaction count:
- **Beginner** — fewer than 10 total interactions or accuracy below 40%
- **Intermediate** — accuracy 40–70%
- **Advanced** — accuracy above 70%

This level label is included in every AI prompt context so the tutor never pitches explanations too high or too low.

**LLM-generated narrative summaries** (`summaryText`, `summaryOneLine`, `summaryThreeLine`) are regenerated by calling Groq when stats change, but are throttled — no more than once every 300 seconds — to avoid excessive API usage. The summary prompt instructs Groq to produce both `ONE:` (single line) and `THREE:` (three-line) responses in one call, which are split and stored separately for the different UI detail levels.

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

**AI-verified code submissions** — when a student submits code, the server doesn't check against a hardcoded expected output. Instead, the AI receives the task description, the student's full code, and the actual program output, then responds with `CORRECT` or `INCORRECT` and a short explanation. This means creative solutions that produce the right result through a different approach are accepted.

**Rate limiting and caching** are handled inside `GroqAI.java` with a configurable LRU cache (200 entries, 5-minute TTL) and independent cooldown timers per model after rate limits are hit.

---

### Real-Time Binary WebSocket Protocol

All game and mobile communication uses a **custom binary packet protocol** over WebSocket (port 49154). This avoids JSON serialisation overhead and allows tight control over the message format.

**Encryption**

Every packet is encrypted with **AES-256-CBC** using a dynamic per-packet seed system. Each message generates a unique seed from `System.nanoTime()`, encrypts that seed with a shared base key, then encrypts the actual payload using the seed as the encryption key. This means every single packet uses a different encryption key — replay attacks and traffic analysis are impractical. The seed length is validated server-side to prevent out-of-memory attacks.

**Packet lifecycle:**

1. Client sends an encrypted binary frame; after decryption the first byte is the **packet ID**
2. `ClientHandler.onMessage` reads the ID, delegates to `PacketManager.createPacket(id)` to deserialise the payload
3. A large `switch` expression dispatches on the concrete packet type
4. The handler checks authorisation (most packets require an authenticated session), processes business logic, then optionally writes a response packet back to the same client or broadcasts to related clients (e.g. goal completions are pushed to the connected parent app in real time)

**Complete packet table (IDs 1–44):**

| ID | Packet(s) | Direction | Purpose |
|----|-----------|-----------|---------|
| 1 | `HandShakePacket` | C→S | Connection init; client identifies itself (`unity_game`, `android_client`, etc.) |
| 2 / 10 | `AuthPacket` / `AuthResponsePacket` | C→S / S→C | Parent login with hashed email + password |
| 3 | `RegisterParentPacket` | C→S | New parent registration; auto-authenticates on success |
| 4 | `AddChildPacket` | C→S | Parent adds a child by name |
| 5 | `AddGoalPacket` | C→S | Create a task-linked or points-threshold goal; optionally pushed live to connected child |
| 8 / 9 | `CompleteTaskPacket` / `ActionResponsePacket` | C→S / S→C | Mark task complete; notifies connected parent |
| 11 / 12 | `FetchTasksPacket` / `FetchTasksResponsePacket` | C→S / S→C | Full global task catalog |
| 13 / 14 | `FetchGoalsPacket` / `FetchGoalsResponsePacket` | C→S / S→C | Goals for a child (self or parent-fetched) |
| 15 / 16 | `FetchChildrenPacket` / `FetchChildrenResponsePacket` | C→S / S→C | Parent's children + **live online flag** per child |
| 17 / 18 | `FetchCompletedTasksPacket` / `FetchCompletedTasksResponsePacket` | C→S / S→C | Parent-only: completed tasks for a child with timestamps |
| 19 / 20 | `GenerateQRLoginPacket` / `QRLoginResponsePacket` | C→S / S→C | Game generates a short-lived QR token |
| 21 | `ClaimQRLoginPacket` | C→S | Parent app claims token for a specific child; triggers child auth |
| 22 | `ChildAuthResponsePacket` | S→C | Auth success: child id, name, session token |
| 23 / 24 | `FetchChildStatsPacket` / `FetchChildStatsResponsePacket` | C→S / S→C | Child fetches own stats; triggers streak update + AI summary refresh |
| 25 | `VerifySessionPacket` | C→S | Resume child session from saved `childId` + token (loaded from `session.json`) |
| 26 | `UpdatePfpPacket` | C→S | Update parent or child profile picture (Base64) |
| 27 | `RemoveChildPacket` | C→S | Parent deletes a child profile |
| 28 / 29 | `ExecuteCPPCodePacket` / `ExecuteCPPCodeResponsePacket` | C→S / S→C | Run C++ code server-side (120s timeout); returns stdout/stderr |
| 30 / 31 | `AskAiPacket` / `AiResponsePacket` | C→S / S→C | AI mentor Q&A; injects learning profile for child sessions |
| 32 | `FetchChildStatsByParentPacket` | C→S | Parent fetches a child's stats (streak **not** updated) |
| 33 | `RecordLearningEventPacket` | C→S | Record a learning event against child profile (fire-and-forget, no ACK) |
| 34 / 35 | `ExecutePythonCodePacket` / `ExecutePythonCodeResponsePacket` | C→S / S→C | Run Python code server-side (120s timeout) |
| 36 / 37 | `FetchPublishedCoursesPacket` / `FetchPublishedCoursesResponsePacket` | C→S / S→C | Published course catalog with per-child completion flags |
| 38 / 39 | `FetchCourseDetailPacket` / `FetchCourseDetailResponsePacket` | C→S / S→C | Full course questions + child completion state |
| 40 | `SubmitCourseCompletionPacket` | C→S | Submit quiz result; awards points on perfect score |
| 41 / 42 | `FetchAllChildrenPacket` / `FetchAllChildrenResponsePacket` | C→S / S→C | All children in DB + online flags (dev use) |
| 43 | `DevLoginAsChildPacket` | C→S | Log in as any child by id; creates session |
| 44 | `DevCreateChildProfilePacket` | C→S | Create a child under the synthetic dev parent account |

Packets **1, 2, 3, 19, 25, 41, 43, 44** are whitelisted and processed **without** prior authentication. All others return an `ActionResponsePacket(currentId, false, "Unauthorized")` if the client has no valid session.

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

- A `Course` has metadata (title, acronym, language, difficulty, summary ≤280 chars, description, point reward) and an ordered list of `CourseQuizQuestion` entries
- Each question has a prompt, four answer options (A–D), a `correctIndex` (0–3), and an optional explanation
- Acronyms are sanitised server-side (uppercased, non-alphanumeric stripped) for use as learning profile topic keys
- Courses require at least 1 question; all options and a valid `correctIndex` are validated before saving
- Courses are only visible in-game after a parent explicitly **publishes** them
- The web creator persists the session token in `localStorage` so parents stay logged in across browser refreshes

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

**Tasks** are pre-defined programming challenges seeded from `DefaultTaskType` into the `tasks` table on first server start. Any child can complete a task once for its point value. Completing a task also increments `game_stats["tasks_completed"]` and triggers automatic goal-completion checking.

**The 22 built-in tasks:**

| # | Title | Points | Category |
|---|-------|--------|---------|
| 1 | C++ Starter Quiz | 25 | C++ |
| 2–5 | C++ Debug: Multiply, Sum, Even, Increment | 15–20 | C++ Medium |
| 6–10 | C++ Hard: IsEven, MaxOfTwo, Square, Sum3, Factorial | 30–35 | C++ Hard |
| 11–14 | Python Debug: Multiply, Sum, Even, Loop | 15–20 | Python Medium |
| 15–19 | Python Visual: Bar Line, Progress Bar, Square Grid, Stairs, Alternating | 30–35 | Python Hard |
| 20 | Logic: Jump & Box (adjust jump velocity + enable physics) | 20 | Logic |
| 21 | Logic: Reveal Island (set island visible flag) | 20 | Logic |
| 22 | Logic: Reveal Bridge (unlock bridge path) | 20 | Logic |

**Goals** are created by parents and tied to a child. A goal specifies:
- A title and description
- A reward message shown to the child on completion
- Either a **point threshold** (`required_points`) or a **specific task** (`required_task_id`) as the completion condition

The server checks goal completion automatically when a task is completed or points are updated. When a goal is met, a live push packet is sent to both the connected game client (so the child sees the reward in-game immediately) and the parent's Android app.

---

## Database Schema

Schema is managed automatically by **Hibernate DDL auto-update**. The logical schema is:

```
parents
  ├── id (PK)
  ├── email (unique)
  ├── password_hash
  └── profile_picture (Base64)

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
