//namespace Dependiator.GitModel
//{
//	public class CommitFile
//	{
		
//		public string Path => "";
//		public string OldPath => "";

//		public GitFileStatus Status => GitFileStatus.Modified;

//		public string StatusText => GetStatusText();


//		private string GetStatusText()
//		{
//			if (Status.HasFlag(GitFileStatus.Renamed) && Status.HasFlag(GitFileStatus.Modified))
//			{
//				return "RM";
//			}
//			else if (Status.HasFlag(GitFileStatus.Renamed))
//			{
//				return "R";
//			}
//			else if (Status.HasFlag(GitFileStatus.Added))
//			{
//				return "A";
//			}
//			else if (Status.HasFlag(GitFileStatus.Deleted))
//			{
//				return "D";
//			}
//			else if (Status.HasFlag(GitFileStatus.Conflict))
//			{
//				return "C";
//			}

//			return "";
//		}
	
//	}
//}