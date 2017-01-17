using System.Windows;


namespace Dependiator.Modeling
{
	internal interface IModelService
	{
		void InitModules(string path);
		object MoveNode(Point viewPosition, Vector viewOffset, object movingObject);
		void Close();
	}
}