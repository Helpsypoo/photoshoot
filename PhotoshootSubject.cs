using Godot;

// Strategy for Photoshoot: what to put in the model slot and where to save the PNG.
// Subclasses are expected to be [GlobalClass] so they can be picked from the
// Godot resource-new menu and edited inline on the Photoshoot node.
[Tool]
[GlobalClass]
public abstract partial class PhotoshootSubject : Resource
{
    // Populate modelSlot with the subject. Implementations may inspect existing
    // children to preserve editor tweaks when the desired model is already loaded,
    // or be a no-op for manual workflows where the user places the model by hand.
    public abstract void LoadModel(Node3D modelSlot);

    // res:// or absolute path for Photoshoot.SnapPicture to write the PNG to.
    // Returning null/empty aborts the capture with an error log.
    public abstract string GetSavePath();

    // Helper for LoadModel: frees whatever is in the slot.
    protected static void ClearSlot(Node3D modelSlot)
    {
        foreach (Node child in modelSlot.GetChildren())
        {
            modelSlot.RemoveChild(child);
            child.QueueFree();
        }
    }

    // Helper for LoadModel: instantiates scene into the slot. With stripScripts,
    // what goes in the slot is a script-free copy of the instance (see
    // StripScripts), so a gameplay prefab (one whose _Ready looks up services,
    // registers for picking, or hides parts of itself from game state) is
    // photographed as plain meshes. Scripts without [Tool] never run in the
    // editor anyway; this is for snapping from a running scene. Returns the
    // node put in the slot, or null when the scene is null.
    protected static Node3D InstantiateInto(Node3D modelSlot, PackedScene scene, bool stripScripts)
    {
        if (scene == null) return null;
        var instance = scene.Instantiate<Node3D>(PackedScene.GenEditState.Instance);
        if (stripScripts) instance = StripScripts(instance);
        modelSlot.AddChild(instance);
        return instance;
    }

    // A copy of node and its whole subtree with every script left out (nodes,
    // properties, transforms and materials kept), and the original freed. A copy
    // rather than set_script(null) in place: removing a C# script from a live
    // node throws from Godot's bridge. The node must not be in the tree yet.
    protected static Node3D StripScripts(Node3D node)
    {
        const int withoutScripts = (int)(Node.DuplicateFlags.Signals | Node.DuplicateFlags.Groups);
        var copy = (Node3D)node.Duplicate(withoutScripts);
        node.Free();
        return copy;
    }
}
