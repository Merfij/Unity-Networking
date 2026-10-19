# Network Tank Arena

A two-player authoritative movement and combat starter for the Network Programming course.
Thursday's lab explores network conditions; Friday adds discrete server-authoritative combat.

Use **Unity 6.3.26f1 (6000.3.26f1)**. In Unity Hub, choose **Add project from disk**
and select this repository folder, then open `Assets/Scenes/SampleScene.unity`.
The project includes Netcode for GameObjects 2.13.3, Unity Transport 2.7.4,
Multiplayer Tools 2.2.12 and Multiplayer Play Mode 2.0.2.

## Current baseline

Exactly two players connect through localhost Host/Client sessions. Ownership
binds each process's input and camera to its own tank. Owners send intent; the
server simulates both tanks and writes their positions, chassis rotation and
canonical turret aim. Clients display replicated movement with NetworkTransform
interpolation enabled by default. Turret yaw is separately replicated and applied
directly, without interpolation.

There is no client movement prediction or reconciliation. Left click submits one
reliable fire request; only the server fires, sweeps projectiles, and damages the
persistent test target. No tank health/death/respawn/score is implemented.

**Before starting this contribution in Play Mode, complete the Inspector wiring in
[docs/NETWORK_COMBAT.md](docs/NETWORK_COMBAT.md).** The committed scene/prefabs still
have the movement-lab wiring. Session startup rejects missing combat target setup.

## Start with Multiplayer Play Mode

1. Open `Assets/Scenes/SampleScene.unity`.
2. Open **Window > Play Mode > Scenarios** (or the Play Mode dropdown's
   **Configure play mode scenarios**). Create/select a scenario with the **Main
   Editor** and exactly one **Additional Editor Instance**. Set **Initial Scene**
   to SampleScene. This setup is manual; a scenario and machine-local virtual
   player state are not supplied by the repository.
3. Select that scenario and start Play Mode. Wait for both Game views.
4. In the Main Editor Game view, click **Start Host**.
5. In the additional Editor Game view, click **Start Client (localhost)**.
6. Confirm both connected labels and two tanks with different Owner Client IDs.
   Focus each Game view and release movement keys before controlling it: **W/S**
   moves, **A/D** turns, and the **mouse** aims. Each process must control only
   its owned tank, with its camera following that tank.

The Host's own player has no genuine remote client/server transport round trip.
**Control the Client-owned tank to experience owner-side network delay.** The
Host uses the same authority rules but handles its own input locally.

Use **Stop session** to disconnect; stop Client before Host and wait for shutdown
before starting again.

## Network Simulator setup

Use **Multiplayer Tools Network Simulator**. UnityTransport's obsolete
**DebugSimulator** fields and `SetDebugSimulatorParameters` have no effect in
this installed NGO version.

If the scene/presets have not yet been prepared, perform this one-time setup
outside Play Mode through Unity:

1. Open **Window > Multiplayer > Multiplayer Tools** and choose **Network
   Simulator > Add to Scene**. Keep exactly one Network Simulator component on
   a scene object outside all spawned tank prefabs. Alternatively, use
   **Component > Netcode > Network Simulator** on a scene object.
2. In the Project window, create `Assets/Settings/NetworkConditions`. Use
   **Create > Multiplayer > NetworkSimulatorPresetAsset** to create the five
   presets below. Set their **Name**, delay, jitter and loss values in the
   Inspector. Keep **Packet Loss Interval** at **0** for every preset; it means
   every Nth packet, not a time interval.
3. Set the component's **Connection Preset** to **Custom** and **Custom Preset**
   to Clean localhost. Leave **Scenario** at **None** and **Auto Run Scenario**
   off. Disable the component using its Inspector header checkbox, then save
   the assets and SampleScene. Both processes must start with it disabled.

| Profile | Delay | Jitter | Random loss |
| --- | --- | --- | --- |
| Clean localhost | 0 ms | 0 ms | 0% |
| Moderate latency | 100 ms | 0 ms | 0% |
| Latency plus jitter | 100 ms | 50 ms | 0% |
| Lossy connection | 0 ms | 0 ms | 10% |
| Combined impairment | 100 ms | 50 ms | 5% |

