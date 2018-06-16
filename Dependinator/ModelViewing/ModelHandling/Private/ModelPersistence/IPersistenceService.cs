using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelDataHandling;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelPersistence
{
	internal interface IPersistenceService
	{
		void Serialize(IReadOnlyList<IModelItem> items, string path);

		Task<R> TryDeserialize(string path, ModelItemsCallback modelItemsCallback);
	}


	internal class MissingDataFileException : Exception
	{
		public MissingDataFileException(string msg) : base(msg)
		{
		}
	}

	internal class InvalidDataFileException : Exception
	{
		public InvalidDataFileException(string msg, Exception inner = null) : base(msg, inner)
		{
		}
	}
}