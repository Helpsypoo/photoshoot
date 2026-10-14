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
  backs `%Camera3D` off until every corner is in view, `FitPadding` to spare; the viewing angle is
  the rig's rotation, which it leaves alone) and **Snap Picture** (waits for a drawn frame, writes
  `%SubViewport` to the subject's save path, then has the editor reimport the file so anything
  using the texture updates at once). Both are public methods too, so a batch script can run the
  scene and snap a list of subjects without the editor.
- `PhotoshootSubject.cs` — what to photograph and where the PNG goes, every field in the
  inspector: **Model Scene** (loaded into the slot; leave it empty to place a model by hand),
  **Save Path** (the full path, with a file picker), **Strip Scripts** (load the model as plain
  meshes, so a gameplay scene's `_ready` never runs; off keeps `[Tool]` scripts that draw the
  model) and **Model Rotation Degrees**. Make one per picture as a `.tres`, or edit it inline on the
  Photoshoot node. `LoadModel` and `GetSavePath` are virtual for a project whose scene or path is
  derived from something else; the helpers `ClearSlot`, `InstantiateInto`, `WithoutScripts` and
  `Freeze` are there for such a subclass.
- `Photoshoot.tscn` — a 512x512 transparent `SubViewport` with a key light, a fill light (off), the
  camera rig and the model slot, with an inline subject. Copy it into your project (keeping the
  unique names) when you want per-project framing and lights, or use it as is.

## Using it

1. Open `Photoshoot.tscn` (or your project's copy) in the editor.
2. On the `Photoshoot` node, fill in the **Subject**: pick a `.tres` or edit the inline one. Set
   its Model Scene and Save Path.
3. Rotate `%CameraRig` for the angle you want, press **Fit Camera**, then nudge the camera.
4. Press **Snap Picture**. The PNG lands at the save path and is imported right away.

The loaded model's nodes are owned by the scene root so you can pose them in the Scene dock. They
are never written into the `.tscn`: the node disowns them for the editor's save and claims them
back after, so saving the scene is safe. A model placed by hand under `%ModelSlot` is transient for
the same reason; keep it as a Model Scene instead.

A subject that derives its path looks like this:

```csharp
[Tool]
[GlobalClass]
public partial class CharacterSubject : PhotoshootSubject
{
    [Export] public CharacterId Character { get; set; }

    public override void LoadModel(Node3D modelSlot)
    {
        ClearSlot(modelSlot);
        InstantiateInto(modelSlot, Characters.SceneFor(Character), stripScripts: false);
    }

    public override string GetSavePath() => $"res://Assets/Portraits/{Character}.png";
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
brings the change back to `main`. The remote is `https://github.com/Helpsypoo/photoshoot.git`.

`.uid` files are tracked, so script references stay valid across machines.
