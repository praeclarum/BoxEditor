using System;
using NGraphics;

namespace BoxEditor
{
	public class Arrow : ISelectable
    {
		public readonly string id;
		public string Id => id;
		public readonly object Value;
		public readonly ArrowStyle Style;
		public readonly PortRef Start;
        public readonly PortRef End;

		public Box StartBox => Start.Box;
		public Box EndBox => End.Box;

		public Arrow(string id, object value, ArrowStyle style, PortRef start, PortRef end)
        {
			this.id = id;
            Value = value;
			Style = style;
            Start = start;
            End = end;
        }

		public Arrow UpdateBox(Box b, Box newb)
		{
			return new Arrow(Id, Value, Style, Start.UpdateBox(b,newb), End.UpdateBox(b,newb));
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
