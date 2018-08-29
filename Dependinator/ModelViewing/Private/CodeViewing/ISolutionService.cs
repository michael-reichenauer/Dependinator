using System.Threading.Tasks;
using Dependinator.Common;


namespace Dependinator.ModelViewing.Private.CodeViewing
{
    internal interface ISolutionService
    {
        Task OpenModelAsync(ModelPaths modelPaths);

        Task OpenFileAsync(ModelPaths modelPaths, string filePath, int lineNumber);
    }
}
