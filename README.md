# Photoshoot

A small Godot 4 (.NET) tool that renders a 3D subject to a PNG with a transparent background: frame
it by hand in the editor, press Snap Picture. For item icons, portraits, anything that wants a
picture of a model.

Pulled out of PrimerTools (its `Photoshoot/` folder, history kept) so it can be shared across
projects on its own. It depends on nothing but Godot.

## Requirements

- Godot 4.x with .NET.
- This repo checked out at `res://addons/Photoshoot`. `Photoshoot.tscn` names its scripts by that
  path.

## What is in it

- `Photoshoot.cs` — the `[Tool]` node. Holds a `PhotoshootSubject`, loads it into `%ModelSlot`,
  and has two inspector buttons: **Fit Camera** (points `%CameraRig` at the subject's bounds and
  backs `%Camera3D` off until the subject fits, `FitPadding` to spare; the viewing angle is the
  rig's rotation, which it leaves alone) and **Snap Picture** (waits for a drawn frame, writes
  `%SubViewport` to the subject's save path). Both are public methods too, so a batch script can run
  the scene and snap a list of subjects without the editor.
- `PhotoshootSubject.cs` — the strategy: `LoadModel(slot)` and `GetSavePath()`. Subclass it per
  project with `[Tool] [GlobalClass]` so it shows in the resource picker. Helpers for subclasses:
  `ClearSlot`, `InstantiateInto(slot, scene, stripScripts)` and `StripScripts`. Strip scripts when
  photographing a gameplay prefab from a running scene, so its `_Ready` doesn't look for the game.
- `ManualPhotoshootSubject.cs` — no model loading: put whatever you like under `%ModelSlot` by hand.
  Saves to `res://photoshoot_results/<FileName>_<timestamp>.png`.
- `Photoshoot.tscn` — a 512x512 transparent `SubViewport` with a key light, a fill light (off), the
  camera rig and the model slot. Copy it into your project (keeping the unique names) when you want
  per-project framing and lights, or use it as is with a manual subject.

## Using it

1. Open `Photoshoot.tscn` (or your project's copy) in the editor.
2. On the `Photoshoot` node, set **Subject** (a `ManualPhotoshootSubject`, or your own).
3. Rotate `%CameraRig` for the angle you want, press **Fit Camera**, then nudge the camera.
4. Press **Snap Picture**. The PNG lands where the subject's `GetSavePath()` says.

Don't save the scene while a subject's model is loaded: the loaded nodes are owned by the scene root
so you can edit them, and a save would bake them into the `.tscn`.

A project-specific subject looks like this:

```csharp
[Tool]
[GlobalClass]
public partial class ItemPhotoshootSubject : PhotoshootSubject
{
    [Export] public PackedScene Prefab { get; set; }
    [Export] public string FileName { get; set; } = "item";

    public override void LoadModel(Node3D modelSlot)
    {
        ClearSlot(modelSlot);
        InstantiateInto(modelSlot, Prefab, stripScripts: true);
    }

    public override string GetSavePath() => $"res://05_Sprites/ItemIcons/{FileName}.png";
}
```

## Sharing across projects

Shared the way the other `Primer/Tools/*` repos are: each consumer project checks out its own
branch as a git worktree inside its `addons/` folder (see `_sync/README.md`). From this repo's
canonical checkout:

```
./_sync/add-consumer.sh <project> /abs/path/to/Project/addons/Photoshoot
```

and add `/addons/Photoshoot/` to the project's `.gitignore`. Edit in the worktree; `_sync/sync.sh`
brings the change back to `main`.

`.uid` files are tracked, so script references stay valid across machines.
