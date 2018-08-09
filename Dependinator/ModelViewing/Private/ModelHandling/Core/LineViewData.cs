using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.Private.Lines;


namespace Dependinator.ModelViewing.Private.ModelHandling.Core
{
    internal class LineViewData
    {
        private static readonly List<Point> DefaultPoints =
            new List<Point>
            {
                new Point(0, 0),
                new Point(0, 0)
            };


        public LineViewModel ViewModel { get; set; }

        public bool IsShowing => ViewModel?.IsShowing ?? false;

        public Point RelativeSourcePoint { get; set; } = new Point(-1, -1);
        public Point RelativeTargetPoint { get; set; } = new Point(-1, -1);

        public List<Point> Points { get; private set; } = DefaultPoints.ToList();


        public Point FirstPoint { get => Points[FirstIndex]; set => Points[FirstIndex] = value; }
        public Point LastPoint { get => Points[LastIndex]; set => Points[LastIndex] = value; }
        public int LastIndex => Points.Count - 1;
        public int FirstIndex => 0;


        public IEnumerable<Point> MiddlePoints()
        {
            for (int i = 1; i < Points.Count - 1; i++)
            {
                yield return Points[i];
            }
        }


        public void ResetPoints() => Points = DefaultPoints.ToList();
    }
}
