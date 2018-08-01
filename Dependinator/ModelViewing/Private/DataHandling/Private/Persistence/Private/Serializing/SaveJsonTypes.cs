using System;
using System.Collections.Generic;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.Serializing
{
    public static class SaveJsonTypes
    {
        public static string Version = "1";


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
            public Line Line { get; set; }
        }


        // A node
        [Serializable]
        public class Node
        {
            public string Name { get; set; }
            public string Bounds { get; set; }
            public string Color { get; set; }
            public double Scale { get; set; }
            public string State { get; set; }
        }


        // A node
        [Serializable]
        public class Line
        {
            // The source node name
            public string Source { get; set; }

            // The target node name
            public string Target { get; set; }

            public List<string> Points { get; set; }
        }
    }
}
