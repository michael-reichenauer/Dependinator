namespace Dependinator.Utils.UI
{
	internal class PropertySetter
	{
		private readonly Notifyable notifyable;


		public PropertySetter(bool isSet, Notifyable notifyable)
		{
			this.notifyable = notifyable;
			IsSet = isSet;
		}


		public bool IsSet { get; }

		public void Notify(params string[] otherPropertyNames)
		{
			if (IsSet)
			{
				notifyable.Notify(otherPropertyNames);
			}
		}

		public void NotifyAll()
		{
			if (IsSet)
			{
				notifyable.NotifyAll();
			}
		}
	}
}