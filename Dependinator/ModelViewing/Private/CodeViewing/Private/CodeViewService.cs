using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.ModelViewing.Private.DataHandling;
using Dependinator.ModelViewing.Private.ModelHandling.Private;
using Dependinator.Utils.ErrorHandling;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
	internal class CodeViewService : ICodeViewService
	{
		private readonly IDataDetailsService dataDetailsService;

		private readonly Func<NodeName, Func<NodeName, Task<R<string>>>, CodeDialog> codeDialogProvider;
		private readonly ModelMetadata modelMetadata;


		public CodeViewService(
			IDataDetailsService dataDetailsService,
			Func<NodeName, Func<NodeName, Task<R<string>>>, CodeDialog> codeDialogProvider,
			ModelMetadata modelMetadata)
		{
			this.dataDetailsService = dataDetailsService;
			this.codeDialogProvider = codeDialogProvider;
			this.modelMetadata = modelMetadata;
		}


		public async void ShowCode(NodeName nodeName)
		{
			string solutionPath = modelMetadata.ModelFilePath;

			R<string> filePath = await TryGetFilePathAsync(nodeName);
			if (filePath.IsOk)
			{
				string serverName = ApiServerNames.ExtensionApiServerName(solutionPath);

				if (ApiIpcClient.IsServerRegistered(serverName))
				{
					using (ApiIpcClient apiIpcClient = new ApiIpcClient(serverName))
					{
						apiIpcClient.Service<IVsExtensionApi>().ShowFile(filePath.Value, 250);
					}
				}
				else
				{
					// No Visual studio has loaded this solution, lets show the file in "our" code viewer
					string fileText = File.ReadAllText(filePath.Value);
					CodeDialog codeDialog = codeDialogProvider(
						nodeName, n => Task.FromResult(R.From(fileText)));
					codeDialog.Show();
				}
			}
			else
			{
				// Could not determine source file path, lets try to decompile th code
				CodeDialog codeDialog = codeDialogProvider(
					nodeName, n => dataDetailsService.GetCodeAsync(solutionPath, n));
				codeDialog.Show();
			}
		}


		private async Task<R<string>> TryGetFilePathAsync(NodeName nodeName)
		{
			string solutionPath = modelMetadata.ModelFilePath;
			R<string> result = await dataDetailsService.GetSourceFilePathAsync(solutionPath, nodeName);

			if (result.IsOk)
			{
				if (File.Exists(result.Value))
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
					return filePaths[0];
				}
			}

			return R<string>.NoValue;
		}
	}
}