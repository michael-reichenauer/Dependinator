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
            public string N { get; set; }
            public string B { get; set; }
            public string C { get; set; }
            public double S { get; set; }
            public List<Line> L { get; set; }
        }


        [Serializable]
        public class Line
        {
            public string T { get; set; }
            public List<string> P { get; set; }
        }
    }
}
