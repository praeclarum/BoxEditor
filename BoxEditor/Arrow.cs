using System;
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

		public virtual void Draw (Path path, ICanvas canvas)
		{
			path.Draw (canvas);
		}
	}

	public class ArrowStyle
	{
		public readonly Color LineColor;
		public readonly double LineWidth;
		public readonly bool ViewDependent;

        public static readonly ArrowStyle Default = new ArrowStyle(Colors.Gray, 2, false);

		public ArrowStyle(Color lineColor, double lineWidth, bool viewDependent)
		{
			LineColor = lineColor;
			LineWidth = lineWidth;
			ViewDependent = viewDependent;
		}
	}
}
