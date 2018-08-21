using System.Threading.Tasks;


namespace Dependinator.ModelViewing.Private.DependencyExploring.Private
{
    internal interface IItemCommands
    {
        Task ShowCodeAsync(NodeName nodeName);

        void Locate(NodeName nodeName);

        void FilterOn(DependencyItem item, bool isSourceItem);
        void ShowDependencyExplorer(NodeName nodeName);
        void HideDependencies(NodeName nodeName, bool isSourceItem);
    }
}
