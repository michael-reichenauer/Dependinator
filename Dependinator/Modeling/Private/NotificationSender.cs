using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Modeling.Private.Serializing;
using Dependinator.Utils;


namespace Dependinator.Modeling.Private
{
	internal class NotificationSender
	{
		private readonly NotificationReceiver receiver;
		private readonly BlockingCollection<Dtos.Item> items = new BlockingCollection<Dtos.Item>();

		private readonly Task sendTask;


		public NotificationSender(NotificationReceiver receiver)
		{
			this.receiver = receiver;

			sendTask = Task.Run(() => Sender());
		}


		public void SendItem(Dtos.Item item) => items.Add(item);



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
					Dtos.Item item;
					if (!items.TryTake(out item, int.MaxValue))
					{
						return;
					}

					List<Dtos.Item> itemBatch = null;

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


		private static void AddToBatch(Dtos.Item item, ref List<Dtos.Item> itemBatch)
		{
			if (itemBatch == null)
			{
				itemBatch = new List<Dtos.Item>();
			}

			itemBatch.Add(item);
		}
	}
}
