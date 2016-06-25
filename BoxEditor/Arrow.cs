using NGraphics;

namespace BoxEditor
{
    public class Arrow
    {
        public readonly object Value;
		public readonly ArrowStyle Style;

		public readonly PortRef From;
        public readonly PortRef To;

		public Arrow(object value, ArrowStyle style, PortRef @from, PortRef @to)
        {
            Value = value;
			Style = style;
            From = @from;
            To = @to;
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
