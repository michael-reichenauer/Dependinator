using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.DataHandling
{
	internal interface IDataService
	{
		Task<R> TryDeserialize(string path, DataItemsCallback dataItemsCallback);
		Task<R> ParseAsync(string filePath, DataItemsCallback dataItemsCallback);
		void Serialize(IReadOnlyList<IDataItem> items, string path);
	}
}