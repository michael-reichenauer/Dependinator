﻿using System;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelParsing
{
	internal interface IParserService
	{
		Task<R> ParseAsync(string filePath, ModelItemsCallback modelItemsCallback);
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