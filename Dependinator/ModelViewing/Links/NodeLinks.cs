namespace Dependinator.ModelViewing.Links
{
	//internal class NodeLinks
	//{
	//	private readonly ILineViewModelService lineViewModelService;
	//	private readonly List<LinkOld> links = new List<LinkOld>();
	//	private readonly List<LinkLineOld> ownedLines = new List<LinkLineOld>();
	//	private readonly List<LinkLineOld> referencingLines = new List<LinkLineOld>();


	//	public IReadOnlyList<LinkOld> Links => links;

	//	public IReadOnlyList<LinkLineOld> OwnedLines => ownedLines;

	//	public IReadOnlyList<LinkLineOld> ReferencingLines => referencingLines;


	//	public NodeLinks(ILineViewModelService lineViewModelService)
	//	{
	//		this.lineViewModelService = lineViewModelService;
	//	}


	//	public void AddDirectLink(NodeOld groupSource, NodeOld groupTarget, IReadOnlyList<LinkOld> groupLinks)
	//	{

	//	}


	//	public void Add(LinkOld link)
	//	{
	//		if (links.Contains(link))
	//		{
	//			return;
	//		}

	//		links.Add(link);

	//		lineViewModelService.AddLinkLines(link);
	//	}
	

	//	public bool TryAddOwnedLine(LinkLineOld line) => ownedLines.TryAdd(line);

	//	public bool RemoveOwnedLine(LinkLineOld line) => ownedLines.Remove(line);

	//	public bool RemoveReferencedLine(LinkLineOld line) => referencingLines.Remove(line);


	//	public bool TryAddReferencedLine(LinkLineOld line) => referencingLines.TryAdd(line);

	//	public override string ToString() => $"{ownedLines.Count} links";
	//}
}