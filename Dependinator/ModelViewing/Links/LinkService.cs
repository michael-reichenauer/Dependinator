using System.Collections.Generic;
using System.Linq;
using Dependinator.Modeling;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private;

namespace Dependinator.ModelViewing.Links
{
	internal class LinkService : ILinkService
	{
		private readonly ILinkSegmentService linkSegmentService;
		private readonly ILineViewModelService lineViewModelService;
		private readonly Model model;


		public LinkService(
			ILinkSegmentService linkSegmentService,
			ILineViewModelService lineViewModelService,
			Model model)
		{
			this.linkSegmentService = linkSegmentService;
			this.lineViewModelService = lineViewModelService;
			this.model = model;
		}

		public void UpdateLink(DataLink dataLink)
		{
			if (dataLink.Source == dataLink.Target)
			{
				// Skipping link to self for now
				return;
			}

			Node source = GetNode(dataLink.Source);
			Node target = GetNode(dataLink.Target);

			if (SourceLinkExists(source, target))

			{
				// Already added link
				return;
			}

			Link link = AddLink(source, target);

			var linkSegments = linkSegmentService.GetLinkSegments(link);

			AddLinkSegmentLines(linkSegments, link);
		}


		private void AddLinkSegmentLines(IEnumerable<LinkSegment> linkSegments, Link link)
		{
			foreach (LinkSegment linkSegment in linkSegments)
			{
				if (!TryGetLine(linkSegment, out Line line))
				{
					line = AddLine(linkSegment);				
				}

				line.Links.Add(link);
			}
		}


		private Line AddLine(LinkSegment segment)
		{
			Line line = new Line(segment.Source, segment.Target);
			line.Source.SourceLines.Add(line);
			AddLineViewModel(line);
			return line;
		}

		private void AddLineViewModel(Line line)
		{
			Node owner = GetLineOwner(line);
			LineViewModel lineViewModel = new LineViewModel(lineViewModelService, line);

			owner.ItemsCanvas.AddItem(lineViewModel);
		}


		private static Node GetLineOwner(Line line) => 
			line.Source == line.Target.Parent ? line.Source : line.Source.Parent;


		private static Link AddLink(Node source, Node target)
		{
			Link link = new Link(source, target);
			source.SourceLinks.Add(link);
			return link;
		}


		private Node GetNode(string name)
		{
			NodeName nodeName = new NodeName(name);
			Node node = model.Node(nodeName);
			return node;
		}


		private static bool TryGetLine(LinkSegment segment, out Line line)
		{
			line = segment.Source.SourceLines
				.FirstOrDefault(l => l.Source == segment.Source && l.Target == segment.Target);
			return line != null;
		}


		private static bool SourceLinkExists(Node source, Node target) =>
			source.SourceLinks.Any(l => l.Source == source && l.Target == target);
	}
}