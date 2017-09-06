using System.Threading.Tasks;

namespace Dependinator.Common.WorkFolders
{
	public interface IOpenService
	{
		Task OpenFileAsync();
	}
}