# Repository Guidelines

## Project Structure & Module Organization
This repository contains four active parts:

- `java-server/Java-Server`: Spring Boot backend, database services, WebSocket protocol, and web creator APIs.
- `kotlin-app`: Android parent app built with Jetpack Compose.
- `unity`: Unity 2022 child-facing game, scenes, runtime scripts, and editor tools.
- `web-creator`: Vite-based local frontend for creating and publishing quiz courses.

Key locations:

- Server source: `java-server/Java-Server/src/main/java`
- Android source: `kotlin-app/app/src/main/java`
- Unity runtime scripts: `unity/Assets/Scripts/Runtime`
- Unity scene assets: `unity/Assets/Scenes`
- Web creator source: `web-creator/src`

## Build, Test, and Development Commands
- Server run: `./gradlew bootRun` from `java-server/Java-Server`
- Server build: `./gradlew build`
- Android debug build: `./gradlew assembleDebug` from `kotlin-app`
- Web creator dev server: `npm run dev` from `web-creator`
- Web creator production build: `npm run build`
- Unity: open `unity` in Unity Editor `2022.3.62f3` and run `Assets/Scenes/SampleScene.unity`

## Coding Style & Naming Conventions
- Java/Kotlin: 4-space indentation, PascalCase for classes, camelCase for methods/fields.
- Unity C#: 4-space indentation, PascalCase for types and public members, camelCase for private fields.
- Web creator JS/CSS: keep modules small, prefer clear DOM ids/classes, and avoid unused helpers.
- Follow existing naming patterns such as `*Manager`, `*Controller`, `*Packet`, `*ResponsePacket`, and `*Cinematic`.

## Testing Guidelines
Automated coverage is limited today.

- Server: add focused service/controller tests under `src/test/java` when changing backend behavior.
- Android: use `app/src/test` for unit tests and `app/src/androidTest` for instrumented tests.
- Unity: validate gameplay changes in Play Mode and note scene/object paths touched.
- Web creator: at minimum run `npm run build` before submitting UI changes.

## Commit & Pull Request Guidelines
- Prefer short, imperative commit messages: `Add published courses fetch`, `Fix community menu theme toggle`.
- Keep commits scoped to one subsystem when possible.
- PRs should include:
  - a clear summary
  - touched modules (`unity`, `java-server`, `web-creator`, `kotlin-app`)
  - setup or migration notes
  - screenshots/video for UI changes
  - linked issue or task when available

## Security & Configuration Tips
- Do not commit secrets, tokens, or local DB credentials.
- Review `.env`, `application.properties`, and Unity persistent-session behavior before shipping.
- Packet/protocol changes usually require coordinated updates across server, Android, and Unity.
