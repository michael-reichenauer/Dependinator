using System;
using System.Collections.Generic;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.JsonTypes
{
    public static class JsonCacheTypes
    {
        public static string Version = "5";


        // A model contains a list of nodes, links and lines
        [Serializable]
        public class Model
        {
            public string FormatVersion { get; set; } = Version;
            public List<Item> Items { get; set; }
        }


        [Serializable]
        public class Item
        {
            public Node Node { get; set; }
            public Link Link { get; set; }
            public Line Line { get; set; }
        }


        // A node
        [Serializable]
        public class Node
        {
            // The name of a node with '.' separating hierarchy, e.g. like in namespaces
            //public string Id { get; set; }
            public string Name { get; set; }

            // Optional data like type, node location and size ...
            public string Parent { get; set; }
            public string Type { get; set; }
            public string Description { get; set; }
            public string Bounds { get; set; }
            public string Color { get; set; }
            public double Scale { get; set; }
            public string State { get; set; }
        }


        // Link between two nodes
        [Serializable]
        public class Link
        {
            // The source node name
            public string Source { get; set; }

            // The target node name
            public string Target { get; set; }
        }


        // Line between two nodes (with list of links)
        [Serializable]
        public class Line
        {
            // The source node name
            public string Source { get; set; }

            // The target node name
            public string Target { get; set; }

            public List<string> Points { get; set; }

            public int LinkCount { get; set; }
        }


        internal static class NodeType
        {
            public const string Solution = "Solution";
            public const string Assembly = "Assembly";
            public const string Group = "Group";
            public const string Dll = "Dll";
            public const string Exe = "Exe";
            public const string NameSpace = "NameSpace";
            public const string Type = "Type";
            public const string Member = "Member";
            public const string SolutionFolder = "SolutionFolder";
        }
    }
}
