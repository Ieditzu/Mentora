# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Mentora is an educational platform for children learning programming (Python/C++) through a Unity 3D game, monitored by parents via an Android app. The backend serves both a binary WebSocket protocol (game + mobile) and a REST API (web course creator).

## Components

- `java-server/` — Spring Boot 3.2.4 backend (Java 21, PostgreSQL)
- `unity/` — HDRP Unity game client (Unity 2022.3.62f3, C#)
- `kotlin-app/` — Android parent dashboard (Kotlin + Jetpack Compose)
- `web-creator/` — Course authoring tool (Vite 7 + React 19 + Tailwind CSS v4 + Framer Motion)

## Commands

### Backend
```bash
cd java-server/Java-Server
./gradlew bootRun    # dev server; HTTP :8085, WebSocket :49154
./gradlew build      # produces JAR in build/libs/
```
Requires `api-keys.json` (Groq key) and `src/main/resources/application.properties` (DB credentials) — neither is in the repo.

### Web Creator
```bash
cd web-creator
npm install
npm run dev          # http://localhost:5173
npm run build        # outputs to dist/
```
Set `VITE_API_BASE` env var to point at the backend; otherwise defaults to the production URL.

### Android App
Open `kotlin-app/` in Android Studio. Target SDK 36, Min SDK 24.

### Unity Game
Open `unity/` in Unity Hub (2022.3.62f3). Update the default server URL in `GameClient.cs` from `wss://neuro.serenityutils.club` to your backend for local dev.

## Architecture

```
Web Creator (React)  Android App (Kotlin)   Unity Game (C#)
       |                     |                     |
   HTTP REST              Binary WS             Binary WS
       |                     |                     |
       +----------  Java/Spring Boot  -------------+
                      :8085 / :49154
                    PostgreSQL + GroqAI
```

**Binary WebSocket (port 49154):** Used by Unity and Android. All frames are AES-256-CBC encrypted. The frame format is `[4-byte seed length][encrypted seed][encrypted payload]`. The seed is derived from `System.nanoTime()` and encrypted with a shared base key. The server's entry point for this traffic is `ClientHandler.java`.

**REST API (port 8085):** Used only by the web course creator. Controllers are `WebAuthController` and `WebCourseController`.

## Key Files

| File | Role |
|------|------|
| `java-server/.../client/ClientHandler.java` | Central packet dispatcher (~44 packet types in a switch expression) |
| `java-server/.../Server.java` | Singleton: manages WebSocket lifecycle, services, and QR login state |
| `java-server/.../packet/PacketManager.java` | Factory: deserializes binary frames by packet ID |
| `java-server/.../packet/Packet.java` | Base class: handles encryption/decryption |
| `java-server/.../database/services/LearningProfileService.java` | Maintains AI learning profiles stored as JSONB |
| `java-server/.../utility/GroqAI.java` | Groq API wrapper (LLaMA-3.3-70B, 200-entry LRU cache, 5-min TTL) |
| `java-server/.../python/PythonExecutor.java` | Sandboxed Python runner (`unshare --net`, ulimit) |
| `java-server/.../cpp/CppExecutor.java` | Sandboxed C++ compiler+runner (same sandbox) |
| `unity/.../Network/GameClient.cs` | Unity WebSocket singleton, packet dispatch, `OnPacketReceived` event |
| `unity/.../PauseMenuManager.cs` | Game-side UI hub: session restore, QR flow, task/goal fetch |
| `unity/.../PythonDebugPadCinematic.cs` | Python challenge UI: execute → AI eval → learning event |
| `unity/.../CodeChallengePadCinematic.cs` | C++ code-debugging pads (Medium/Hard modes); student edits and runs C++ code, bilingual Romanian/English |
| `unity/.../CppQuestionPadCinematic.cs` | C++ multiple-choice quiz pad (5 bilingual MCQ questions, "C++ Starter Quiz" task) |
| `kotlin-app/.../ui/SocketViewModel.kt` | Android state container: socket, login, children, goals, AI profiles |
| `web-creator/src/App.jsx` | Full SPA: auth, course CRUD, question editor |

## Non-Obvious Patterns

**Protocol is not shared code.** There is no code generation or shared protocol library. Adding or changing a packet type requires edits in three places: Java server, Kotlin client, and Unity client.

**Packet auth whitelist.** Packet IDs 1, 2, 3, 11, 13, 15, 19, 25, 32, 41, 43, 44 are allowed before authentication. All others return an auth error. This whitelist is hardcoded in `ClientHandler.java` (there is a comment acknowledging it should not be hardcoded).

**AI evaluation is excluded from learning counters.** When Unity sends `AskAiPacket` with `context="eval"` (during task grading), the server's `recordAiInteraction` skips updating hint/chat counters. This ensures the learning profile reflects student effort, not AI-assisted grading events.

**Task auto-completion matches by title string.** When a code challenge is solved in Unity, the game sends `CompleteTaskPacket` with a task title string matched against `DefaultTaskType` enum values. Title changes break the match.

**Course completion is all-or-nothing.** A student must score 100% to earn points. Any wrong answer voids the reward. The server grants points only on perfect score.

**`FetchChildStatsPacket` updates streak; `FetchChildStatsByParentPacket` does not.** The distinction is intentional to prevent parents from inflating streaks.

**Learning profiles in JSONB.** The `game_stats` column on the Child entity stores three sub-profiles (C++, Python, General) as free-form JSON. Each profile holds counters, a topic accuracy map, a rolling 10-event window, and LLM-generated summaries throttled to once per 5 minutes.

**Sandbox constraints for code execution:**
- 256 MB memory (`ulimit -v`)
- 2 MB max file size (`ulimit -f`)
- 64 max processes (`ulimit -u`, prevents fork bombs)
- 120s timeout + 2s grace period
- Network isolated via `unshare --net`

**Packets 41/42 are `FetchAllChildren` (admin/dev use).** IDs 41 (`FetchAllChildrenPacket`) and 42 (`FetchAllChildrenResponsePacket`) fetch children across all parents, not just the authenticated one. They are in the auth whitelist alongside the dev packets (43, 44).

**No tests.** There are no unit, integration, or end-to-end tests in any component. All testing is manual.
