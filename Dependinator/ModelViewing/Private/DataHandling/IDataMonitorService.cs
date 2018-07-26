using System;


namespace Dependinator.ModelViewing.Private.DataHandling
{
    internal interface IDataMonitorService
    {
        event EventHandler ChangedOccurred;

        void Start(string filePath);
        void Stop();
    }
}
