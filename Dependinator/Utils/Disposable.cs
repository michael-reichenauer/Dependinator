using System;


namespace Dependinator.Utils
{
	internal class Disposable : IDisposable
	{
		private readonly Action disposeAction;

		public Disposable(Action disposeAction)
		{
			this.disposeAction = disposeAction;
		}

		public void Dispose()
		{
			disposeAction();
		}
	}
}