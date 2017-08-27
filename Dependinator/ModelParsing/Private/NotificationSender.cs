using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelParsing.Private.Serializing;
using Dependinator.Utils;

namespace Dependinator.ModelParsing.Private
{
	internal class NotificationSender
	{
		private readonly NotificationReceiver receiver;
		private readonly BlockingCollection<JsonTypes.Item> items = new BlockingCollection<JsonTypes.Item>();

		private readonly Task sendTask;


		public NotificationSender(NotificationReceiver receiver)
		{
			this.receiver = receiver;

			sendTask = Task.Run(() => Sender());
		}


		public void SendItem(JsonTypes.Item item) => items.Add(item);



		public void Flush()
		{
			items.CompleteAdding();

			// Wait until all items have been sent
			sendTask.Wait();
		}


		private void Sender()
		{
			try
			{
				while (!items.IsCompleted)
				{
					JsonTypes.Item item;
					if (!items.TryTake(out item, int.MaxValue))
					{
						return;
					}

					List<JsonTypes.Item> itemBatch = null;

					AddToBatch(item, ref itemBatch);

					while (items.TryTake(out item))
					{
						AddToBatch(item, ref itemBatch);
					}

					if (itemBatch?.Any() ?? false)
					{
						receiver.ReceiveItems(itemBatch);
					}
				}
			}
			catch (Exception e)
			{
				Log.Warn($"exception {e}");
			}
		}


		private static void AddToBatch(JsonTypes.Item item, ref List<JsonTypes.Item> itemBatch)
		{
			if (itemBatch == null)
			{
				itemBatch = new List<JsonTypes.Item>();
			}

			itemBatch.Add(item);
		}
	}
}
