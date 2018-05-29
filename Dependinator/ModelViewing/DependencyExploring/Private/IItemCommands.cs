using System;
using System.Threading.Tasks;
using Dependinator.Common;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal interface IItemCommands
	{
		void ShowCode(string title, Lazy<string> codeText);
		void FilterOn(DependencyItem item, bool isSourceItem);
	}
}