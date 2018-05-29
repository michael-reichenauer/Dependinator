namespace Dependinator.Utils.UI.Mvvm
{
	internal interface IPropertyNotify
	{
		bool IsSet { get; }

		void Notify(params string[] otherPropertyNames);

		void NotifyAll();
	}
}