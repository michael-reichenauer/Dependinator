using System;
using System.IO;
using System.Threading.Tasks;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Solutions.Private
{
    internal class SolutionParserService : IParser
    {
        #pragma warning disable 0067
        public event EventHandler DataChanged;
        #pragma warning restore 0067

        public bool CanSupport(string path) => Path.GetExtension(path).IsSameIc(".sln");


        public async Task ParseAsync(
            string path,
            Action<NodeData> nodeCallback, 
            Action<LinkData> linkCallback)
        {
            using (SolutionParser solutionParser = new SolutionParser(path, nodeCallback, linkCallback, false))
            {
                M result = await solutionParser.ParseAsync();

                if (result.IsFaulted)
                {
                    throw new Exception(result.ErrorMessage);
                }
            }
        }


        public async Task<NodeDataSource> GetSourceAsync(string path, string nodeName)
        {
            using (SolutionParser solutionParser = new SolutionParser(path, null, null, true))
            {
                M<NodeDataSource> source = await solutionParser.TryGetSourceAsync(nodeName);

                if (source.IsFaulted)
                {
                    throw new Exception(source.ErrorMessage);
                }

                return source.Value;
            }
        }


        public async Task<string> GetNodeAsync(string path, NodeDataSource source)
        {
            using (SolutionParser solutionParser = new SolutionParser(path, null, null, true))
            {
                M<string> nodeName = await solutionParser.TryGetNodeAsync(source);

                if (nodeName.IsFaulted)
                {
                    throw new Exception(nodeName.ErrorMessage);
                }

                return nodeName.Value;
            }
        }
    }
}
