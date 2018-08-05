using System;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;


namespace Dependinator.ModelViewing.Private.DataHandling.Private
{
    internal interface IDataMonitorService
    {
        event EventHandler DataChangedOccurred; 

        void StartMonitorData(DataFile dataFile);
    }
}
