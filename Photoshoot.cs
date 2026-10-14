using Godot;
using System.Threading.Tasks;

// Photoshoot: render a subject to PNG with a transparent background. Runs as a
// [Tool] so the configured subject appears in the editor for framing, posing,
// and animation scrubbing. Set a PhotoshootSubject in the inspector (a .tres or
// one made inline: the scene to load, the save path, whether to strip scripts,
// a rotation), frame the shot (Fit Camera gives a starting point), click Snap
// Picture to write the PNG at the subject's save path.
//
// Capture is meant to happen in the editor, so tweaked poses aren't overwritten
// by AnimationPlayer autoplay. SnapPicture and FitCamera are public all the
// same, so a project's batch script can run the scene and snap a list of
// subjects without the editor.
//
// The loaded model is owned by the scene root so its subtree is editable in the
// Scene dock. For a save of the scene the slot's children are disowned, so
// they are never written into the .tscn, and claimed back afterwards.
[Tool]
public partial class Photoshoot : Node
{
    private PhotoshootSubject _subject;
    [Export]
    public PhotoshootSubject Subject
    {
        get => _subject;
        set
        {
            if (_subject != null) _subject.Changed -= OnSubjectChanged;
            _subject = value;
            if (_subject != null) _subject.Changed += OnSubjectChanged;
            ReloadModel();
        }
    }

    // Extra room left around the subject by Fit Camera, as a multiplier on the
    // distance that would just fit its bounds. 1 touches the edges.
    [Export(PropertyHint.Range, "1,3,0.05")]
    public float FitPadding { get; set; } = 1.15f;

    [ExportToolButton("Fit Camera")]
    public Callable FitCameraButton => Callable.From(FitCamera);

    // A statement lambda: an expression lambda would hand the Task back to
    // Godot, which can't turn it into a Variant.
    [ExportToolButton("Snap Picture")]
    public Callable SnapPictureButton => Callable.From(() => { _ = SnapPicture(); });

    // Lazy accessors. The node references are re-resolved from %-unique-names
    // on demand because [Tool] Photoshoot can receive inspector edits before
    // _Ready (during deserialization) and after hot-reload (which swaps the C#
    // instance but does not re-fire _Ready), leaving stale/null cached fields.
    private SubViewport _viewport;
    private SubViewport Viewport
    {
        get
        {
            if (_viewport == null && IsInsideTree())
                _viewport = GetNodeOrNull<SubViewport>("%SubViewport");
            return _viewport;
        }
    }

    private Node3D _modelSlot;
    private Node3D ModelSlot
    {
        get
        {
            if (_modelSlot == null && IsInsideTree())
                _modelSlot = GetNodeOrNull<Node3D>("%ModelSlot");
            return _modelSlot;
        }
    }

    private Node3D _cameraRig;
    private Node3D CameraRig
    {
        get
        {
            if (_cameraRig == null && IsInsideTree())
                _cameraRig = GetNodeOrNull<Node3D>("%CameraRig");
            return _cameraRig;
        }
    }

    private Camera3D _camera;
    private Camera3D Camera
    {
        get
        {
            if (_camera == null && IsInsideTree())
                _camera = GetNodeOrNull<Camera3D>("%Camera3D");
            return _camera;
        }
    }

    public override void _Ready()
    {
        ReloadModel();
    }

    public override void _Notification(int what)
    {
        // The editor tells every node of the scene before and after it writes the
        // .tscn. Nodes with no owner are left out of the file.
        if (what == NotificationEditorPreSave) SetSlotOwner(null);
        else if (what == NotificationEditorPostSave) ClaimOwnership(ModelSlot);
    }

    private void OnSubjectChanged()
    {
        ReloadModel();
    }

    private void ReloadModel()
    {
        var slot = ModelSlot;
        if (slot == null || _subject == null) return;
        _subject.LoadModel(slot);
        ClaimOwnership(slot);
    }

    // Give every node under the slot Owner = edited scene root so the subtree
    // is visible and editable in the Scene dock while framing the shot.
    // Editor-only: outside the editor, Owner is meaningless for behavior.
    private void ClaimOwnership(Node3D slot)
    {
        if (!Engine.IsEditorHint()) return;
        var root = GetTree()?.EditedSceneRoot;
        if (root == null || slot == null) return;
        SetSlotOwner(root);
    }

    private void SetSlotOwner(Node owner)
    {
        var slot = ModelSlot;
        if (slot == null) return;
        foreach (Node child in slot.GetChildren())
            SetOwnerRecursive(child, owner);
    }

    private static void SetOwnerRecursive(Node node, Node owner)
    {
        node.Owner = owner;
        foreach (Node child in node.GetChildren())
            SetOwnerRecursive(child, owner);
    }

