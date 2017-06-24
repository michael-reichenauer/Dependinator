using System;


namespace Dependinator.Common.ProgressHandling
{
	internal class Progress : IDisposable
	{
		public virtual void SetText(string text)
		{		
		}

		public virtual void Dispose()
		{
		}
	}
}