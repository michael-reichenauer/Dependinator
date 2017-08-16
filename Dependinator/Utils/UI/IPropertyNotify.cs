namespace Dependinator.Utils.UI
{
	internal interface IPropertyNotify
	{
		bool IsSet { get; }

		void Notify(params string[] otherPropertyNames);

		void NotifyAll();
	}
}