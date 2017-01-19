using System.Windows;


namespace Dependiator.Modeling
{
	internal interface IModelService
	{
		void InitModules();
		object MoveNode(Point viewPosition, Vector viewOffset, object movingObject);
		void Close();
	}
}