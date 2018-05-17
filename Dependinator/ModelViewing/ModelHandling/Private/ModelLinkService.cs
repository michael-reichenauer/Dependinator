using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Common;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal class ModelLinkService : IModelLinkService
	{
		private readonly IModelLineService modelLineService;
		private readonly IModelService modelService;


		public ModelLinkService(
			IModelLineService modelLineService,
			IModelService modelService)
		{
			this.modelLineService = modelLineService;
			this.modelService = modelService;
		}


		public void UpdateLink(ModelLink modelLink, int stamp)
		{
			try
			{
				Node source = modelService.GetNode(NodeName.From(modelLink.Source));

				if (!TryGetTarget(modelLink, out Node target))
				{
					return;
				}


				target.Stamp = stamp;

				if (TryGetLink(source, target, out Link link))
				{
					// Already added link
					link.Stamp = stamp;
					return;
				}

				link = AddLink(source, target);
				link.Stamp = stamp;

				modelLineService.AddLinkLines(link);
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Failed to update link {modelLink}");
				throw;
			}
		}


		private bool TryGetTarget(ModelLink modelLink, out Node target)
		{
			NodeName targetName = NodeName.From(modelLink.Target);
			if (!modelService.TryGetNode(targetName, out target))
			{
				modelService.QueueModelLink(targetName, modelLink);
				return false;
			}

			return true;
		}



		public void RemoveObsoleteLinks(IReadOnlyList<Link> obsoleteLinks)
		{
			foreach (Link link in obsoleteLinks)
			{
				foreach (Line line in link.Lines)
				{
					line.Links.Remove(link);

					if (!line.Links.Any())
					{
						modelLineService.RemoveLine(line);
					}
				}

				RemoveLink(link);
			}
		}


		public void ResetLayout(List<Link> links)
		{
			foreach (Link link in links)
			{
				foreach (Line line in link.Lines)
				{
					if (line.View.Points.Count != 2)
					{
						line.View.ResetPoints();
						line.View.ViewModel.UpdateLine();
						line.View.ViewModel.NotifyAll();
					}
				}
			}
		}


		private static Link AddLink(Node source, Node target)
		{
			Link link = new Link(source, target);
			link.Source.SourceLinks.Add(link);
			return link;
		}


		private void RemoveLink(Link link)
		{
			link.Source.SourceLinks.Remove(link);
		}


		private static bool TryGetLink(Node source, Node target, out Link link)
		{
			link = source.SourceLinks.FirstOrDefault(l => l.Source == source && l.Target == target);
			return link != null;
		}
	}
}