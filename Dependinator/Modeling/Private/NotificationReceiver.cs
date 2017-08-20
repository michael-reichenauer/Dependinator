﻿using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Modeling.Private.Serializing;


namespace Dependinator.Modeling.Private
{
	internal class NotificationReceiver : MarshalByRefObject
	{
		private readonly ItemsCallback itemsCallback;


		public NotificationReceiver(ItemsCallback itemsCallback)
		{
			this.itemsCallback = itemsCallback;
		}


		public override object InitializeLifetimeService() => null;


		public void ReceiveItems(List<Dtos.Item> dtoItems)
		{
			IReadOnlyList<DataItem> items = dtoItems.Select(Convert.ToDataItem).ToList();

			itemsCallback(items);
		}
	}
}