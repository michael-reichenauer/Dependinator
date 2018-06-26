//using System;
//using System.Windows;
//using Dependinator.Common;
//using Dependinator.ModelViewing.ModelHandling.Core;


//namespace Dependinator.ModelViewing.DependencyExploring.Private
//{
//	internal class DependencyExplorerService : IDependencyExplorerService
//	{
//		private readonly IDependencyWindowService dependencyWindowService;
//		private readonly Lazy<IModelNotifications> modelNotifications;
//		private readonly WindowOwner owner;


//		public DependencyExplorerService(
//			IDependencyWindowService dependencyWindowService,
//			Lazy<IModelNotifications> modelNotifications,
//			WindowOwner owner)
//		{
//			this.dependencyWindowService = dependencyWindowService;
//			this.modelNotifications = modelNotifications;
//			this.owner = owner;
//		}


//		public void ShowWindow(Node node)
//		{
//			Window window = new DependencyExplorerWindow(
//				dependencyWindowService, modelNotifications.Value, owner, node, null);
//			window.Show();
//		}


//		public void ShowWindow(Line line)
//		{
//			Window window = new DependencyExplorerWindow(
//				dependencyWindowService, modelNotifications.Value, owner, null, line);
//			window.Show();
//		}
//	}
//}