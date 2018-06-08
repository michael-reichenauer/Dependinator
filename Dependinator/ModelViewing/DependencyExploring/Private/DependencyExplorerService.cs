using System.Windows;
using Dependinator.Common;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependencyExplorerService : IDependencyExplorerService
	{
		private readonly IDependencyWindowService dependencyWindowService;
		private readonly WindowOwner owner;


		public DependencyExplorerService(
			IDependencyWindowService dependencyWindowService,
			WindowOwner owner)
		{
			this.dependencyWindowService = dependencyWindowService;
			this.owner = owner;
		}


		public void ShowWindow(Node node)
		{
			Window window = new DependencyExplorerWindow(dependencyWindowService, owner, node, null);
			window.Show();
		}


		public void ShowWindow(Line line)
		{
			Window window = new DependencyExplorerWindow(dependencyWindowService, owner, null, line);
			window.Show();
		}
	}
}