using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.JsonTypes;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Threading;
using Newtonsoft.Json;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private
{
    internal class SaveSerializer : ISaveSerializer
    {
        private static readonly char[] PartSeparator = ".".ToCharArray();
        private static readonly char[] LineSeparator = "|".ToCharArray();
        private static readonly char[] ItemSeparator = ",".ToCharArray();


        public Task SerializeAsync(IReadOnlyList<IDataItem> items, string path)
        {
            return Task.Run(() =>
            {
                try
                {
                    List<JsonSaveTypes.Node> nodes = ToSaveNodes(items);
                    AddLinesToNodes(items, nodes);

                    ShortenNodeNames(nodes);

                    //var compressedNodes = ToCompressedNodes(nodes);

                    JsonSaveTypes.Model dataModel = new JsonSaveTypes.Model {Nodes = nodes};

                    Serialize(path, dataModel);
                }
                catch (Exception e)
                {
                    Log.Exception(e, $"Failed to serialize to {path}");
                }
            });
        }


        public Task SerializeMergedAsync(IReadOnlyList<IDataItem> items, string path)
        {
            return Task.Run(() =>
            {
                try
                {
                    Timing t = Timing.Start();
                    List<JsonSaveTypes.Node> nodes = ToSaveNodes(items);
                    t.Log("Json items");

                    AddLinesToNodes(items, nodes);
                    t.Log("added lines");

                    MergeInPreviousSavedNodes(path, nodes);
                    t.Log("merged with previous");

                    ShortenNodeNames(nodes);
                    t.Log("shortened node names");

                    //var compressedNodes = ToCompressedNodes(nodes);
                    //t.Log("compressed nodes");

                    JsonSaveTypes.Model dataModel = new JsonSaveTypes.Model {Nodes = nodes };

                    Serialize(path, dataModel);
                    t.Log("serailaized");
                }
                catch (Exception e)
                {
                    Log.Exception(e, $"Failed to serialize to {path}");
                }
            });
        }


        public Task<R<IReadOnlyList<IDataItem>>> DeserializeAsync(string path)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        return R.NoValue;
                    }

                    var model = Deserialize<JsonSaveTypes.Model>(path);
                    if (model.FormatVersion != JsonSaveTypes.Version)
                    {
                        Log.Warn($"Unexpected format {model.FormatVersion}, expected {JsonSaveTypes.Version}");
                        return R.NoValue;
                    }

                   // List<JsonSaveTypes.Node> nodes = ToDecompressedNodes(model.Nodes);
                    List<JsonSaveTypes.Node> nodes = model.Nodes;
                    ExpandNodeNames(nodes);

                    var dataNodes = nodes.Select(Convert.ToDataNode);
                    var dataLines = GetDataLines(nodes);

                    IReadOnlyList<IDataItem> dataItems = dataNodes.Concat(dataLines).ToList();

                    return R.From(dataItems);
                }
                catch (Exception e)
                {
                    Log.Exception(e, $"Failed to serialize to {path}");
                    return R.NoValue;
                }
            });
        }


        private IEnumerable<IDataItem> GetDataLines(List<JsonSaveTypes.Node> modelNodes)
        {
            return modelNodes
                .Where(node => node.L != null)
                .ForEach(node => ExpandLineNames(node.N, node.L))
                .SelectMany(node => node.L.Select(line => ToDataLine(line, node)));
        }


        private static IDataItem ToDataLine(JsonSaveTypes.Line line, JsonSaveTypes.Node node) =>
            new DataLine(
                (DataNodeName)node.N,
                (DataNodeName)line.T,
                line.P.Select(Point.Parse).ToList(),
                0);


        private static void MergeInPreviousSavedNodes(string path, List<JsonSaveTypes.Node> nodes)
        {
            if (!File.Exists(path))
            {
                return;
            }

            Dictionary<string, JsonSaveTypes.Node> previousNodes = GetPreviousNodes(path);

            // Add all previous nodes, that are not already in nodes
            nodes.ForEach(node => previousNodes.Remove(node.N));
            previousNodes.ForEach(pair => nodes.Add(pair.Value));

            // resort nodes
            nodes = nodes.OrderBy(node => node.N).ToList();
        }


        private static Dictionary<string, JsonSaveTypes.Node> GetPreviousNodes(string path)
        {
            var previousNodes = new Dictionary<string, JsonSaveTypes.Node>();

            try
            {
                JsonSaveTypes.Model model = Deserialize<JsonSaveTypes.Model>(path);
                if (model.FormatVersion == JsonSaveTypes.Version)
                {
                    //List<JsonSaveTypes.Node> nodes = ToDecompressedNodes(model.Nodes);
                    List<JsonSaveTypes.Node> nodes = model.Nodes;
                    ExpandNodeNames(nodes);

                    foreach (JsonSaveTypes.Node node in nodes)
                    {
                        previousNodes[node.N] = node;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Failed to deserialize {path}");
            }

            return previousNodes;
        }


        private static List<JsonSaveTypes.Node> ToSaveNodes(IEnumerable<IDataItem> items)
        {
            return items
                .Where(item => item is DataNode)
                .Cast<DataNode>()
                .Select(Convert.ToSaveJsonNode)
                .OrderBy(it => it.N)
                .ToList();
        }


        private static Dictionary<string, List<JsonSaveTypes.Line>> ToJsonLines(
            IEnumerable<IDataItem> items)
        {
            Dictionary<string, List<JsonSaveTypes.Line>> lines =
                new Dictionary<string, List<JsonSaveTypes.Line>>();

            foreach (DataLine line in items.Where(item => item is DataLine).Cast<DataLine>())
            {
                if (!lines.TryGetValue((string)line.Source, out var nodeLines))
                {
                    nodeLines = new List<JsonSaveTypes.Line>();
                    lines[(string)line.Source] = nodeLines;
                }

                nodeLines.Add(Convert.ToSaveJsonLine(line));
            }

            return lines;
        }


        private static void AddLinesToNodes(
            IReadOnlyList<IDataItem> items,
            IEnumerable<JsonSaveTypes.Node> nodes)
        {
            Dictionary<string, List<JsonSaveTypes.Line>> lines = ToJsonLines(items);

            foreach (JsonSaveTypes.Node node in nodes)
            {
                if (lines.TryGetValue(node.N, out var nodeLines))
                {
                    node.L = nodeLines.OrderBy(line => line.T).ToList();
                    ShortenLineNames(node.N, node.L);
                }
            }
        }


        private static void ShortenNodeNames(IReadOnlyList<JsonSaveTypes.Node> nodes)
        {
            for (int i = nodes.Count - 1; i > 0; i--)
            {
                string[] prefixParts = nodes[i - 1].N.Split(PartSeparator);

                for (int partIndex = prefixParts.Length; partIndex >= 0; partIndex--)
                {
                    string prefix = string.Join(".", prefixParts.Take(partIndex)) + ".";

                    if (nodes[i].N.StartsWith(prefix))
                    {
                        var suffix = nodes[i].N.Substring(prefix.Length);
                        nodes[i].N = new string('.', partIndex) + suffix;
                        break;
                    }
                }
            }
        }


        private static void ExpandNodeNames(IReadOnlyList<JsonSaveTypes.Node> nodes)
        {
            for (int i = 1; i < nodes.Count; i++)
            {
                string name = nodes[i].N;
                int partIndex = name.TakeWhile(c => c == '.').Count();

                if (partIndex > 0)
                {
                    string[] prefixParts = nodes[i - 1].N.Split(PartSeparator);
                    string prefix = string.Join(".", prefixParts.Take(partIndex));
                    nodes[i].N = $"{prefix}.{name.TrimStart('.')}";
                }
            }
        }


        private static void ShortenLineNames(string sourceName, IReadOnlyList<JsonSaveTypes.Line> lines)
        {
            string[] prefixParts = sourceName.Split(PartSeparator);

            foreach (JsonSaveTypes.Line line in lines)
            {
                for (int partIndex = prefixParts.Length; partIndex >= 0; partIndex--)
                {
                    string prefix = string.Join(".", prefixParts.Take(partIndex)) + ".";

                    if (line.T.StartsWith(prefix))
                    {
                        var suffix = line.T.Substring(prefix.Length);
                        line.T = new string('.', partIndex) + suffix;
                        break;
                    }
                }
            }
        }


        private static void ExpandLineNames(string sourceName, IReadOnlyList<JsonSaveTypes.Line> lines)
        {
            string[] prefixParts = sourceName.Split(PartSeparator);

            foreach (JsonSaveTypes.Line line in lines)
            {
                int partIndex = line.T.TakeWhile(c => c == '.').Count();

                if (partIndex > 0)
                {
                    string prefix = string.Join(".", prefixParts.Take(partIndex));
                    line.T = $"{prefix}.{line.T.TrimStart('.')}";
                }
            }
        }


        private static void Serialize(string path, object dataModel)
        {
            JsonSerializer jsonSerializer = CreateSerializer();

            using (StreamWriter stream = new StreamWriter(path))
            {
                jsonSerializer.Serialize(stream, dataModel);
            }
        }


        public static T Deserialize<T>(string path)
        {
            JsonSerializer jsonSerializer = CreateSerializer();

            using (StreamReader stream = new StreamReader(path))
            {
                return (T)jsonSerializer.Deserialize(stream, typeof(T));
            }
        }


        // Used for serializing data and ignores null and default values
        private static JsonSerializer CreateSerializer()
        {
            JsonSerializer jsonSerializer = new JsonSerializer
            {
                Formatting = Formatting.Indented,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

            jsonSerializer.Converters.Add(new ItemJsonConverter());

            return jsonSerializer;
        }


        private static List<List<string>> ToCompressedNodes(IEnumerable<JsonSaveTypes.Node> nodes) => 
            nodes.Select(ToCompressedNode).ToList();


        private static List<JsonSaveTypes.Node> ToDecompressedNodes(IEnumerable<List<string>> nodes) =>
            nodes.Select(ToSaveNode).ToList();


        private static List<string> ToCompressedNode(JsonSaveTypes.Node node) =>
            node.L == null
                ? new List<string>
                {
                    node.N,
                    node.B,
                    node.S.ToString()
                }
                : new List<string>
                {
                    node.N,
                    node.B,
                    node.S.ToString(),
                    ToSaveListLines(node)
                };


        private static JsonSaveTypes.Node ToSaveNode(IReadOnlyList<string> node)
        {
            JsonSaveTypes.Node saveNode = new JsonSaveTypes.Node
            {
                N = node[0],
                B = node[1],
                S = double.Parse(node[2])
            };

            if (node.Count == 4) saveNode.L = ToSaveLines(node[3]);

            return saveNode;
        }


        private static List<JsonSaveTypes.Line> ToSaveLines(string linesText)
        {
            string[] linesParts = linesText.Split(LineSeparator);
            return linesParts.Select(ToSaveLine).ToList();
        }


        private static JsonSaveTypes.Line ToSaveLine(string lineText)
        {
            string[] lineParts = lineText.Split(ItemSeparator);
            JsonSaveTypes.Line saveLine = new JsonSaveTypes.Line
            {
                T = lineParts[0],
                P = new List<string>()
            };

            for (int i = 1; i < lineParts.Length; i += 2)
            {
                saveLine.P.Add($"{lineParts[i]},{lineParts[i + 1]}");
            }

            return saveLine;
        }


        private static string ToSaveListLines(JsonSaveTypes.Node node) =>
            string.Join("|", node.L.Select(ToSaveListLine));


        private static string ToSaveListLine(JsonSaveTypes.Line line) =>
            $"{line.T},{ToSaveListPoints(line)}";


        private static string ToSaveListPoints(JsonSaveTypes.Line line) =>
            string.Join(",", line.P);
    }
}
