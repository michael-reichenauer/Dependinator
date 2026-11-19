using Dependinator.Core.Shared;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling;

// Where a workspace model shows each of its sources: inside the deepest node of another source
// whose folder holds the source's folder, or at the top when none does. A node holds a folder
// when it is a source's root (the folder of the source's file, WorkspaceSources.FolderOf) or a
// project (Node.ProjectFolder, a C# project's folder). So a TypeScript project in a C# project's
// folder is shown in that project, and a script project elsewhere in a solution's folder in the
// solution. A root holds the folders below its own, so two sources in one folder stay side by
// side; a project holds its own folder too for a source of another kind (a package.json beside
// a .csproj is the project's, a second solution in its folder is not), and goes before a root
// of the same folder. A source at the workspace folder (a monorepo's package.json) holds every
// other one below it.
//
// The user can choose another place for a source (Source.ShownIn, WorkspaceSource.ShownIn):
// the top, or a node of another source (its root or a project, Places). A choice whose node is
// not such a node now (left out of the model, or not a project any more) is Automatic until it
// is back.
//
// Decided anew after each parse on the whole model, so that it holds for the sources a refresh
// did not parse too, and a source whose holder is left out of the model goes back to the top.
// The names stay as they are (a node's parent is not part of its name), so the rules, searches
// and notes of a source and its own model still hold. Moving a root changes no layout inside
// it: the root is placed anew in its new parent.
static class SourceNesting
{
    // A source of the model: its key (WorkspaceSources.Key), its root node, and where the user
    // chose to show it (null: Automatic, "": at the top, else a node's name)
    public record Source(string Key, string RootName, string? ShownIn = null);

    // The parent for the root of each source that is in the model, by root name ("" for the top)
    public static IReadOnlyDictionary<string, string> Parents(
        IModel model,
        string workspaceFolder,
        IReadOnlyList<Source> sources
    )
    {
        List<Source> roots =
        [
            .. sources
                .Where(source => model.Nodes.ContainsKey(NodeId.FromName(source.RootName)))
                .DistinctBy(source => source.RootName),
        ];
        IReadOnlySet<string> rootNames = roots.Select(source => source.RootName).ToHashSet(StringComparer.Ordinal);

        // The nodes that hold a folder, each with the index of its source
        List<Holder> holders = [];
        for (var index = 0; index < roots.Count; index++)
        {
            var root = model.Nodes[NodeId.FromName(roots[index].RootName)];
            var kind = WorkspaceSources.ChangeKind(roots[index].Key);
            holders.Add(new(root.Name, WorkspaceSources.FolderOf(roots[index].Key), index, kind, IsProject: false));
            foreach (var node in Projects(model, root, rootNames))
                holders.Add(new(node.Name, Folder(workspaceFolder, node.ProjectFolder!), index, kind, IsProject: true));
        }

        // The deepest holder of another source, a project before a root; the first of equal ones
        var parentOf = new int[roots.Count];
        for (var index = 0; index < roots.Count; index++)
        {
            var folder = WorkspaceSources.FolderOf(roots[index].Key);
            var kind = WorkspaceSources.ChangeKind(roots[index].Key);
            parentOf[index] = -1;
            for (var holder = 0; holder < holders.Count; holder++)
            {
                if (
                    holders[holder].Source != index
                    && holders[holder].Holds(folder, kind)
                    && (parentOf[index] < 0 || holders[holder].IsDeeperThan(holders[parentOf[index]]))
                )
                    parentOf[index] = holder;
            }
        }

        // The user's choices that can be followed: the top, or a holder of another source (its
        // root or a project of it, as Places offers), and the source of each parent (-1: none)
        var parentName = new string[roots.Count];
        var parentSource = new int[roots.Count];
        var isChosen = new bool[roots.Count];
        for (var index = 0; index < roots.Count; index++)
        {
            (parentName[index], parentSource[index]) =
                parentOf[index] < 0 ? ("", -1) : (holders[parentOf[index]].Name, holders[parentOf[index]].Source);
            var shownIn = roots[index].ShownIn;
            if (shownIn == "")
                (parentName[index], parentSource[index], isChosen[index]) = ("", -1, true);
            else if (
                shownIn is not null
                && holders.FirstOrDefault(h => h.Name == shownIn && h.Source != index) is { } chosen
            )
                (parentName[index], parentSource[index], isChosen[index]) = (shownIn, chosen.Source, true);
        }

        // Sources inside each other (through a C# project outside its solution's folder, a
        // solution in src/app holding src/app/../lib, or through the user's choices): one of them
        // goes to the top, the first that is placed automatically, else the first.
        for (var index = 0; index < roots.Count; index++)
        {
            List<int> loop = [index];
            for (
                var source = parentSource[index];
                source >= 0 && loop.Count <= roots.Count;
                source = parentSource[source]
            )
            {
                if (source != index)
                {
                    loop.Add(source);
                    continue;
                }
                var top = loop.FirstOrDefault(i => !isChosen[i], index);
                (parentName[top], parentSource[top]) = ("", -1);
                break;
            }
        }

        return roots
            .Select((source, index) => (source.RootName, Parent: parentName[index]))
            .ToDictionary(root => root.RootName, root => root.Parent, StringComparer.Ordinal);
    }

