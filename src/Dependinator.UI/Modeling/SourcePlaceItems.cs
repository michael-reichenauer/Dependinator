namespace Dependinator.UI.Modeling;

// The places a workspace source can be shown in (SourcePlaces) as the items of a list, in the
// sources dialog ("Shown in") and on a source root's node menu ("Show Inside"). An item's value
// is a string that stands for a ShownIn (null: Automatic, "": the top, else a node's name).
static class SourcePlaceItems
{
    public const string Automatic = "auto";
    public const string Top = "top";
    const string NodePrefix = "node:";

    public const string TopLabel = "Top level";

    public record Item(string Value, string Label, bool IsIndented = false);

    public static string ValueOf(string? shownIn) =>
        shownIn switch
        {
            null => Automatic,
            "" => Top,
            _ => NodePrefix + shownIn,
        };

    public static string? ShownInOf(string value) =>
        value == Automatic ? null
        : value == Top ? ""
        : value.StartsWith(NodePrefix, StringComparison.Ordinal) ? value[NodePrefix.Length..]
        : null;

    // Automatic (with the place it gives), the top, the places, and a chosen node that is not in
    // the model now (the source is shown automatically until it is back)
    public static IReadOnlyList<Item> Of(SourcePlaces places)
    {
        List<Item> items = [new(Automatic, AutomaticLabel(places)), new(Top, TopLabel)];
        items.AddRange(places.Places.Select(place => new Item(ValueOf(place.NodeName), place.Label, place.IsProject)));
        if (places.ShownIn is { Length: > 0 } shownIn && places.Places.All(place => place.NodeName != shownIn))
            items.Add(new(ValueOf(shownIn), $"{shownIn} (not in the model)"));
        return items;
    }

    public static string AutomaticLabel(SourcePlaces places) =>
        $"Automatic: {PlaceLabel(places, places.AutomaticParent)}";

    // Where the source is shown now, for a menu's subtitle
    public static string CurrentLabel(SourcePlaces places) =>
        places.ShownIn is null ? AutomaticLabel(places) : Capitalized(PlaceLabel(places, places.ShownIn));

    static string Capitalized(string text) => text == "" ? text : char.ToUpperInvariant(text[0]) + text[1..];

    static string PlaceLabel(SourcePlaces places, string nodeName) =>
        nodeName == "" ? TopLabel.ToLowerInvariant()
        : places.Places.FirstOrDefault(place => place.NodeName == nodeName) is { } place ? $"in {place.Label}"
        : $"in {nodeName}";
}
