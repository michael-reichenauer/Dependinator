using System;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Assemblies.Private
{
    internal interface IDataMonitorService
    {
        event EventHandler DataChangedOccurred;

        void StartMonitorData(string solutionPath);
    }
}
