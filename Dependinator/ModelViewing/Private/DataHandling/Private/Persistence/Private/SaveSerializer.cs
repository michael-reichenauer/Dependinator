﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.JsonTypes;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using Newtonsoft.Json;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private
{
    internal class SaveSerializer : ISaveSerializer
    {
        private static readonly char[] PartSeparator = ".".ToCharArray();


        public Task SerializeAsync(IReadOnlyList<IDataItem> items, string path)
        {
            return Task.Run(() =>
            {
                try
                {
                    List<JsonSaveTypes.Node> nodes = ToSaveNodes(items);
                    AddLinesToNodes(items, nodes);

                    ShortenNodeNames(nodes);

                    JsonSaveTypes.Model dataModel = new JsonSaveTypes.Model
                        {Nodes = ToCompressedNodes(nodes)};

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
                    List<JsonSaveTypes.Node> nodes = ToSaveNodes(items);
                    AddLinesToNodes(items, nodes);

                    MergeInPreviousSavedNodes(path, nodes);

                    ShortenNodeNames(nodes);

                    JsonSaveTypes.Model dataModel = new JsonSaveTypes.Model
                        {Nodes = ToCompressedNodes(nodes)};

                    Serialize(path, dataModel);
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
                    var model = Deserialize<JsonSaveTypes.Model>(path);
                    if (model.FormatVersion != JsonSaveTypes.Version)
                    {
                        Log.Warn($"Unexpected format {model.FormatVersion}, expected {JsonSaveTypes.Version}");
                        return R.NoValue;
                    }

                    List<JsonSaveTypes.Node> nodes = ToDecompressedNodes(model.Nodes);
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
                .Where(node => node.Lines != null)
                .ForEach(node => ExpandLineNames(node.Name, node.Lines))
                .SelectMany(node => node.Lines.Select(line => ToDataLine(line, node)));
        }


        private static IDataItem ToDataLine(JsonSaveTypes.Line line, JsonSaveTypes.Node node) =>
            new DataLine(
                (DataNodeName)node.Name,
                (DataNodeName)line.Target,
                line.Points.Select(Point.Parse).ToList(),
                0);


        private static void MergeInPreviousSavedNodes(string path, List<JsonSaveTypes.Node> nodes)
        {
            Dictionary<string, JsonSaveTypes.Node> previousNodes = GetPreviousNodes(path);

            // Add all previous nodes, that are not already in nodes
            nodes.ForEach(node => previousNodes.Remove(node.Name));
            previousNodes.ForEach(pair => nodes.Add(pair.Value));

            // resort nodes
            nodes = nodes.OrderBy(node => node.Name).ToList();
        }


        private static Dictionary<string, JsonSaveTypes.Node> GetPreviousNodes(string path)
        {
            var previousNodes = new Dictionary<string, JsonSaveTypes.Node>();

            try
            {
                JsonSaveTypes.Model model = Deserialize<JsonSaveTypes.Model>(path);
                if (model.FormatVersion == JsonSaveTypes.Version)
                {
                    List<JsonSaveTypes.Node> nodes = ToDecompressedNodes(model.Nodes);
                    ExpandNodeNames(nodes);

                    foreach (JsonSaveTypes.Node node in nodes)
                    {
                        previousNodes[node.Name] = node;
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
                .OrderBy(it => it.Name)
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
                if (lines.TryGetValue(node.Name, out var nodeLines))
                {
                    node.Lines = nodeLines.OrderBy(line => line.Target).ToList();
                    ShortenLineNames(node.Name, node.Lines);
                }
            }
        }


        private static void ShortenNodeNames(IReadOnlyList<JsonSaveTypes.Node> nodes)
        {
            for (int i = nodes.Count - 1; i > 0; i--)
            {
                string[] prefixParts = nodes[i - 1].Name.Split(PartSeparator);

                for (int partIndex = prefixParts.Length; partIndex >= 0; partIndex--)
                {
                    string prefix = string.Join(".", prefixParts.Take(partIndex)) + ".";

                    if (nodes[i].Name.StartsWith(prefix))
                    {
                        var suffix = nodes[i].Name.Substring(prefix.Length);
                        nodes[i].Name = new string('.', partIndex) + suffix;
                        break;
                    }
                }
            }
        }


        private static void ExpandNodeNames(IReadOnlyList<JsonSaveTypes.Node> nodes)
        {
            for (int i = 1; i < nodes.Count; i++)
            {
                string name = nodes[i].Name;
                int partIndex = name.TakeWhile(c => c == '.').Count();

                if (partIndex > 0)
                {
                    string[] prefixParts = nodes[i - 1].Name.Split(PartSeparator);
                    string prefix = string.Join(".", prefixParts.Take(partIndex));
                    nodes[i].Name = $"{prefix}.{name.TrimStart('.')}";
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

                    if (line.Target.StartsWith(prefix))
                    {
                        var suffix = line.Target.Substring(prefix.Length);
                        line.Target = new string('.', partIndex) + suffix;
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
                int partIndex = line.Target.TakeWhile(c => c == '.').Count();

                if (partIndex > 0)
                {
                    string prefix = string.Join(".", prefixParts.Take(partIndex));
                    line.Target = $"{prefix}.{line.Target.TrimStart('.')}";
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
            node.Lines == null
                ? new List<string>
                {
                    node.Name,
                    node.Bounds,
                    node.Scale.ToString()
                }
                : new List<string>
                {
                    node.Name,
                    node.Bounds,
                    node.Scale.ToString(),
                    ToSaveListLines(node)
                };


        private static JsonSaveTypes.Node ToSaveNode(IReadOnlyList<string> node)
        {
            JsonSaveTypes.Node saveNode = new JsonSaveTypes.Node
            {
                Name = node[0],
                Bounds = node[1],
                Scale = double.Parse(node[2])
            };

            if (node.Count == 4) saveNode.Lines = ToSaveLines(node[3]);

            return saveNode;
        }


        private static List<JsonSaveTypes.Line> ToSaveLines(string linesText)
        {
            string[] linesParts = linesText.Split("|".ToCharArray());
            return linesParts.Select(ToSaveLine).ToList();
        }


        private static JsonSaveTypes.Line ToSaveLine(string lineText)
        {
            string[] lineParts = lineText.Split(",".ToCharArray());
            JsonSaveTypes.Line saveLine = new JsonSaveTypes.Line
            {
                Target = lineParts[0],
                Points = new List<string>()
            };

            for (int i = 1; i < lineParts.Length; i += 2)
            {
                saveLine.Points.Add($"{lineParts[i]},{lineParts[i + 1]}");
            }

            return saveLine;
        }


        private static string ToSaveListLines(JsonSaveTypes.Node node) =>
            string.Join("|", node.Lines.Select(ToSaveListLine));


        private static string ToSaveListLine(JsonSaveTypes.Line line) =>
            $"{line.Target},{ToSaveListPoints(line)}";


        private static string ToSaveListPoints(JsonSaveTypes.Line line) =>
            string.Join(",", line.Points);
    }
}
