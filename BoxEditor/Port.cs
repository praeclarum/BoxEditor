using System;
using NGraphics;

namespace BoxEditor
{
    public class Port
    {
        public readonly object Value;
		public readonly Rect RelativeFrame;
		public readonly Point Direction;

		public Port(object value, Rect relativeFrame, Point directions)
        {
            Value = value;
            RelativeFrame = relativeFrame;
			Direction = directions;
        }

		public Port Move(Point d)
		{
			return new Port(Value, RelativeFrame + d, Direction);
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
