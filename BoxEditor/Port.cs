using System;
using NGraphics;

namespace BoxEditor
{
    public class Port
    {
        public readonly object Value;
        public readonly Point Point;
		public readonly Directions Directions;

		public Port(object value, Point point, Directions directions)
        {
            Value = value;
            Point = point;
			Directions = directions;
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
	}
}
