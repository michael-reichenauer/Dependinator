using System;
using Dependinator.Common;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Open
{
	internal class HiddenNodeItem : ViewModel
	{
		private readonly NodeName nodeName;
		private readonly Action<NodeName> showNodeAction;

		public HiddenNodeItem(NodeName nodeName, Action<NodeName> showNodeAction)
		{
			this.nodeName = nodeName;
			this.showNodeAction = showNodeAction;
		}


		public string Name => nodeName.DisplayFullNoParametersName;

		public string ToolTip => "Show hidden node " + nodeName.DisplayFullName;

		public Command ShowNodeCommand => Command(() => showNodeAction(nodeName));
	}
}