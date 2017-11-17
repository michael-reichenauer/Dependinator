using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.ModelParsing;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Open;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelService : IModelService
	{
		private static readonly int MaxPriority = 10;
		private static readonly int BatchSize = 100;

		private readonly IParserService parserService;
		private readonly IModelNodeService modelNodeService;
		private readonly IModelLinkService modelLinkService;
		private readonly IRecentModelsService recentModelsService;
		private readonly IMessage message;
		private readonly Func<OpenModelViewModel> openModelViewModelProvider;

		private readonly Model model;
		private readonly ModelMetadata modelMetadata;

		private int currentId;
		private bool isShowingOpenModel = false;


		public ModelService(
			IParserService parserService,
			IModelNodeService modelNodeService,
			IModelLinkService modelLinkService,
			Func<OpenModelViewModel> openModelViewModelProvider,
			Model model,
			ModelMetadata modelMetadata,
			IRecentModelsService recentModelsService,
			IMessage message)
		{
			this.parserService = parserService;
			this.modelNodeService = modelNodeService;
			this.modelLinkService = modelLinkService;
			this.openModelViewModelProvider = openModelViewModelProvider;

			this.model = model;
			this.modelMetadata = modelMetadata;
			this.recentModelsService = recentModelsService;
			this.message = message;
		}


		public Node Root => model.Root;

		public void SetRootCanvas(ItemsCanvas rootCanvas) => Root.ItemsCanvas = rootCanvas;


		public async Task LoadAsync()
		{
			Log.Debug($"Metadata model: {modelMetadata.ModelFilePath} {DateTime.Now}");
			string dataFilePath = GetDataFilePath();

			ClearAll();
			Root.ItemsCanvas.IsZoomAndMoveEnabled = true;

			if (File.Exists(dataFilePath))
			{
				await ShowModelAsync(operation => parserService.TryDeserialize(
					dataFilePath, items => UpdateDataItems(items, operation)));
			}
			else
			if (File.Exists(modelMetadata.ModelFilePath))
			{
				await ShowModelAsync(operation => parserService.ParseAsync(
					modelMetadata.ModelFilePath, items => UpdateDataItems(items, operation)));
			}

			if (!Root.Children.Any())
			{
				if (!modelMetadata.IsDefault)
				{
					message.ShowWarning($"Could not load model from:\n{modelMetadata.ModelFilePath}");
				}

				if (File.Exists(dataFilePath))
				{
					File.Delete(dataFilePath);
				}

				isShowingOpenModel = true;
				modelMetadata.SetDefault();
				Root.ItemsCanvas.SetRootScale(1);
				Root.ItemsCanvas.SetOffset(new Point(0, 0));
				Root.ItemsCanvas.IsZoomAndMoveEnabled = false;

				Root.ItemsCanvas.AddItem(openModelViewModelProvider());
			}
			else
			{
				isShowingOpenModel = false;
				Root.ItemsCanvas.IsZoomAndMoveEnabled = true;
				UpdateLines(Root);
				recentModelsService.AddModelPaths(modelMetadata.ModelFilePath);
			}

			GC.Collect();
		}


		private static void UpdateLines(Node node)
		{
			node.SourceLines
				.Where(line => line.IsShowing)
				.ForEach(line => line.ViewModel.NotifyAll());

			node.Children
				.Where(child => child.IsShowing)
				.ForEach(UpdateLines);
		}



		public void ClearAll() => modelNodeService.RemoveAll();


		public async Task SaveAsync()
		{
			if (isShowingOpenModel)
			{
				// Nothing to save
				return;
			}

			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = Root.DescendentsBreadth().ToList();
			t.Log($"Saving {nodes} nodes");

			IReadOnlyList<IModelItem> items = Convert.ToDataItems(nodes);
			t.Log($"Saving {items} items");

			string dataFilePath = GetDataFilePath();
			await parserService.SerializeAsync(items, dataFilePath);
			t.Log($"Saved {items} items");
		}


		public void Save()
		{

			if (isShowingOpenModel)
			{
				// Nothing to save
				return;
			}

			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = Root.DescendentsBreadth().ToList();
			t.Log($"Saving {nodes.Count} nodes");

			IReadOnlyList<IModelItem> items = Convert.ToDataItems(nodes);
			t.Log($"Saving {items.Count} items");

			string dataFilePath = GetDataFilePath();

			parserService.Serialize(items, dataFilePath);
			t.Log($"Saved {items.Count} items");
		}


		public async Task RefreshAsync(bool isClean)
		{
			if (isClean)
			{
				string dataFilePath = GetDataFilePath();

				if (File.Exists(dataFilePath))
				{
					File.Delete(dataFilePath);
				}

				await LoadAsync();
				return;
			}


			int operationId = await ShowModelAsync(operation => parserService.ParseAsync(
				modelMetadata.ModelFilePath, items => UpdateDataItems(items, operation)));

			modelNodeService.RemoveObsoleteNodesAndLinks(operationId);

			GC.Collect();
		}


		private async Task<int> ShowModelAsync(Func<Operation, Task> parseFunctionAsync)
		{
			Operation operation = new Operation(currentId++);

			Timing t = Timing.Start();

			Task showTask = Task.Run(() => ShowModel(operation));

			Task parseTask = parseFunctionAsync(operation)
				.ContinueWith(_ => operation.Queue.CompleteAdding());

			await Task.WhenAll(showTask, parseTask);

			t.Log("Shown all");
			return operation.Id;
		}


		private static void UpdateDataItems(IModelItem item, Operation operation)
		{
			//int priority = GetPriority(item, operation);
			int priority = 0;

			operation.Queue.Enqueue(item, priority);
		}


		private void ShowModel(Operation operation)
		{
			while (operation.Queue.TryTake(out IModelItem item, -1))
			{
				Application.Current.Dispatcher.InvokeBackground(() =>
				{
					UpdateItem(item, operation.Id);

					for (int i = 0; i < BatchSize; i++)
					{
						if (!operation.Queue.TryTake(out item, 0))
						{
							break;
						}

						UpdateItem(item, operation.Id);
					}
				});
			}
		}


		private void UpdateItem(IModelItem item, int stamp)
		{
			if (item is ModelNode modelNode)
			{
				modelNodeService.UpdateNode(modelNode, stamp);
			}

			if (item is ModelLine modelLine)
			{
				modelLinkService.UpdateLine(modelLine, stamp);
			}

			if (item is ModelLink modelLink)
			{
				modelLinkService.UpdateLink(modelLink, stamp);
			}
		}


		//private static int GetPriority(IModelItem item, Operation operation)
		//{
		//	if (item is ModelNode modelNode)
		//	{
		//		return operation.GetPriority(modelNode.Name);
		//	}
		//	else if (item is ModelLine modelLine)
		//	{
		//		return Math.Max(
		//			operation.GetPriority(modelLine.Source),
		//			operation.GetPriority(modelLine.Target));
		//	}
		//	else if (item is ModelLink modelLink)
		//	{
		//		return Math.Max(
		//			operation.GetPriority(modelLink.Source),
		//			operation.GetPriority(modelLink.Target));
		//	}

		//	return MaxPriority - 1;
		//}


		private string GetDataFilePath()
		{
			string dataJson = $"{Path.GetFileName(modelMetadata.ModelFilePath)}.dn.json";
			string dataFilePath = Path.Combine(modelMetadata, dataJson);
			return dataFilePath;
		}


		private class Operation
		{
			public PriorityBlockingQueue<IModelItem> Queue { get; } =
				new PriorityBlockingQueue<IModelItem>(MaxPriority);

			public int Id { get; }

			public Operation(int stamp) => Id = stamp;


			//public int GetPriority(string name)
			//{
			//	int priority = 0;

			//	return priority;

			//	//foreach (char t in name)
			//	//{
			//	//	if (t == '(')
			//	//	{
			//	//		break;
			//	//	}

			//	//	if (t == '.')
			//	//	{
			//	//		priority++;
			//	//	}

			//	//	if (priority >= MaxPriority - 1)
			//	//	{
			//	//		break;
			//	//	}
			//	//}

			//	//return priority;
			//}
		}
	}
}