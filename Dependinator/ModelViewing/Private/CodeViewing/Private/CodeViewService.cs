using System;
using System.IO;
using System.Threading.Tasks;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ProgressHandling;
using Dependinator.ModelViewing.Private.DataHandling;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
    internal class CodeViewService : ICodeViewService
    {
        private readonly Func<NodeName, Source, Func<Task<M<Source>>>, CodeDialog> codeDialogProvider;
        private readonly IDataService dataService;
        private readonly IProgressService progressService;
        private readonly IMessage message;
        private readonly ModelMetadata modelMetadata;


        public CodeViewService(
            IDataService dataService,
            IProgressService progressService,
            IMessage message,
            Func<NodeName, Source, Func<Task<M<Source>>>, CodeDialog> codeDialogProvider,
            ModelMetadata modelMetadata)
        {
            this.dataService = dataService;
            this.progressService = progressService;
            this.message = message;
            this.codeDialogProvider = codeDialogProvider;
            this.modelMetadata = modelMetadata;
        }


        public async Task ShowCodeAsync(NodeName nodeName)
        {
            Source source = null;
            DataFile dataFile = modelMetadata.DataFile;

            using (progressService.ShowDialog("Getting source code.."))
            {
                M<Source> result = await dataService.TryGetSourceAsync(dataFile, nodeName);

                if (!result.HasValue(out source))
                {
                    message.ShowWarning($"Error while showing code for:\n{nodeName}\n\n{result.ErrorMessage}");
                    return;
                }


                if (source.Path != null && File.Exists(source.Path))
                {
                    if (TryOpenInVisualStudio(dataFile, source))
                    {
                        return;
                    }

                    // Lets show the file in "our" code viewer
                    string fileText = File.ReadAllText(source.Path);
                    source = new Source(source.Path, fileText, source.LineNumber);
                }
            }


            CodeDialog codeDialog = codeDialogProvider(
                nodeName, source, () => dataService.TryGetSourceAsync(dataFile, nodeName));
            codeDialog.Show();
        }


        private static bool TryOpenInVisualStudio(DataFile dataFile, Source source)
        {
            string solutionPath = dataFile.FilePath;
            string serverName = ApiServerNames.ServerName<IVsExtensionApi>(solutionPath);

            if (!ApiIpcClient.IsServerRegistered(serverName)) return false;

            using (ApiIpcClient apiIpcClient = new ApiIpcClient(serverName))
            {
                apiIpcClient.Service<IVsExtensionApi>().ShowFile(source.Path, source.LineNumber);
                apiIpcClient.Service<IVsExtensionApi>().Activate();
            }

            return true;
        }
    }
}