During Play Mode, enable the component **only in the Main Editor Host** and
select a prepared Custom Preset. Leave the virtual Client's simulator disabled;
confirm this in its Inspector. Do not apply the same preset at both endpoints
unless deliberately demonstrating compounded delay. Select presets rather than
editing their values during an exercise. Random loss reproduces a condition,
not an identical packet trace.

With Host-only impairment, Client input passes through the impaired receive
path and authoritative state returns through the impaired send path. A delay of
D in each direction adds approximately **2D network RTT**: 100 ms configured
delay adds roughly 200 ms RTT for the remote Client. Input scheduling, server
simulation, replication and interpolation add further visible delay.

## Reset to clean localhost

1. Restore the simulator **Connection** to **Connected**.
2. Pause/clear active scenarios, leave Auto Run Scenario off, and let any active
   lag spike finish.
3. Select Clean localhost (or None / zero all impairment).
4. Disable the simulator component.
5. Allow queued traffic to drain before comparing clean behavior.
6. For a definitive clean restart, stop Client, then Host, wait for shutdown,
   and restart both sessions with the simulator disabled.

Disabling/re-enabling can retain the previously selected preset. Editing preset
assets in Play Mode may persist changes after Play Mode ends. Check the profile
and saved disabled/clean defaults before the next exercise.

## Lab observations

- **Server authority:** the server decides gameplay state.
- **Interpolation:** clients smooth received states, trading some immediacy for
  smoothness; it does not give them authority.
- **Client prediction:** the owner simulates input before server confirmation.
- **Reconciliation:** predicted state is corrected against authoritative results.
- **Presentation smoothing:** visual motion is eased without changing gameplay
  decisions; hover/bank remains below the stable tank root.

This starter implements authority, replication and interpolation. Prediction and
reconciliation are deliberately deferred.

1. Observe the clean baseline from both processes.
2. Apply Moderate latency; compare Client responsiveness with Host responsiveness.
3. Apply Latency plus jitter; look for variation in movement arrival/smoothness.
4. Apply Lossy connection, then Combined impairment. Isolated lost movement updates
   may be subtle: newer intent replaces older intent, the server temporarily
   retains accepted intent, and transform interpolation can hide some loss.
5. Under the same impairment, compare interpolation enabled and disabled. In the
   Main Editor during Play Mode, select a **spawned tank's server copy** and toggle
   its NetworkTransform **Interpolate** setting. Observe that tank in the Client
   and confirm the replicated setting there. Repeat the same movement with all
   other settings unchanged. Keep server authority and the component enabled.
6. Restore Interpolate on, reset impairment, then exit/re-enter Play Mode to restore
   runtime-only edits. Keep prefab interpolation enabled and the saved simulator
   disabled/clean.

The prefab uses NGO's LegacyLerp interpolation with extra smoothing. Compare
client-rendered movement; the Host's authoritative copies do not demonstrate the
same interpolation delay. Optional ownership labels are available through
Multiplayer Tools' **Hierarchy Network Debug View**.

## Known limits

- Capacity is two connected players; a third is rejected. Disconnecting frees a
  spawn slot for a new Client.
- Transport is direct localhost (`127.0.0.1:7777`); there is no Relay or
  dedicated-server startup.
- NGO scene management is disabled: both instances load SampleScene themselves.
- Combat requires the one-time wiring in [NETWORK_COMBAT.md](docs/NETWORK_COMBAT.md).
- Projectile interpolation can lag the authoritative collision; very short-lived
  projectiles may disappear before a remote frame renders them. Prediction,
  reconciliation, tank health, death, respawn, and score remain deferred.
- Movement intent becomes neutral after **0.5 seconds without an accepted update
  at the server**. This measures server receipt silence, not a command's original
  age. A delayed newer command can still be accepted. After focus loss, movement
  should eventually stop; release movement keys before resuming control.
