using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.ModelViewing.Private.DataHandling;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
    internal class CodeViewService : ICodeViewService
    {
        private readonly Func<NodeName, Func<NodeName, Task<M<SourceCode>>>, CodeDialog> codeDialogProvider;
        private readonly IDataService dataService;
        private readonly ModelMetadata modelMetadata;


        public CodeViewService(
            IDataService dataService,
            Func<NodeName, Func<NodeName, Task<M<SourceCode>>>, CodeDialog> codeDialogProvider,
            ModelMetadata modelMetadata)
        {
            this.dataService = dataService;
            this.codeDialogProvider = codeDialogProvider;
            this.modelMetadata = modelMetadata;
        }


        public async void ShowCode(NodeName nodeName)
        {
            DataFile dataFile = modelMetadata.DataFile;
            string solutionPath = dataFile.FilePath;

            M<Source> file = await TryGetFilePathAsync(dataFile, nodeName);
            if (file.IsOk)
            {
                string serverName = ApiServerNames.ServerName<IVsExtensionApi>(solutionPath);

                if (ApiIpcClient.IsServerRegistered(serverName))
                {
                    using (ApiIpcClient apiIpcClient = new ApiIpcClient(serverName))
                    {
                        apiIpcClient.Service<IVsExtensionApi>().ShowFile(file.Value.Path, file.Value.LineNumber);
                        apiIpcClient.Service<IVsExtensionApi>().Activate();
                    }
                }
                else
                {
                    // No Visual studio has loaded this solution, lets show the file in "our" code viewer
                    string fileText = File.ReadAllText(file.Value.Path);
                    CodeDialog codeDialog = codeDialogProvider(nodeName, name => GetCode(fileText, file.Value));
                    codeDialog.Show();
                }
            }
            else
            {
                // Could not determine source file path, lets try to decompile th code
                CodeDialog codeDialog = codeDialogProvider(nodeName, name => GetCodeAsync(dataFile, name));
                codeDialog.Show();
            }
        }


        private static Task<M<SourceCode>> GetCode(string fileText, Source source)
        {
            SourceCode sourceCode = new SourceCode(fileText, source.LineNumber, source.Path);
            return Task.FromResult(M.From(sourceCode));
        }


        private async Task<M<SourceCode>> GetCodeAsync(DataFile dataFile, NodeName nodeName)
        {
            M<string> text = await dataService.GetCodeAsync(dataFile, nodeName);
            if (text.IsFaulted)
            {
                return text.Error;
            }

            return new SourceCode(text.Value, 0, null);
        }


        private async Task<M<Source>> TryGetFilePathAsync(DataFile dataFile, NodeName nodeName)
        {
            string solutionPath = modelMetadata.ModelFilePath;
            M<Source> result = await dataService.GetSourceFilePathAsync(dataFile, nodeName);

            if (result.IsOk)
            {
                if (File.Exists(result.Value.Path))
                {
                    return result.Value;
                }
            }
            else
            {
                // Node information did not contain file path info. Try locate file within the solution
                string solutionFolderPath = Path.GetDirectoryName(solutionPath);

                var filePaths = Directory
                    .GetFiles(solutionFolderPath, $"{nodeName.DisplayShortName}.cs", SearchOption.AllDirectories)
                    .ToList();

                if (filePaths.Count == 1)
                {
                    return new Source(filePaths[0], 0);
                }
            }

            return M.NoValue;
        }
    }
}
