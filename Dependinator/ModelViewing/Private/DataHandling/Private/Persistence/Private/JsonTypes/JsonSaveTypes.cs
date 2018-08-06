using System;
using System.Collections.Generic;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.JsonTypes
{
    public static class JsonSaveTypes
    {
        public static string Version = "1";


        // A model contains a list of nodes, lines
        [Serializable]
        public class Model
        {
            public string FormatVersion { get; set; } = Version;
            public List<Node> Nodes { get; set; }
        }


        [Serializable]
        public class Node
        {
            public string Name { get; set; }
            public string Bounds { get; set; }
            public string Color { get; set; }
            public double Scale { get; set; }
            public List<Line> Lines { get; set; }
        }


        [Serializable]
        public class Line
        {
            public string Target { get; set; }
            public List<string> Points { get; set; }
        }
    }
}
