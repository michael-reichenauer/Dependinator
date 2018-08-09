using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing
{
    internal interface IParserService
    {
        Task<M> ParseAsync(DataFile dataFile, DataItemsCallback itemsCallback);


        IReadOnlyList<string> GetDataFilePaths(DataFile dataFile);
        IReadOnlyList<string> GetBuildPaths(DataFile dataFile);
        Task<M<string>> GetCodeAsync(DataFile dataFile, NodeName nodeName);
        Task<M<SourceLocation>> GetSourceFilePath(DataFile dataFile, NodeName nodeName);
        Task<M<NodeName>> GetNodeForFilePathAsync(DataFile dataFile, string sourceFilePath);
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
