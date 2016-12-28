using System;


namespace Dependiator.Git
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