using System;
using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing
{
	internal interface IParserService
	{
		Task<R> ParseAsync(string filePath, DataItemsCallback dataItemsCallback);

		Task<R<string>> GetCodeAsync(string filePath, NodeName nodeName);
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