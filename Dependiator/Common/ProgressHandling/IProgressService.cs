using System;


namespace Dependiator.Common.ProgressHandling
{
	internal interface IProgressService
	{
		Progress ShowDialog(string text = "");

		void SetText(string text);
		IDisposable ShowBusy();
	}
}