using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.DataHandling
{
	internal interface IDataService
	{
		Task<R> TryReadSavedDataAsync(string dataFilePath, DataItemsCallback dataItemsCallback);

		void SaveData(IReadOnlyList<IDataItem> items, string dataFilePath);

		Task<R> ParseAsync(string filePath, DataItemsCallback dataItemsCallback);
	}
}