using System;
using NGraphics;

namespace BoxEditor
{
    public class Port
    {
        public readonly object Value;
		public readonly Rect RelativeFrame;
		public readonly Directions Directions;

		public Port(object value, Rect relativeFrame, Directions directions)
        {
            Value = value;
            RelativeFrame = relativeFrame;
			Directions = directions;
        }

		public Port Move(Point d)
		{
			return new Port(Value, RelativeFrame + d, Directions);
		}

		public Point GetPoint(Box inBox)
		{
			return GetFrame(inBox).Center;
		}

		public Rect GetFrame(Box inBox)
		{
			var bf = inBox.Frame;
			var rf = RelativeFrame;
			var pf = new Rect(
				bf.X + rf.X * bf.Width,
				bf.Y + rf.Y * bf.Height,
				rf.Width * bf.Width,
				rf.Height * bf.Height);
			return pf;
		}
	}

	[Flags]
	public enum Directions
	{
		None = 0x00,
		Left = 0x01,
		Right = 0x02,
		Up = 0x04,
		Down = 0x08,
		Horizontal = Left | Right,
		Vertical = Up | Down,
		Any = Left | Right | Up | Down,
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
		public PortRef UpdateBox(Box b, Box newb)
		{
			if (b != Box) return this;
			var pi = Box.Ports.IndexOf(Port);
			if (0 <= pi && pi < newb.Ports.Length)
			{
				return new PortRef(newb, newb.Ports[pi]);
			}
			else {
				return this;
			}
		}
		public Rect PortFrame => Port.GetFrame(Box);
		public Point PortPoint => Port.GetPoint(Box);
	}
}
