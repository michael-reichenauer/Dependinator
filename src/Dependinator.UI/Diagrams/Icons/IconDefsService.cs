using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams.Icons;

// Provides the icon <defs> for the diagram canvas, filtered to the icons the current model
// actually references. The full library is hundreds of KB of markup (all icons plus tint
// variants), while a model typically uses a dozen icons; emitting only those keeps the DOM
// and the WebKit paint-server refresh small.
interface IIconDefsService
{
    string Defs { get; }
}

[Scoped]
class IconDefsService : IIconDefsService
{
    readonly IModelMgr modelMgr;

    // The defs string is kept reference-stable while the used-icon set is unchanged, so the
    // Blazor diff skips the defs DOM on model changes that do not alter any icon.
    string defs = "";
    string usedNamesKey = "";
    bool isStale = true;

    public IconDefsService(IModelMgr modelMgr, IApplicationEvents applicationEvents)
    {
        this.modelMgr = modelMgr;
        applicationEvents.ModelChanged += () => isStale = true;
    }

    public string Defs
    {
        get
        {
            if (isStale)
                Update();
            return defs;
        }
    }

    void Update()
    {
        isStale = false;

        // Icon.GetIconName is the same resolution the tile renderer uses (custom icons, tint
        // variants, fallbacks), so every icon a tile references is guaranteed to be included.
        SortedSet<string> names = new(StringComparer.Ordinal);
        using (var model = modelMgr.UseModel())
        {
            foreach (var node in model.Nodes.Values)
                names.Add(Icon.GetIconName(node));
        }

        var namesKey = names.Join(",");
        if (namesKey == usedNamesKey)
            return;

        usedNamesKey = namesKey;
        defs = names.Where(IconLibrary.Contains).Select(IconLibrary.Get).Join("\n");
    }
}
