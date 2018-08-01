using System.Threading.Tasks;


namespace Dependinator.ModelViewing.Private.CodeViewing
{
    internal interface ISolutionService
    {
        Task OpenStudioAsync();

        Task OpenFileAsync(string filePath, int lineNumber);
    }
}
