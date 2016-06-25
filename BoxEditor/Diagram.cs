using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using NGraphics;

namespace BoxEditor
{
    public class Diagram
    {
        public readonly ImmutableArray<Box> Boxes;
        public readonly ImmutableArray<Arrow> Arrows;
		public readonly DiagramStyle Style;

        public static Diagram Empty =
			new Diagram(ImmutableArray<Box>.Empty, ImmutableArray<Arrow>.Empty, DiagramStyle.Default);

		public Diagram(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows, DiagramStyle style)
        {
            Boxes = boxes;
            Arrows = arrows;
			Style = style;
        }

        public static Diagram Create(
			DiagramStyle style,
            IEnumerable<object> boxValues,
            IEnumerable<object> arrowValues,
            Func<object, Box> getBox,
            Func<Func<object, object, PortRef>, object, Arrow> getArrow)
        {
            var boxes = boxValues.Select(getBox).ToImmutableArray();

            var portIndex = new Dictionary<Tuple<object, object>, PortRef>();
            foreach (var b in boxes)
            {
                foreach (var p in b.Ports)
                {
                    portIndex[Tuple.Create(b.Value, p.Value)] = new PortRef (b, p);
                }
            }
            Func<object, object, PortRef> portF = (o, n) => portIndex[Tuple.Create(o, n)];

            var arrows = arrowValues.Select (o => getArrow(portF, o)).ToImmutableArray();

            return new Diagram(boxes, arrows, style);
        }

		public IEnumerable<Box> HitTestBoxes(Point point)
		{
			return Boxes.Where(x => x.HitTest(point)).Reverse().ToList();
		}

		public IEnumerable<Arrow> HitTestArrows(Point point, double viewToDiagramScale)
		{
			var maxDist = viewToDiagramScale * 22;
			var q = from a in Arrows
					let p = a.GetPath()
					let d = p.DistanceTo(point)
	                where d < a.Style.LineWidth / 2 + maxDist
					orderby d ascending
					select a;
			return q.ToList();
		}


	}

	public class DiagramStyle
	{
		public readonly Color BackgroundColor;
		public readonly Color HandleBackgroundColor;
		public readonly Color HandleBorderColor;

		public static readonly DiagramStyle Default = new DiagramStyle(
			Color.FromWhite(236/255.0, 1), Colors.White, Colors.Black);

		public DiagramStyle(Color backgroundColor, Color handleBackgroundColor, Color handleBorderColor)
		{
			BackgroundColor = backgroundColor;
			HandleBackgroundColor = handleBackgroundColor;
			HandleBorderColor = handleBorderColor;
		}
	}
}
