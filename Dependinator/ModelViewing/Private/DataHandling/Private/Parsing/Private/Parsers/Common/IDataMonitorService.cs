using System;
using System.Collections.Generic;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Common
{
    internal interface IDataMonitorService
    {
        event EventHandler DataChangedOccurred; 

        void StartMonitorData(string mainPath, IReadOnlyList<string> dataPaths);
    }
}