    // The nodes the source with the root can be shown in: the roots of the other sources in the
    // model and their projects, in the order of the sources
    public static IReadOnlyList<SourcePlace> Places(IModel model, IReadOnlyList<Source> sources, string rootName)
    {
        IReadOnlySet<string> rootNames = sources.Select(source => source.RootName).ToHashSet(StringComparer.Ordinal);
        List<SourcePlace> places = [];
        foreach (var source in sources.DistinctBy(source => source.RootName))
        {
            if (source.RootName == rootName || !model.Nodes.TryGetValue(NodeId.FromName(source.RootName), out var root))
                continue;
            places.Add(new(root.Name, root.DisplayName));
            places.AddRange(
                Projects(model, root, rootNames).Select(node => new SourcePlace(node.Name, node.DisplayName, true))
            );
        }
        return places;
    }

    // The projects of a source (Node.ProjectFolder), without those of the sources shown inside
    // it. A project is one of this parse (or of a source the model keeps, which KeepNodeTree has
    // stamped), not one that the parse no longer has, since that is removed after the parse.
    static IEnumerable<Node> Projects(IModel model, Node root, IReadOnlySet<string> rootNames) =>
        root.DescendantsAndSelfPreOrder(n => rootNames.Contains(n.Name))
            .Skip(1)
            .Where(node => node.ProjectFolder is not null && node.UpdateStamp == model.UpdateStamp);

    // Moves the root of each source to its parent (Parents), where it is placed anew, and says
    // whether any moved. A move
    // that is not possible yet (into a node that is still inside the root, before that node's
    // own root has moved out) is tried again after the others.
    public static bool Apply(
        IModel model,
        IStructureService structure,
        string workspaceFolder,
        IReadOnlyList<Source> sources
    )
    {
        var moves = Parents(model, workspaceFolder, sources)
            .Where(move => model.Nodes[NodeId.FromName(move.Key)].Parent.Name != move.Value)
            .ToList();
        var isAnyMoved = false;
        while (moves.Count > 0)
        {
            var isMoved = false;
            foreach (var (rootName, parentName) in moves.ToList())
            {
                var root = model.Nodes[NodeId.FromName(rootName)];
                var oldParent = root.Parent;
                if (!structure.MoveNode(model, rootName, parentName))
                    continue;

                root.Boundary = Rect.None;
                oldParent.IsChildrenLayoutRequired = true;
                root.Parent.IsChildrenLayoutRequired = true;
                moves.RemoveAll(move => move.Key == rootName);
                isMoved = true;
                isAnyMoved = true;
            }
            if (!isMoved)
                break;
        }
        if (isAnyMoved)
            model.BumpStructureVersion();
        return isAnyMoved;
    }

    // A parsed source's root comes without a parent: it keeps the one the model gave it, so a
    // refresh that parses it leaves it where it is, unless where it belongs changed (Apply).
    public static IReadOnlyList<Core.Parsing.Item> KeepParents(IModel model, IReadOnlyList<Core.Parsing.Item> items)
    {
        var rootNames = items.Select(item => item.Source?.RootName).OfType<string>().ToHashSet(StringComparer.Ordinal);
        if (rootNames.Count == 0)
            return items;

        return
        [
            .. items.Select(item =>
                item.Node is { Properties.Parent: null } node
                && rootNames.Contains(node.Name)
                && model.Nodes.TryGetValue(NodeId.FromName(node.Name), out var existing)
                && !existing.Parent.IsRoot
                    ? item with
                    {
                        Node = node with { Properties = node.Properties.WithParent(existing.Parent.Name) },
                    }
                    : item
            ),
        ];
    }

    // A project's folder as a key's folder is written: below the workspace folder with "/"
    // ("" for the workspace folder itself), or its whole path when it is outside.
    static string Folder(string workspaceFolder, string folder) =>
        WorkspaceSources.Key(workspaceFolder, folder.TrimEnd('/', '\\') + "/").TrimEnd('/');

    // A node that holds a folder: a source's root, or a project in it
    record Holder(string Name, string Folder, int Source, string Kind, bool IsProject)
    {
        // A root holds the folders below its own, a project its own one too for a source of
        // another kind (WorkspaceSources.ChangeKind)
        public bool Holds(string folder, string kind) =>
            IsBelow(folder, Folder)
            || IsProject && kind != Kind && string.Equals(folder, Folder, StringComparison.OrdinalIgnoreCase);

        public bool IsDeeperThan(Holder other) =>
            Folder.Length > other.Folder.Length
            || Folder.Length == other.Folder.Length && IsProject && !other.IsProject;
    }

    // Whether a folder is below another one ("" is the workspace folder, which holds every
    // folder in it, but not a source's whole path outside it)
    static bool IsBelow(string folder, string holder) =>
        holder == ""
            ? folder != "" && !FileSpanPaths.IsAbsolute(folder)
            : folder.StartsWith(holder + "/", StringComparison.OrdinalIgnoreCase);
}
