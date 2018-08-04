using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils;
using Dependinator.Utils.Threading;
using Newtonsoft.Json;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.Serializing
{
    internal class SaveSerializer : ISaveSerializer
    {
        private static readonly char[] PartSeparator = ".".ToCharArray();


        public void Serialize(IReadOnlyList<IDataItem> items, string path)
        {
            try
            {
                Timing t = new Timing();

                List<SaveJsonTypes.Node> nodes = ToJsonNodes(items);

                Dictionary<string, List<SaveJsonTypes.Line>> lines = ToJsonLines(items);

                AddLinesToNodes(nodes, lines);

                ShortenNodeNames(nodes);

                SaveJsonTypes.Model dataModel = new SaveJsonTypes.Model { Nodes = nodes };
                Serialize(path, dataModel);

                t.Log("Wrote data file");
            }
            catch (Exception e)
            {
                Log.Exception(e, "Failed to serialize");
            }
        }


        private static List<SaveJsonTypes.Node> ToJsonNodes(IEnumerable<IDataItem> items)
        {
            return items
                .Where(item => item is DataNode)
                .Cast<DataNode>()
                .Select(Convert.ToSaveJsonNode)
                .OrderBy(it => it.Name)
                .ToList();
        }


        private static Dictionary<string, List<SaveJsonTypes.Line>> ToJsonLines(
            IEnumerable<IDataItem> items)
        {
            Dictionary<string, List<SaveJsonTypes.Line>> lines =
                new Dictionary<string, List<SaveJsonTypes.Line>>();

            foreach (DataLine line in items.Where(item => item is DataLine).Cast<DataLine>())
            {
                if (!lines.TryGetValue((string)line.Source, out var nodeLines))
                {
                    nodeLines = new List<SaveJsonTypes.Line>();
                    lines[(string)line.Source] = nodeLines;
                }

                nodeLines.Add(Convert.ToSaveJsonLine(line));
            }

            return lines;
        }


        private static void AddLinesToNodes(
            IEnumerable<SaveJsonTypes.Node> nodes,
            IReadOnlyDictionary<string, List<SaveJsonTypes.Line>> lines)
        {
            foreach (SaveJsonTypes.Node node in nodes)
            {
                if (lines.TryGetValue(node.Name, out var nodeLines))
                {
                    node.Lines = nodeLines.OrderBy(line => line.Target).ToList();
                    ShortenLineNames(node.Name, node.Lines);
                }
            }
        }


        private static void ShortenNodeNames(IReadOnlyList<SaveJsonTypes.Node> nodes)
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

        private static void ShortenLineNames(string sourceName, IReadOnlyList<SaveJsonTypes.Line> lines)
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
