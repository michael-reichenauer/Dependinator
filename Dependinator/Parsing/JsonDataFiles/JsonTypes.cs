namespace Dependinator.Parsing.JsonDataFiles;

public static class JsonTypes
{
    public static string Version = "2";

    // A model contains a list of nodes and links
    [Serializable]
    public class Model
    {
        // The format version
        public string FormatVersion { get; set; } = Version;

        // The list of data items (nodes or links in any order)
        public List<Item> Items { get; set; } = default!;
    }

    // A data item, which can be either a node or a link
    [Serializable]
    public class Item
    {
        public Node Node { get; set; } = default!;
        public Link Link { get; set; } = default!;
    }

    // A node
    [Serializable]
    public class Node
    {
        // The name of a node with '.' separating hierarchy, e.g. like in namespaces
        public string Name { get; set; } = "";

        // Optional data like parent, type, ...
        public string Parent { get; set; } = "";
        public NodeAttributes? Attributes { get; set; }
    }

    public class NodeAttributes
    {
        public string? Description { get; set; }
        public string? Type { get; set; } = "";
    }

    public class LinkAttributes
    {
        public string? Description { get; set; }
        public string? Type { get; set; } = "";
    }

    // Link between two nodes
    [Serializable]
    public class Link
    {
        // The source node name
        public string Source { get; set; } = "";

        // The target node name
        public string Target { get; set; } = "";

        // Optional attributes like target type, ...
        public LinkAttributes? Attributes { get; set; }
    }
}
