using NGraphics;

namespace BoxEditor
{
	public class Arrow : ISelectable
    {
        public readonly object Value;
		public readonly ArrowStyle Style;
		public readonly PortRef Start;
        public readonly PortRef End;

		public Box StartBox => Start.Box;
		public Box EndBox => End.Box;

		public Arrow(object value, ArrowStyle style, PortRef start, PortRef end)
        {
            Value = value;
			Style = style;
            Start = start;
            End = end;
        }

		public Path GetPath()
		{
			var startCenter = StartBox.Frame.Center;
			var endCenter = EndBox.Frame.Center;

			Point s, e;

			s = Start.Port.Point;
			e = End.Port.Point;

			var sDir = (s - startCenter).Normalized;
			var eDir = (e - endCenter).Normalized;

			var dist = s.DistanceTo(e);
			var c1 = s + sDir * (dist / 3);
			var c2 = e + eDir * (dist / 3);

			var p = new Path();
			p.MoveTo(s);
			p.CurveTo(c1, c2, e);
			return p;
		}
	}

	public class ArrowStyle
	{
		public readonly Color LineColor;
		public readonly double LineWidth;

		public static readonly ArrowStyle Default = new ArrowStyle(Colors.Black, 4);

		public ArrowStyle(Color lineColor, double lineWidth)
		{
			LineColor = lineColor;
			LineWidth = lineWidth;
		}
	}
}
