// ReSharper disable once CheckNamespace
namespace System.Windows.Threading
{
	internal static class DispatcherExtensions
	{
		public static void InvokeBackground<T>(this Dispatcher dispatcher, Action<T> action, T arg) =>
			dispatcher.Invoke(DispatcherPriority.Background, action, arg);
	}
}