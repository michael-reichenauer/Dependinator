namespace Dependinator.ModelViewing.Private.DependencyExploring.Private
{
    internal interface IItemCommands
    {
        void ShowCode(NodeName nodeName);

        void Locate(NodeName nodeName);

        void FilterOn(DependencyItem item, bool isSourceItem);
        void ShowDependencies(NodeName nodeName);
        void HideDependencies(NodeName nodeName, bool isSourceItem);
    }
}
