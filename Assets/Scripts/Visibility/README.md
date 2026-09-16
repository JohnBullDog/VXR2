# Visibility-Gated Version Switching

Lets an object have several interchangeable "versions" (e.g. different rigs, LODs, or
story states) and swap between them without the player ever seeing the pop — the swap
is deferred until the object leaves both eyes' view frustums.

## How it fits together

```
Timeline (TimeLine.playable)
  -> Signal Emitter fires a Signal Asset (the .signal files)
     -> Signal Receiver on a GameObject calls VersionSwitchGroup.RequestSwitchTo(index)
        -> forwards to every VisibilityGatedVersionSwitcher in the group
           -> each one swaps the moment it's outside EyeVisibilityCones
```

`EyeVisibilityCones` is the only thing that needs to exist once per scene. Everything
else is per-object.

## Components

### `EyeVisibilityCones`
Put this on (or near) the XR camera rig. It builds one visibility cone per eye each
frame from the camera's live stereo data (falls back to the Inspector-configured
separation/FOV values when no headset is attached, e.g. running in the Editor). Other
scripts query it via the static `EyeVisibilityCones.IsSphereVisible(...)` /
`IsBoundsVisible(...)` methods — you don't call anything on it directly.

Inspector fields:
- **Xr Camera** — defaults to `Camera.main` if left empty.
- **Fallback Eye Separation / Half Angle** — used only without stereo XR data.
- **Max Range** — how far the cones extend; anything past this always counts as
  "not visible."

Enable Gizmos in the Scene view to see the two cones (cyan = left eye, magenta =
right) while the game is running, for tuning `maxRange` / fallback values.

### `VisibilityGatedVersionSwitcher`
Add to any object that has multiple interchangeable versions.

1. Set **Versions** to the GameObjects for each version (siblings under the same
   parent works well — only one is active at a time).
2. Set **Starting Version** to the index that should be active on load.
3. Optionally set **Bounds Source** to specific renderers to check against; leave it
   empty to auto-use the current version's renderers.
4. Call `RequestSwitchTo(index)` (from a Signal Receiver, `UnityEvent`, or your own
   code) whenever a version change should happen. It applies immediately if the
   object is already out of view, otherwise it waits — checking every frame — until
   it leaves both eye cones, then swaps instantly.
5. `onVersionApplied` (a `UnityEvent<int>`) fires right after a swap actually happens,
   in case something needs to react (e.g. re-enable physics on the new version).

`visibilityMargin` adds extra meters to the bounds radius used for the check, so a
swap waits until the object is a bit further out of view rather than right at the
frustum edge.

### `VersionSwitchGroup`
Optional fan-out helper: wire one trigger (e.g. one Signal Receiver) to a
`VersionSwitchGroup`, list every `VisibilityGatedVersionSwitcher` that should react in
its **Switchers** list, and call `RequestSwitchTo(index)` once to drive all of them.
Each switcher still gates its own swap independently — the group only centralizes the
trigger.

### `SignalPing` (debug aid, delete when done)
Wire a Signal Receiver's event to `Ping()` to confirm in the Console that a given
Signal Asset actually fired, with a timestamp. Meant to be temporary — remove once
the Timeline wiring is confirmed working.

### Timeline / Signal assets
`TimeLine.playable` plus the three `.signal` assets (`Sceen 1 change`,
`Sceen 2 change 1`, `Sceen 3 change 2`) and the animation clips (`Start Walking`,
`Talking`, `Telling A Secret`, `Leaning On A Wall`) are the authoring-side pieces:
place Signal Emitters on the timeline where a version change should be requested, and
point their Signal Receiver at a `VersionSwitchGroup`/`VisibilityGatedVersionSwitcher`.

### `Controler.controller`
An Animator Controller scaffold for driving the animation clips above. Currently
empty (no states/parameters wired up yet) — add states for each clip and hook up
transitions before relying on it.

## Typical setup checklist
1. Confirm exactly one `EyeVisibilityCones` exists in the scene, attached near/at the
   XR camera.
2. On the object you want to swap, add `VisibilityGatedVersionSwitcher` and assign its
   version GameObjects.
3. Add a Signal Emitter to the Timeline at the desired moment, referencing one of the
   `.signal` assets.
4. Add a Signal Receiver (on any GameObject the Timeline can reach) reacting to that
   signal, calling either the switcher's `RequestSwitchTo(index)` directly or a
   `VersionSwitchGroup.RequestSwitchTo(index)` if multiple objects should switch
   together.
5. Play the Timeline and confirm in the Console that the swap log
   (`"<name>: switched to version <index>."`) appears only after the object leaves
   view, not while it's on-screen.
