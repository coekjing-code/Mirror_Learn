# Network Learning Roadmap

## Current Flow

1. Client reads local input.
2. Client calls a Mirror `Command`, such as `CmdFire`.
3. Server validates the request.
4. Server changes authoritative state or spawns network objects.
5. Clients receive synchronized state through Mirror `SyncVar`, spawn messages, or RPC/messages.

## Sync Modes

- `StateSync`: clients send state such as position, rotation, animation state. Easy to learn, weak anti-cheat.
- `InputSync`: clients send input, server simulates movement. Better server authority.
- `SnapshotSync`: server sends periodic world snapshots. Common for action games.
- `LockstepFrameSync`: all clients execute the same deterministic frames. Common for RTS, hard in Unity physics.

This project currently defaults to `InputSync`.

## InputSync In This Project

The local player still moves immediately for client-side prediction.

Every sync tick, the client sends:

- world move direction
- run/crouch/jump intent
- current rotation
- current motion state

The server then:

- clamps the input delta time
- picks an allowed speed from the motion state
- simulates movement on the server object
- writes authoritative position, rotation, and motion state into SyncVars

Other clients smooth toward the authoritative server state.

The owner can reconcile if the prediction drifts too far from the server.

## Latency Simulation

The debug HUD can control Mirror's `LatencySimulation` transport if it exists in the scene.

To test real transport delay:

1. Add a `LatencySimulation` component to the NetworkManager object.
2. Assign the current KCP transport to `LatencySimulation.wrap`.
3. Set NetworkManager's transport field to the `LatencySimulation` component.
4. Play the game and press `F4` to toggle simulated latency.

When latency is enabled, watch:

- RTT
- local position
- server position
- prediction error

Higher latency should make prediction error easier to see.

## Reconnect

Reconnect needs a session identity, not just a transport connection.

Recommended next steps:

1. Client sends account/session token when connecting.
2. Server maps token to player data.
3. Server keeps player state for a short timeout after disconnect.
4. Client reconnects and asks server to rebind to the saved player.

## Lag Compensation

For shooting:

1. Client sends fire time and aim ray.
2. Server keeps recent hitbox history.
3. Server rewinds targets to the reported fire time.
4. Server checks hit and applies damage.

## Current Combat Loop

The project now has a minimal arena combat loop:

1. Client clicks fire.
2. Client sends `clientFireTime`, `rayOrigin`, and `rayDirection`.
3. Server checks fire cooldown.
4. Server rewinds target history through `LagCompensationTarget`.
5. Server applies damage through `PlayerController.ServerTakeDamage`.
6. Player health is synchronized by SyncVars.
7. On death, server disables gameplay, records the kill in `MatchManager`, waits, then respawns the player.

This is the basic server-authoritative shape for a multiplayer TPS.

## Match Framework

`MatchManager` is intentionally small:

- finds spawn points
- chooses respawn locations
- tracks kill counts on the server

Recommended next additions:

- match states: Waiting, Countdown, Playing, Finished
- round timer
- score limit
- team assignment or free-for-all mode
- scoreboard SyncList

## Redis

Do not connect Unity clients directly to Redis.

Use Redis behind a dedicated game/backend server for:

- account/session records
- inventory/profile snapshots
- matchmaking queues
- short-lived reconnect state
- leaderboard/cache data
