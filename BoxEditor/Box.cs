using NGraphics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace BoxEditor
{
    public class Box
    {
        public readonly object Value;
        public readonly Rect Frame;
		public readonly BoxStyle Style;
        public readonly ImmutableArray<Port> Ports;
		public readonly State State;

		public Box(object value, Rect frame, BoxStyle style, State state, ImmutableArray<Port> ports)
        {
            Value = value;
            Frame = frame;
			Style = style;
			State = state;
            Ports = ports;
        }
    }

	public class BoxBuilder
    {
        public object Value;
		public Rect Frame;
        public List<Port> Ports = new List<Port>();
		public BoxStyle Style = BoxStyle.Default;
		public State State = State.None;

		public void AddPort(object value, Point point, Directions directions)
        {
			Ports.Add(new Port(value, point, directions));
        }

        public Box ToBox()
        {
			return new Box(Value, Frame, Style, State, Ports.ToImmutableArray());
        }
    }

	public class BoxStyle
	{
		public readonly Color BackgroundColor;
		public readonly Color BorderColor;
		public readonly double BorderWidth;

		public static readonly BoxStyle Default = new BoxStyle(Colors.White, Colors.Black, 1);

		public BoxStyle(Color backgroundColor, Color borderColor, double borderWidth)
		{
			BackgroundColor = backgroundColor;
			BorderColor = borderColor;
			BorderWidth = borderWidth;
		}
	}
}
