using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.ModelViewing.Private.DataHandling;
using Dependinator.Utils.ErrorHandling;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
	internal class CodeViewService : ICodeViewService
	{
		private readonly IDataDetailsService dataDetailsService;

		private readonly Func<NodeName, Func<NodeName, Task<R<SourceCode>>>, CodeDialog> codeDialogProvider;
		private readonly ModelMetadata modelMetadata;


		public CodeViewService(
			IDataDetailsService dataDetailsService,
			Func<NodeName, Func<NodeName, Task<R<SourceCode>>>, CodeDialog> codeDialogProvider,
			ModelMetadata modelMetadata)
		{
			this.dataDetailsService = dataDetailsService;
			this.codeDialogProvider = codeDialogProvider;
			this.modelMetadata = modelMetadata;
		}


		public async void ShowCode(NodeName nodeName)
		{
			string solutionPath = modelMetadata.ModelFilePath;

			R<SourceLocation> file = await TryGetFilePathAsync(nodeName);
			if (file.IsOk)
			{
				string serverName = ApiServerNames.ServerName<IVsExtensionApi>(solutionPath);

				if (ApiIpcClient.IsServerRegistered(serverName))
				{
					using (ApiIpcClient apiIpcClient = new ApiIpcClient(serverName))
					{
						apiIpcClient.Service<IVsExtensionApi>().ShowFile(file.Value.FilePath, file.Value.LineNumber);
						apiIpcClient.Service<IVsExtensionApi>().Activate();
					}
				}
				else
				{
					// No Visual studio has loaded this solution, lets show the file in "our" code viewer
					string fileText = File.ReadAllText(file.Value.FilePath);
					CodeDialog codeDialog = codeDialogProvider(nodeName, name => GetCode(fileText, file.Value));
					codeDialog.Show();
				}
			}
			else
			{
				// Could not determine source file path, lets try to decompile th code
				CodeDialog codeDialog = codeDialogProvider(nodeName, name => GetCodeAsync(solutionPath, name));
				codeDialog.Show();
			}
		}


		private static Task<R<SourceCode>> GetCode(string fileText, SourceLocation sourceLocation)
		{
			SourceCode sourceCode = new SourceCode(fileText, sourceLocation.LineNumber, sourceLocation.FilePath);
			return Task.FromResult(R.From(sourceCode));
		}


		private async Task<R<SourceCode>> GetCodeAsync(string solutionPath, NodeName nodeName)
		{
			R<string> text = await dataDetailsService.GetCodeAsync(solutionPath, nodeName);
			if (text.IsFaulted)
			{
				return text.Error;
			}

			return new SourceCode(text.Value, 0, null);
		}


		private async Task<R<SourceLocation>> TryGetFilePathAsync(NodeName nodeName)
		{
			string solutionPath = modelMetadata.ModelFilePath;
			R<SourceLocation> result = await dataDetailsService.GetSourceFilePathAsync(solutionPath, nodeName);

			if (result.IsOk)
			{
				if (File.Exists(result.Value.FilePath))
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
					return new SourceLocation(filePaths[0], 0);
				}
			}

			return R<SourceLocation>.NoValue;
		}
	}
}