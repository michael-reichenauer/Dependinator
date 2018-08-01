using System;


namespace Dependinator.Common.ProgressHandling
{
    internal interface IProgressService
    {
        Progress ShowDialog(string text = "");

        void SetText(string text);
        IDisposable ShowBusy();
    }
}
