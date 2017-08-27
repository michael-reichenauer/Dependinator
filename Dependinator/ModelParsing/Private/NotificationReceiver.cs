using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelParsing.Private.Serializing;

namespace Dependinator.ModelParsing.Private
{
	internal class NotificationReceiver : MarshalByRefObject
	{
		private readonly ModelItemsCallback modelItemsCallback;


		public NotificationReceiver(ModelItemsCallback modelItemsCallback)
		{
			this.modelItemsCallback = modelItemsCallback;
		}


		public override object InitializeLifetimeService() => null;


		public void ReceiveItems(List<JsonTypes.Item> dtoItems)
		{
			IReadOnlyList<ModelItem> items = dtoItems.Select(Convert.ToModelItem).ToList();

			modelItemsCallback(items);
		}
	}
}