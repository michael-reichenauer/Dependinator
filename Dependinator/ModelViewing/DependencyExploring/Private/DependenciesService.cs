using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Common;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependenciesService : IDependenciesService
	{
		private readonly IThemeService themeService;
		private readonly IModelService modelService;
		private readonly Lazy<IModelNotifications> modelViewModel;

		private readonly WindowOwner owner;


		public DependenciesService(
			IThemeService themeService,
			IModelService modelService,
			Lazy<IModelNotifications> modelViewModel,
			WindowOwner owner)
		{
			this.themeService = themeService;
			this.modelService = modelService;
			this.modelViewModel = modelViewModel;
			this.owner = owner;
		}


		public async Task<IReadOnlyList<DependencyItem>> GetDependencyItemsAsync(
			IEnumerable<Line> lines, 
			bool isSource,
			Node sourceFilter,
			Node targetFilter)
		{
			return await Task.Run(() =>
			{
				ReferenceOptions options = new ReferenceOptions(isSource, sourceFilter, targetFilter);
				IEnumerable<Link> links = lines
					.SelectMany(line => line.Links)
					.Where(link => IsIncluded(link, options));

				var items = CreateReferenceHierarchy(links, options);

				if (!items.Any())
				{
					return Enumerable.Empty<DependencyItem>().ToList();
				}

				DependencyItem rootItem = items[NodeName.Root];

				return new List<DependencyItem> { rootItem };
			});
		}


		public async Task<int> GetDependencyCountAsync(
			IEnumerable<Line> lines,
			bool isSource,
			Node sourceFilter,
			Node targetFilter)
		{
			return await Task.Run(() =>
			{
				ReferenceOptions options = new ReferenceOptions(isSource, sourceFilter, targetFilter);

				IEnumerable<Link> links = lines
					.SelectMany(line => line.Links)
					.Where(link => IsIncluded(link, options));

				return links.Count();
			});
		}


		public bool TryGetNode(NodeName nodeName, out Node node) => 
			modelService.TryGetNode(nodeName, out node);


		public Task RefreshModelAsync() => modelViewModel.Value.ManualRefreshAsync(false);


		private static bool IsIncluded(IEdge link, ReferenceOptions options) =>
			(options.SourceFilter.IsRoot || link.Source.AncestorsAndSelf().Contains(options.SourceFilter)) &&
			(options.TargetFilter.IsRoot || link.Target.AncestorsAndSelf().Contains(options.TargetFilter)) &&
			!link.Target.AncestorsAndSelf().Any(n => n.View.IsHidden);



		private Dictionary<NodeName, DependencyItem> CreateReferenceHierarchy(
			IEnumerable<Link> links, ReferenceOptions options)
		{
			Dictionary<NodeName, DependencyItem> items = new Dictionary<NodeName, DependencyItem>();

			items[NodeName.Root] = new DependencyItem(
				options.SourceFilter.Root.Name, options.SourceFilter.Root.CodeText);

			foreach (Link link in links)
			{
				Node node = EndPoint(link, options.IsSource);

				if (!items.TryGetValue(node.Name, out DependencyItem item))
				{
					DependencyItem parentItem = GetParentItem(items, node.Parent);

					item = new DependencyItem(node.Name, node.CodeText);
					parentItem.AddChild(item);

					items[node.Name] = item;
				}
			}

			return items;
		}


		private DependencyItem GetParentItem(
			IDictionary<NodeName, DependencyItem> items,
			Node parentNode)
		{
			if (items.TryGetValue(parentNode.Name, out DependencyItem parentItem))
			{
				return parentItem;
			}

			parentItem = new DependencyItem(parentNode.Name, parentNode.CodeText);

			if (!parentNode.IsRoot)
			{
				DependencyItem grandParentItem = GetParentItem(items, parentNode.Parent);
				grandParentItem.AddChild(parentItem);
			}

			items[parentNode.Name] = parentItem;
			return parentItem;
		}


		private static Node EndPoint(IEdge edge, bool isSource)
		{
			return isSource ? edge.Source : edge.Target;
		}


		private class ReferenceOptions
		{
			public bool IsSource { get; }
			public Node SourceFilter { get; }
			public Node TargetFilter { get; }


			public ReferenceOptions(
				bool isSource,
				Node sourceFilter,
				Node targetFilter)
			{
				IsSource = isSource;
				SourceFilter = sourceFilter;
				TargetFilter = targetFilter;
			}
		}
	}
}