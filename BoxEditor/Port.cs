using NGraphics;

namespace BoxEditor
{
    public class Port
    {
        public readonly string Id;

        public readonly Rect Frame;
		public readonly PortStyle Style;

		public Port(string id, Rect frame, PortStyle style)
        {
            Id = id;
            Frame = frame;
			Style = style;
        }
    }

	public class PortStyle
	{
		public readonly Color BackgroundColor;
		public readonly Color BorderColor;
		public readonly double BorderWidth;

		public static readonly PortStyle Default = new PortStyle(Colors.LightGray, Colors.Clear, 1);

		public PortStyle(Color backgroundColor, Color borderColor, double borderWidth)
		{
			BackgroundColor = backgroundColor;
			BorderColor = borderColor;
			BorderWidth = borderWidth;
		}
	}

	public class PortRef
	{
		public readonly Box Box;
		public readonly Port Port;
		public PortRef(Box box, Port port)
		{
			Box = box;
			Port = port;
		}
	}
}
