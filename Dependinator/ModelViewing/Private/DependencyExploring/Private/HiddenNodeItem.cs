using System;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Private.DependencyExploring.Private
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


        public string Name => nodeName.DisplayLongName;

        public Command ShowNodeCommand => Command(() => showNodeAction(nodeName));
    }
}
