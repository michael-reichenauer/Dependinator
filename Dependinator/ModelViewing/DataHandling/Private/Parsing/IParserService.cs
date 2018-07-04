﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing
{
	internal interface IParserService
	{
		Task<R> ParseAsync(string filePath, DataItemsCallback itemsCallback);

		Task<R<string>> GetCodeAsync(string filePath, NodeName nodeName);

		IReadOnlyList<string> GetDataFilePaths(string filePath);
		IReadOnlyList<string> GetBuildPaths(string filePath);
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