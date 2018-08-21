using System;
using System.Windows;


namespace Dependinator.Common.ProgressHandling
{
    internal interface IProgressService
    {
        Progress ShowDialog(string text = "", Window owner = null);

        void SetText(string text);
        IDisposable ShowBusy();
    }
}
