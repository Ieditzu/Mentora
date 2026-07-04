# Mentora Improvement Checklist

This file turns the evaluation rubric into concrete work items.

## Highest Priority

- Add a real `README.md` in the project root.
- Add formal testing for core gameplay and learning flows.
- Fix security issues around saved tokens and API keys.
- Prepare the project for public distribution with proper build metadata.
- Refactor the biggest runtime classes into smaller modules.

## 1. Documentation

### Must add

- Create `README.md` with:
  - project overview
  - target audience
  - main features
  - technologies used
  - architecture summary
  - install/build/run instructions
  - usage guide
  - screenshots

- Add a short architecture document:
  - gameplay systems
  - UI systems
  - networking
  - AI/code execution flow
  - voice system
  - data flow between client and server

- Add a testing document:
  - what was tested
  - on what devices
  - known limitations
  - bugs fixed

### Nice to have

- Add a short judge-facing presentation document.
- Add a one-page quick-start guide.

## 2. Testing

### Must add

- Create Unity `EditMode` tests for:
  - code parsing/evaluation helpers
  - quiz answer validation
  - utility methods

- Create Unity `PlayMode` tests for:
  - pause menu opens/closes correctly
  - quiz progression works
  - retry wrong answers works
  - code world run/stop flow works

- Add a manual QA checklist for:
  - Windows keyboard/mouse
  - Android/mobile
  - VR/XR if this is part of the final demo
  - multiplayer host/join
  - microphone permissions
  - AI/chat/code execution fallback behavior

### Specific flows to test

- Login with QR
- Session restore from saved session
- C++ quiz flow
- Python challenge flow
- Code island loop execution and stop button
- Multiplayer connect/disconnect
- Voice mode switching
- Settings persistence

## 3. Security

### Must fix

- Stop storing sensitive values in plain local storage:
  - session token in `session.json`
  - OpenAI key in `PlayerPrefs`

- Move secrets to a safer approach:
  - server-side storage
  - short-lived tokens
  - secure platform storage if available

- Review all network calls and make sure production endpoints use secure transport only.

- Validate and sanitize all user inputs:
  - player name
  - host/IP/port
  - code editor input where needed
  - chat input

### Should improve

- Add limits/timeouts to all remote AI/code execution paths.
- Add better failure handling for bad server responses.
- Review whether any learning event or voice data can leak private information.

## 4. Release Readiness

### Must fix

- Replace placeholder build metadata:
  - company name
  - package identifier
  - version

- Review build settings:
  - confirm final scenes
  - remove anything not meant for release

- Add release assets:
  - app icon
  - splash setup
  - proper product branding

- Write a public distribution checklist:
  - tested build
  - no debug-only features exposed
  - no placeholder text
  - no developer keys required in normal flow

### Should improve

- Add a proper main menu / entry flow if needed.
- Remove dead code and prototype-only paths.

## 5. Code Quality

### Must improve

- Refactor large classes into smaller pieces:
  - `PauseMenuManager.cs`
  - `CodeWorldRuntime.cs`
  - `RobotCompanion.cs`
  - `CommunityIslandMenu.cs`

- Separate responsibilities:
  - UI building
  - state management
  - networking
  - persistence
  - gameplay logic

- Standardize naming and reduce duplicated UI creation code.

### Good targets

- Extract settings persistence into a dedicated service.
- Extract session/auth handling into a dedicated service.
- Extract code execution state into its own controller.
- Extract multiplayer UI from multiplayer transport logic.

## 6. Interface

### Must improve

- Test all important screens at multiple resolutions.
- Verify no overlapping UI remains in:
  - settings
  - code editor
  - multiplayer panels
  - quiz panels

- Review text size and spacing for smaller screens.

### Should improve

- Centralize colors, sizing, and spacing constants.
- Make the visual style more consistent across all menus.
- Add a clearer visual hierarchy for buttons and panels.

## 7. Internationalization

### Must improve

- Find all hardcoded strings and move them into a localization system.
- Make Romanian and English complete across all major screens.
- Check grammar and consistency in both languages.

### Should improve

- Prepare support for adding more languages later.

## 8. Educational Content

### Must improve

- Review all quiz, hint, and coding content for scientific/technical correctness.
- Make sure every challenge gives useful feedback when the answer is wrong.
- Ensure progression is clear and difficulty ramps properly.

### Should improve

- Add more explanation after mistakes, not only correct/incorrect.
- Add content authoring notes so educational content can be updated safely.

## 9. Content Management

### Must improve

- Clarify which content can be updated from inside the app and which cannot.
- If content is server-driven, document the update flow.
- If content is local-only, consider adding an admin/content pipeline.

## 10. Originality and Presentation

### Must improve

- Prepare a short demo script that highlights:
  - what makes Mentora different
  - interactive learning
  - AI assistant
  - coding gameplay
  - multiplayer or voice features

- Prepare 3-5 screenshots or short clips for presentation.

## Suggested Work Order

1. Write `README.md` and architecture/testing docs.
2. Fix security around token/API key storage.
3. Add tests for core gameplay and learning flows.
4. Clean up build settings and release metadata.
5. Refactor the largest classes.
6. Finish UI polish and localization pass.
7. Do a final manual test pass on all target devices.

## Minimal Version That Would Raise The Score Fast

- Add `README.md`
- Add testing checklist
- Add 3-5 real tests
- Fix token/API key storage approach
- Clean `ProjectSettings` branding and build metadata
- Verify all menu layouts at target resolutions

