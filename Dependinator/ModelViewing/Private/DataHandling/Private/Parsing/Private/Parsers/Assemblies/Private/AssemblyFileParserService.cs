using System;
using System.IO;
using System.Threading.Tasks;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Assemblies.Private
{
    internal class AssemblyFileParserService : IParser
    {
#pragma warning disable 0067
        public event EventHandler DataChanged;
#pragma warning restore 0067

        public bool CanSupport(string path) =>
            Path.GetExtension(path).IsSameIc(".exe") ||
            Path.GetExtension(path).IsSameIc(".dll");


        public async Task ParseAsync(
            string path,
            Action<NodeData> nodeCallback,
            Action<LinkData> linkCallback)
        {
            NodeData fileNode = GetAssemblyFileNode(path);
            nodeCallback(fileNode);

            using (AssemblyParser assemblyParser = new AssemblyParser(
                path, null, fileNode.Name, nodeCallback, linkCallback, false))
            {
                M result = await assemblyParser.ParseAsync();

                if (result.IsFaulted)
                {
                    throw new Exception(result.ErrorMessage);
                }
            }
        }


        public async Task<NodeDataSource> GetSourceAsync(string path, string nodeName)
        {
            using (AssemblyParser assemblyParser = new AssemblyParser(
                path, null, null, null, null, true))
            {
                M<NodeDataSource> result = await Task.Run(() => assemblyParser.TryGetSource(nodeName));

                if (result.IsFaulted)
                {
                    throw new Exception(result.ErrorMessage);
                }

                return result.Value;
            }
        }


        public Task<string> GetNodeAsync(string path, NodeDataSource source)
        {
            throw new NotImplementedException();
        }


        private static NodeData GetAssemblyFileNode(string path)
        {
            string name = Path.GetFileName(path).Replace(".", "*");
            return new NodeData(name, null, NodeData.AssemblyType, "Assembly file");
        }
    }
}
