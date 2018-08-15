using System;
using System.IO;
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

            M<Source> source = await dataService.TryGetSourceAsync(dataFile, nodeName);
            if (source.IsOk)
            {
                if (source.Value.Path != null)
                {
                    string serverName = ApiServerNames.ServerName<IVsExtensionApi>(solutionPath);

                    if (ApiIpcClient.IsServerRegistered(serverName))
                    {
                        using (ApiIpcClient apiIpcClient = new ApiIpcClient(serverName))
                        {
                            apiIpcClient.Service<IVsExtensionApi>().ShowFile(source.Value.Path, source.Value.LineNumber);
                            apiIpcClient.Service<IVsExtensionApi>().Activate();
                        }
                    }
                    else
                    {
                        // No Visual studio has loaded this solution, lets show the file in "our" code viewer
                        string fileText = File.ReadAllText(source.Value.Path);
                        CodeDialog codeDialog = codeDialogProvider(nodeName, name => GetCode(fileText, source.Value));
                        codeDialog.Show();
                    }
                }
                else
                {
                    CodeDialog codeDialog = codeDialogProvider(nodeName,
                        name => GetCode(source.Value.Text, source.Value));
                    codeDialog.Show();
                }
            }
            else
            {
                // Could not determine source file path,
                CodeDialog codeDialog = codeDialogProvider(nodeName,
                    name => Task.FromResult((M<SourceCode>)source.Error));
                codeDialog.Show();
            }
        }


        private static Task<M<SourceCode>> GetCode(string fileText, Source source)
        {
            SourceCode sourceCode = new SourceCode(fileText, source.LineNumber, source.Path);
            return Task.FromResult(M.From(sourceCode));
        }
    }
}
