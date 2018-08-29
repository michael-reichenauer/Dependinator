using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Common;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Threading;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private
{
    [SingleInstance]
    internal class ParserService : IParserService
    {
        private readonly IEnumerable<IParser> parsers;


        public ParserService(IEnumerable<IParser> parsers)
        {
            this.parsers = parsers;

            parsers.ForEach(parser => parser.DataChanged += (s, e) => DataChanged?.Invoke(this, e));
        }


        public event EventHandler DataChanged;


        public void StartMonitorDataChanges(ModelPaths modelPaths)
        {
            if (TryGetParser(modelPaths, out IParser parser))
            {
                parser.StartMonitorDataChanges(modelPaths.ModelPath);
            }
        }


        public DateTime GetDataTime(ModelPaths modelPaths)
        {
            if (!TryGetParser(modelPaths, out IParser parser))
            {
                return DateTime.MinValue;
            }

            return parser.GetDataTime(modelPaths.ModelPath);
        }


        public async Task<M> ParseAsync(ModelPaths modelPaths, Action<IDataItem> itemsCallback)
        {
            Log.Debug($"Parse {modelPaths} ...");
            Timing t = Timing.Start();

            if (!TryGetParser(modelPaths, out IParser parser))
            {
                return Error.From($"File not supported: {modelPaths}");
            }

            void NodeCallback(NodeData nodeData) => itemsCallback(ToDataItem(nodeData));
            void LinkCallback(LinkData linkData) => itemsCallback(ToDataItem(linkData));

            try
            {
                await parser.ParseAsync(modelPaths.ModelPath, NodeCallback, LinkCallback);
                t.Log($"Parsed {modelPaths}");
                return M.Ok;
            }
            catch (Exception e)
            {
                return Error.From(e);
            }
        }


        public async Task<M<Source>> GetSourceAsync(ModelPaths modelPaths, DataNodeName nodeName)
        {
            Log.Debug($"Get source for {nodeName} in model {modelPaths}...");
            try
            {
                if (!TryGetParser(modelPaths, out IParser parser))
                {
                    return Error.From($"File not supported: {modelPaths}");
                }

                NodeDataSource source = await parser.GetSourceAsync(modelPaths.ModelPath, (string)nodeName);
                if (source == null)
                {
                    return M.NoValue;
                }

                return new Source(source.Path, source.Text, source.LineNumber);
            }
            catch (Exception e)
            {
                return Error.From(e);
            }

        }


        public async Task<M<DataNodeName>> TryGetNodeAsync(ModelPaths modelPaths, Source source)
        {
            Log.Debug($"Get node for {source} in model {modelPaths}...");

            try
            {
                if (!TryGetParser(modelPaths, out IParser parser))
                {
                    return Error.From($"File not supported: {modelPaths}");
                }

                NodeDataSource nodeSource = new NodeDataSource(source.Text, source.LineNumber, source.Path);

                string nodeName = await parser.GetNodeAsync(modelPaths.ModelPath, nodeSource);
                if (nodeName == null)
                {
                    return M.NoValue;
                }

                return (DataNodeName)nodeName;
            }
            catch (Exception e)
            {
                return Error.From(e);
            }
        }



        private bool TryGetParser(ModelPaths modelPaths, out IParser parser)
        {
            parser = parsers.FirstOrDefault(p => p.CanSupport(modelPaths.ModelPath));
            if (parser == null) Log.Warn($"No supported parser for {modelPaths}");

            return parser != null;
        }


        private static IDataItem ToDataItem(NodeData node) => new DataNode(
            (DataNodeName)node.Name,
            node.Parent != null ? (DataNodeName)node.Parent : null,
            ToNodeType(node.Type))
            { Description = node.Description };


        private static IDataItem ToDataItem(LinkData link) => new DataLink(
            (DataNodeName)link.Source,
            (DataNodeName)link.Target,
            ToNodeType(link.TargetType));



        private static NodeType ToNodeType(string nodeType)
        {
            switch (nodeType)
            {
                case null:
                    return NodeType.None;
                case NodeData.SolutionType:
                    return NodeType.Solution;
                case NodeData.SolutionFolderType:
                    return NodeType.SolutionFolder;
                case NodeData.AssemblyType:
                    return NodeType.Assembly;
                case NodeData.GroupType:
                    return NodeType.Group;
                case NodeData.DllType:
                    return NodeType.Dll;
                case NodeData.ExeType:
                    return NodeType.Exe;
                case NodeData.NameSpaceType:
                    return NodeType.NameSpace;
                case NodeData.TypeType:
                    return NodeType.Type;
                case NodeData.MemberType:
                    return NodeType.Member;
                default:
                    throw Asserter.FailFast($"Unexpected type {nodeType}");
            }
        }
    }
}
