using System;


namespace Dependinator.ModelViewing.DataHandling
{
	internal interface IDataMonitorService
	{
		event EventHandler ChangedOccurred;

		void Start(string filePath);
		void Stop();
	}
}