using Godot;

// What Photoshoot photographs and where the PNG goes, with every field in the
// inspector: the scene to load into %ModelSlot (or none, to place a model by
// hand), the full save path, whether to strip the model's scripts, and how the
// model is turned. Make one per picture as a .tres, or edit it inline on the
// Photoshoot node. A project can still subclass it when the scene or the path
// is derived from something else: LoadModel and GetSavePath are virtual.
[Tool]
[GlobalClass]
public partial class PhotoshootSubject : Resource
{
    private PackedScene _modelScene;
    private string _savePath = "res://photoshoot_results/snap.png";
    private bool _stripScripts = true;
    private Vector3 _modelRotationDegrees;

    // The scene to photograph, loaded into %ModelSlot. Leave it empty to put a
    // model under the slot by hand; the slot is then left alone.
    [Export]
    public PackedScene ModelScene
    {
        get => _modelScene;
        set
        {
            if (_modelScene == value) return;
            _modelScene = value;
            EmitChanged();
        }
    }

    // Where Snap Picture writes the PNG, in full: a res:// path or an absolute one.
    [Export(PropertyHint.SaveFile, "*.png")]
    public string SavePath
    {
        get => _savePath;
        set => _savePath = value;
    }

    // Load the model with every script removed, so a gameplay scene (one whose
    // _ready looks for the game, registers for picking, or hides parts of itself
    // from game state) is photographed as plain meshes. Off keeps [Tool] scripts
    // that draw the model in the editor.
    [Export]
    public bool StripScripts
    {
        get => _stripScripts;
        set
        {
            if (_stripScripts == value) return;
            _stripScripts = value;
            EmitChanged();
        }
    }

    // How the model is turned in the slot, in degrees: a box shows a corner, a
    // hat its front.
    [Export]
    public Vector3 ModelRotationDegrees
    {
        get => _modelRotationDegrees;
        set
        {
            if (_modelRotationDegrees == value) return;
            _modelRotationDegrees = value;
            EmitChanged();
        }
    }

    // Puts the subject in the slot. With no ModelScene the slot is left as it is,
    // for a model placed by hand.
    public virtual void LoadModel(Node3D modelSlot)
    {
        if (ModelScene == null) return;
        ClearSlot(modelSlot);
        var model = InstantiateInto(modelSlot, ModelScene, StripScripts);
        if (model == null) return;
        model.RotationDegrees = ModelRotationDegrees;
        Freeze(model);
    }

    // The path Photoshoot.SnapPicture writes to. Null or empty aborts the
    // capture with an error log.
    public virtual string GetSavePath() =>
        string.IsNullOrWhiteSpace(SavePath) ? null : SavePath.Trim();

    // Helper for LoadModel: frees whatever is in the slot.
    protected static void ClearSlot(Node3D modelSlot)
    {
        foreach (Node child in modelSlot.GetChildren())
        {
            modelSlot.RemoveChild(child);
            child.QueueFree();
        }
    }

    // Helper for LoadModel: instantiates scene into the slot, as a script-free
    // copy when stripScripts is set (see WithoutScripts). Returns the node put
    // in the slot, or null when the scene is null.
    protected static Node3D InstantiateInto(Node3D modelSlot, PackedScene scene, bool stripScripts)
    {
        if (scene == null) return null;
        var instance = scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance);
        if (stripScripts) instance = WithoutScripts(instance);
        modelSlot.AddChild(instance);
        return instance;
    }

    // A copy of node and its whole subtree with every script left out (nodes,
    // properties, transforms and materials kept), and the original freed. A copy
    // rather than set_script(null) in place: removing a C# script from a live
    // node throws from Godot's bridge. The node must not be in the tree yet.
    protected static Node3D WithoutScripts(Node3D node)
    {
        const int withoutScripts = (int)(Node.DuplicateFlags.Signals | Node.DuplicateFlags.Groups);
        var copy = (Node3D)node.Duplicate(withoutScripts);
        node.Free();
        return copy;
    }

    // Helper for LoadModel: holds every rigid body in the model still, so a
    // scene run outside the editor doesn't drop it out of the shot.
    protected static void Freeze(Node node)
    {
        if (node is RigidBody3D body) body.Freeze = true;
        foreach (Node child in node.GetChildren())
            Freeze(child);
    }
}
