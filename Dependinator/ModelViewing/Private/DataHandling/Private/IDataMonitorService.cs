using System;


namespace Dependinator.ModelViewing.Private.DataHandling
{
    internal interface IDataMonitorService
    {
        event EventHandler DataChangedOccurred; 

        void StartMonitorData(string filePath);
        void StopMonitorData();
    }
}
