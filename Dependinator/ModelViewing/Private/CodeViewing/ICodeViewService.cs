using System.Threading.Tasks;


namespace Dependinator.ModelViewing.Private.CodeViewing
{
    internal interface ICodeViewService
    {
        Task ShowCodeAsync(NodeName nodeName);
    }
}
