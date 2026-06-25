# Multiplayer Notes

## What Was Done

### Pause Menu UI
- Added a `Multiplayer` button to `PauseMenuManager`.
- Added a dedicated multiplayer screen with:
  - player name input
  - host IP input
  - port input
  - `Host Game` button
  - `Join Game` button
  - live status text
- Kept the scene unchanged, per request.

### Networking Layer
- Added a new runtime manager: `MultiplayerSessionManager`.
- Implemented a basic host/join flow using TCP on a local port.
- Host flow:
  - starts a TCP listener on the chosen port
  - connects the host player locally
- Join flow:
  - connects to an IP address and port
- Added packet types for multiplayer session setup and player state sync.

### Player Representation
- Used the bean character as the base player representation.
- Added floating name labels above local and remote players.
- Added simple remote avatar spawning for connected peers.

## What Still Needs To Be Done

### Core Multiplayer
- Replace the current first-pass sync with a more robust player replication model.
- Add interpolation / smoothing for remote player movement.
- Decide whether the host should also be the authoritative game state owner.
- Add better disconnect handling and client cleanup.

### Lobby / Session Flow
- Add a proper lobby state before spawning into the world.
- Show connected players, ready state, and session status.
- Add a way to stop hosting and return to the menu cleanly.
- Add a way to leave a joined session cleanly.

### Gameplay Sync
- Sync more than just position and rotation:
  - jumping
  - crouching, if applicable
  - animations / emotes later
  - respawn state
- Decide how pickups, interactions, and world events are synchronized.

### Connection UX
- Validate IP and port input before attempting to connect.
- Show clearer success/failure feedback for host and join actions.
- Add a copyable host address or local IP helper for the host machine.

### Visual Polish
- Replace the placeholder multiplayer panel with proper layout and styling.
- Replace the basic remote avatar with the actual bean visual when ready.
- Add name label styling so it reads well in-game.

### Stability / Testing
- Test host on one machine and join from another machine on the same network.
- Test host and join from the same machine.
- Verify behavior when a player disconnects unexpectedly.
- Check for packet loss / desync under worse network conditions.

## Important Constraint

- The existing scene was not modified.

## Suggested Next Step

1. Add a proper lobby screen and disconnect/leave flow.
2. Upgrade sync to smooth remote bean movement.
3. Replace the placeholder remote avatar with the actual bean model.
