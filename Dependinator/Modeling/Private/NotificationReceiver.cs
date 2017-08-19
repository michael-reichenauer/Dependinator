using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Modeling.Private.Serializing;


namespace Dependinator.Modeling.Private
{
	internal class NotificationReceiver : MarshalByRefObject
	{
		private readonly IModelNotifications modelNotifications;


		public NotificationReceiver(IModelNotifications modelNotifications)
		{
			this.modelNotifications = modelNotifications;
		}


		public override object InitializeLifetimeService() => null;


		public void ReceiveItems(List<Dtos.Item> dtoItems)
		{
			List<DataItem> items = dtoItems.Select(Convert.ToDataItem).ToList();

			modelNotifications.UpdateDataItems(items);
		}
	}
}