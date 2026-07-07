# Mentora — Ideas for Nationals

Ideas brainstormed for InfoEducație Națională. Tick off as implemented.

---

## Game (Unity 3D)

### [x] AI generates new tasks based on your profile
Instead of 22 static tasks forever, the server reads the child's weak areas from their learning profile and generates a brand new coding challenge via LLaMA targeting exactly where they're struggling. No two students get the same generated challenge. Implemented via `GenerateAiTaskPacket` (45) / `GenerateAiTaskResponsePacket` (46) + `AiChallengePad.cs`.

### [x] Robot companion that follows you and remembers everything
An NPC robot that follows the player around, reads their full learning profile, and makes contextual comments — "last time you tried this you forgot to initialize your variable." Reacts to challenge success/fail in real time. Implemented via `CompanionSpeakPacket` (47) / `CompanionSpeakResponsePacket` (48) + `RobotCompanion.cs`.

### [ ] Your code literally controls the world
Instead of stdout text, student code has visible 3D consequences. Write a loop → a robot NPC walks that loop in the scene. Write a factorial → a tower of blocks builds floor by floor. Wrong output → the structure breaks visually. Requires Unity event mapping from server output to scene objects.

### [ ] The world is broken — your code fixes it
Make every task diegetic. The bridge is broken because of a bug in its control code — debug it and the bridge physically rebuilds. The island is frozen in an infinite loop — find it and time resumes. Coding becomes world repair, not homework.

### [ ] Expand logic/physics puzzles from 3 to 10+
Current: only 3 (jumpVelocity, islandVisible, bridge). Add physics simulations students can solve by setting variables:
- Adjust rocket thrust + drag to land on a platform
- Set pendulum length + angle to hit a target
- Configure ball friction + mass to navigate a maze
Each is a "virtual experiment" — exactly what judges want to see.

### [ ] Competitive coding islands
Two students race to solve the same challenge simultaneously. Countdown timer, both coding in parallel. Winner's solution posts on an island leaderboard. Even async: race your friend's ghost run time.

---

## Android App

### [x] Live session spectator mode
Parent opens app while child is playing and sees a live feed: which pad they're at, what code they're typing, attempt count, whether they asked for a hint. Same WebSocket infrastructure, more events.

### [x] Weekly AI report card
Every Monday the app generates a natural-language letter from the AI tutor:
> "This week Andrei spent 3 sessions on Python. He consistently miscounts loop ranges — appeared in 5 of 8 mistakes..."
All data already exists. One Groq call with a smart prompt.

### [x] Parent sets tonight's challenge
From the app, parent sends a custom challenge push to the game: "Try the factorial task before dinner." Child sees it in-game as a notification from their parent. Parent gets push on completion.

### [x] Learning heatmap calendar
GitHub contribution-style graph — green/yellow/red days based on session quality. Far more readable than "streak: 6 days."

### [x] Skill radar chart
Hexagonal radar with axes: Loops, Functions, Conditionals, Recursion, Memory, Data Structures. Fills as child demonstrates competency. The visual parents screenshot and share.

---

## Web Creator

### [ ] AI generates quiz questions for you
Educator types topic ("Python list comprehensions, intermediate"). AI drafts 8 questions. Educator reviews and approves. Course creation drops from 40 minutes to 5.

### [ ] Per-question difficulty heatmap
After students take a course: each question color-coded by % wrong, most common wrong answer, average time spent. Educator sees exactly which questions need rewriting.

### [ ] Preview in-game before publishing
Button: "Preview in Unity" — sends packet to a connected game instance and the course appears exactly as students will see it, before publish.

### [ ] Educator analytics dashboard
Per-course stats: total students attempted, average score, completion rate, most common wrong answers. Turns the creator from "quiz editor" into a real content management system.
