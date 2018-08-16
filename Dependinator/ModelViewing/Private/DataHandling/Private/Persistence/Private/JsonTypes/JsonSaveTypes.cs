using System;
using System.Collections.Generic;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.JsonTypes
{
    public static class JsonSaveTypes
    {
        public static string Version = "2";


        // A model contains a list of nodes, lines
        [Serializable]
        public class Model
        {
            public string FormatVersion { get; set; } = Version;
            public string Description { get; set; } =
                "This file contains model layout data. Merge conflicts are not a serious problem. " +
                "Just select one of the conflicting lines." +
                "In worst case, a node or line might be a little off.";
            public List<Node> Nodes { get; set; }
        }


        [Serializable]
        public class Node
        {

            public string N { get; set; }    // Name
            public string B { get; set; }    // Bounds
            public string C { get; set; }    // Color
            public double S { get; set; }    // Scale
            public string St { get; set; }   // State

            public List<Line> L { get; set; }
        }


        [Serializable]
        public class Line
        {
            public string T { get; set; }          // Target
            public List<string> P { get; set; }    // Points
        }
    }
}
