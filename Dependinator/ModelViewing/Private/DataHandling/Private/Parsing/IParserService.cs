using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing
{
	internal interface IParserService
	{
		Task<R> ParseAsync(string filePath, DataItemsCallback itemsCallback);


		IReadOnlyList<string> GetDataFilePaths(string filePath);
		IReadOnlyList<string> GetBuildPaths(string filePath);
		Task<R<string>> GetCodeAsync(string filePath, NodeName nodeName);
		Task<R<string>> GetSourceFilePath(string filePath, NodeName nodeName);
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