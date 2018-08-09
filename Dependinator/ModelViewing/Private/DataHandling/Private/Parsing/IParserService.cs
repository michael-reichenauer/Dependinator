using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing
{
    internal interface IParserService
    {
        Task<R> ParseAsync(DataFile dataFile, DataItemsCallback itemsCallback);


        IReadOnlyList<string> GetDataFilePaths(DataFile dataFile);
        IReadOnlyList<string> GetBuildPaths(DataFile dataFile);
        Task<R<string>> GetCodeAsync(DataFile dataFile, NodeName nodeName);
        Task<R<SourceLocation>> GetSourceFilePath(DataFile dataFile, NodeName nodeName);
        Task<R<NodeName>> GetNodeForFilePathAsync(DataFile dataFile, string sourceFilePath);
    }


    internal class NoAssembliesException : Exception
    {
        public NoAssembliesException(string msg) : base(msg)
        {
        }
    }


    internal class MissingAssembliesException : Exception
    {
        public MissingAssembliesException(string msg) : base(msg)
        {
        }
    }
}