    // Points the camera rig at the centre of everything visible under the model
    // slot and backs the camera off along the rig's view direction until every
    // corner of the subject's bounds is in view, with FitPadding to spare. The
    // rig's rotation (the viewing angle) is left alone, so set the angle first,
    // fit, then nudge by hand.
    public void FitCamera()
    {
        var slot = ModelSlot;
        var rig = CameraRig;
        var camera = Camera;
        var viewport = Viewport;
        if (slot == null || rig == null || camera == null || viewport == null)
        {
            GD.PrintErr("[photoshoot] Fit Camera needs %SubViewport, %ModelSlot, %CameraRig and %Camera3D in the scene");
            return;
        }
        if (!TryGetBounds(slot, out Aabb bounds))
        {
            GD.PrintErr("[photoshoot] Nothing visible under the model slot to fit");
            return;
        }

        // The camera sits at (0, 0, d) in the rig's space looking down -Z, so a corner at rig-local
        // (x, y, z) is (d - z) in front of it and must fit the frustum there: |y| within the vertical
        // half angle, |x| within the horizontal one (Godot's fov is vertical; the aspect gives the
        // horizontal). The distance is the largest any corner needs.
        Vector3 centre = bounds.GetCenter();
        Basis toRig = rig.GlobalBasis.Inverse();
        float tanHalfV = Mathf.Tan(Mathf.DegToRad(camera.Fov) / 2f);
        Vector2I size = viewport.Size;
        float aspect = size.Y > 0 ? (float)size.X / size.Y : 1f;
        float distance = 0f;
        float depth = 0f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = toRig * (bounds.GetEndpoint(i) - centre);
            float lateral = Mathf.Max(Mathf.Abs(corner.Y), Mathf.Abs(corner.X) / aspect) * FitPadding;
            distance = Mathf.Max(distance, corner.Z + lateral / tanHalfV);
            depth = Mathf.Max(depth, Mathf.Abs(corner.Z));
        }
        distance = Mathf.Max(distance, 0.01f);

        rig.GlobalPosition = centre;
        camera.Position = new Vector3(0f, 0f, distance);
        camera.Near = Mathf.Min(camera.Near, Mathf.Max(distance - depth, 0.001f) / 2f);
        camera.Far = Mathf.Max(camera.Far, distance + depth * 2f);
        GD.Print($"[photoshoot] fit: centre {centre}, size {bounds.Size}, distance {distance:0.###}");
    }

    // The world-space box around every visible VisualInstance3D under root.
    private static bool TryGetBounds(Node root, out Aabb bounds)
    {
        bounds = default;
        bool any = false;
        Collect(root, ref bounds, ref any);
        return any;

        static void Collect(Node node, ref Aabb bounds, ref bool any)
        {
            if (node is VisualInstance3D visual && visual.IsVisibleInTree())
            {
                Aabb box = visual.GlobalTransform * visual.GetAabb();
                bounds = any ? bounds.Merge(box) : box;
                any = true;
            }
            foreach (Node child in node.GetChildren())
                Collect(child, ref bounds, ref any);
        }
    }

    // Renders the subject and writes the PNG where the subject says. Awaits the
    // next drawn frame first, so a model loaded or a camera moved this frame is
    // in the picture. In the editor the file is then reimported, so an open
    // scene using the texture shows the new picture at once.
    public async Task SnapPicture()
    {
        var viewport = Viewport;
        if (viewport == null) return;
        if (_subject == null)
        {
            GD.PrintErr("[photoshoot] No Subject assigned");
            return;
        }
        var savePath = _subject.GetSavePath();
        if (string.IsNullOrEmpty(savePath))
        {
            GD.PrintErr("[photoshoot] The subject has no save path");
            return;
        }

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = viewport.GetTexture().GetImage();
        var absPath = ProjectSettings.GlobalizePath(savePath);
        var dir = System.IO.Path.GetDirectoryName(absPath);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);
        var err = img.SavePng(absPath);
        GD.Print($"[photoshoot] save {absPath} => {err}");
        if (err == Error.Ok) ReimportInEditor(savePath);
    }

    // The editor only notices a file its own process wrote when told: register
    // the path and reimport it, which also reloads the texture anywhere it is
    // in use.
    private static void ReimportInEditor(string savePath)
    {
#if TOOLS
        if (!Engine.IsEditorHint() || !savePath.StartsWith("res://")) return;
        var fileSystem = EditorInterface.Singleton.GetResourceFilesystem();
        fileSystem.UpdateFile(savePath);
        fileSystem.ReimportFiles(new[] { savePath });
#endif
    }
}
