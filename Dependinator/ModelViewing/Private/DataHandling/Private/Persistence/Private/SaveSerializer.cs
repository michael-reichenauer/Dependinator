﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.JsonTypes;
using Dependinator.Utils;
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
                    List<JsonSaveTypes.Node> nodes = ToJsonNodes(items);

                    Dictionary<string, List<JsonSaveTypes.Line>> lines = ToJsonLines(items);

                    AddLinesToNodes(nodes, lines);

                    ShortenNodeNames(nodes);

                    JsonSaveTypes.Model dataModel = new JsonSaveTypes.Model { Nodes = nodes };
                    Serialize(path, dataModel);
                }
                catch (Exception e)
                {
                    Log.Exception(e, $"Failed to serialize to {path}");
                }
            });
        }


        private static List<JsonSaveTypes.Node> ToJsonNodes(IEnumerable<IDataItem> items)
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
            IEnumerable<JsonSaveTypes.Node> nodes,
            IReadOnlyDictionary<string, List<JsonSaveTypes.Line>> lines)
        {
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
            for (int j = nodes.Count - 1; j > 0; j--)
            {
                string[] prefixParts = nodes[j - 1].Name.Split(PartSeparator);

                for (int partIndex = prefixParts.Length; partIndex >= 0; partIndex--)
                {
                    string prefix = string.Join(".", prefixParts.Take(partIndex)) + ".";

                    if (nodes[j].Name.StartsWith(prefix))
                    {
                        var suffix = nodes[j].Name.Substring(prefix.Length);
                        nodes[j].Name = new string('.', partIndex) + suffix;
                        break;
                    }
                }
            }
        }

        private static void ShortenLineNames(string sourceName, IReadOnlyList<JsonSaveTypes.Line> lines)
        {
            string[] prefixParts = sourceName.Split(PartSeparator);

            for (int j = lines.Count - 1; j >= 0; j--)
            {
                for (int partIndex = prefixParts.Length; partIndex >= 0; partIndex--)
                {
                    string prefix = string.Join(".", prefixParts.Take(partIndex)) + ".";

                    if (lines[j].Target.StartsWith(prefix))
                    {
                        var suffix = lines[j].Target.Substring(prefix.Length);
                        lines[j].Target = new string('.', partIndex) + suffix;
                        break;
                    }
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
    }
}
