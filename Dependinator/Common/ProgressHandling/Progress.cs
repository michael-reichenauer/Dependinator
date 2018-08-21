using System;


namespace Dependinator.Common.ProgressHandling
{
    internal class Progress : IDisposable
    {
        public virtual void Dispose()
        {
        }


        public virtual void SetText(string text)
        {
        }
    }
}
