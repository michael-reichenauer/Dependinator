using System;
using System.Collections.Generic;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Solutions.Private
{
    internal interface IDataMonitorService
    {
        event EventHandler DataChangedOccurred; 

        void StartMonitorData(string solutionPath, IReadOnlyList<string> dataPaths);
    }
}
