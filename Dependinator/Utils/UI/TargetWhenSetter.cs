using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Dependinator.Utils.UI
{
	internal class TargetWhenSetter
	{
		private readonly Notifyable targetNotifyable;
		private readonly string[] sourcePropertyNames;

		private Action<string> notifyPropertyAction;
		private bool isNotifyAll = false;
		private IEnumerable<string> targetPropertyNames;

		public TargetWhenSetter(
			Notifyable targetNotifyable,
			INotifyPropertyChanged sourceNotifyable,
			params string[] sourcePropertyNames)
		{
			this.targetNotifyable = targetNotifyable;
			this.sourcePropertyNames = sourcePropertyNames;

			sourcePropertyNames.ForEach(
				propertyName => PropertyChangedEventManager.AddHandler(
					sourceNotifyable, PropertyChangedEventHandler, propertyName));
		}


		public void Notify(params string[] propertyNames) => targetPropertyNames = propertyNames;

		public void NotifyAll() => isNotifyAll = true;

		public void Notify(Action<string> notifyAction) => notifyPropertyAction = notifyAction;


		private void PropertyChangedEventHandler(object sender, PropertyChangedEventArgs e)
		{
			if (!sourcePropertyNames.Any(name => name == e.PropertyName))
			{
				// changed property was not one of the specified source properties 
				return;
			}

			if (notifyPropertyAction != null)
			{
				notifyPropertyAction(e.PropertyName);
			}
			else if (isNotifyAll)
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