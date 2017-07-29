using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;


namespace Dependinator.Utils.UI
{
	internal class WhenSetter
	{
		private readonly Notifyable targetNotifyable;
		private readonly string[] sourcePropertyNames;

		private bool isNotifyAll = false;
		private IEnumerable<string> targetPropertyNames;

		public WhenSetter(
			Notifyable targetNotifyable,
			INotifyPropertyChanged sourceNotifyable,
			params string[] sourcePropertyNames)
		{
			this.targetNotifyable = targetNotifyable;
			this.sourcePropertyNames = sourcePropertyNames;

			PropertyChangedEventManager.AddHandler(
				sourceNotifyable, PropertyChangedEventHandler, nameof(sourceNotifyable.PropertyChanged));
		}


		public void Notify(params string[] propertyNames)
		{
			targetPropertyNames = propertyNames;
		}


		public void NotifyAll()
		{
			isNotifyAll = true;
		}


		private void PropertyChangedEventHandler(object sender, PropertyChangedEventArgs e)
		{
			if (!sourcePropertyNames.Any(name => name == e.PropertyName))
			{
				// changed property was not one of the specified source properties 
				return;
			}

			if (isNotifyAll)
			{
				targetNotifyable.NotifyAll();
			}
			else
			{
				targetPropertyNames.ForEach(name => targetNotifyable.Notify(name));
			}
		}
	}
}