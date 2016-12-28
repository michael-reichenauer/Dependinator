using System;


namespace Dependiator.GitModel
{
	[Flags]
	public enum GitFileStatus
	{
		Modified,
		Added,
		Deleted,
		Renamed,
		Conflict,
	}
}