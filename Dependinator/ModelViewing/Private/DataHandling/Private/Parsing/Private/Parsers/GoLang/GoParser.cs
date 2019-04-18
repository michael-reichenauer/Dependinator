using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.GoLang
{
    internal class GoParser : IParser
    {
        public event EventHandler DataChanged;

        public bool CanSupport(string path)
        {
            return Directory
                .EnumerateFiles(path, "*.go", SearchOption.AllDirectories)
                .Any();
        }


        public void StartMonitorDataChanges(string path)
        {
            // Skip monitoring for now
        }


        public Task ParseAsync(string path, Action<NodeData> nodeCallback, Action<LinkData> linkCallback)
        {
            throw new NotImplementedException();
        }

        public Task<NodeDataSource> GetSourceAsync(string path, string nodeName) =>
            Task.FromResult((NodeDataSource)null);


        public Task<string> GetNodeAsync(string path, NodeDataSource source) =>
            Task.FromResult((string)null);

        public DateTime GetDataTime(string path) => DateTime.MinValue;
    }
}
