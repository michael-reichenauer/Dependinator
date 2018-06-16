using System;
using System.Threading.Tasks;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.ModelDataHandling.Private.Parsing
{
	internal interface IParserService
	{
		Task<R> ParseAsync(string filePath, DataItemsCallback dataItemsCallback);
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