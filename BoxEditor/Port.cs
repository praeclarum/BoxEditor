using System;
using System.Collections.Generic;
using NGraphics;

namespace BoxEditor
{
    public class Port
    {
        public readonly string Id;
        public readonly object Value;
        public readonly uint Kind;
        public readonly uint AcceptMask;
        public readonly int MaxConnections;
        public readonly Point RelativePosition;
        public readonly Size Size;
		public readonly Point Direction;
        public readonly FlowDirection FlowDirection;

		public Port(string id, object value, uint kind, uint acceptMask, int maxConnections, Point relativePosition, Size size, Point direction, FlowDirection flowDirection)
        {
            Id = id;
            Value = value;
            Kind = kind;
            MaxConnections = maxConnections;
            AcceptMask = acceptMask;
            RelativePosition = relativePosition;
            Size = size;
			Direction = direction;
            FlowDirection = flowDirection;
        }

        public override string ToString() => $"Port {Id}";

        public Port WithDirection(Point direction)
        {
            return new Port(Id, Value, Kind, AcceptMask, MaxConnections, RelativePosition, Size, direction, FlowDirection);
        }

		public Point GetPoint(Box inBox)
		{
            var bf = inBox.Frame;
            var rf = RelativePosition;
            var pf = new Point(
                bf.X + rf.X * bf.Width,
                bf.Y + rf.Y * bf.Height);
            return pf;
        }

        public Rect GetFrame(Box inBox)
		{
			var bf = inBox.Frame;
			var rf = RelativePosition;
			var pf = new Rect(
				bf.X + rf.X * bf.Width - Size.Width / 2,
				bf.Y + rf.Y * bf.Height - Size.Height / 2,
				Size.Width,
				Size.Height);
			return pf;
		}
	}

    public enum FlowDirection
    {
        Input,
        Output,
        Bidireactional
    }

	public struct PortRef
	{
		public readonly Box Box;
		public readonly Port Port;
		public PortRef(Box box, Port port)
		{            
			Box = box ?? throw new ArgumentNullException(nameof(box));
			Port = port ?? throw new ArgumentNullException(nameof(port));
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
        public override string ToString() => $"{Box} : {Port}";

        public bool EqualIds(PortRef other)
        {
            return Box.Id == other.Box.Id && Port.Id == other.Port.Id;
        }
    }

    public class PortRefIdEquality : IEqualityComparer<PortRef>
    {
        public bool Equals(PortRef x, PortRef y)
        {
            return x.Box.Id == y.Box.Id && x.Port.Id == y.Port.Id;
        }

        public int GetHashCode(PortRef obj)
        {
            return obj.Box.Id.GetHashCode() + obj.Port.Id.GetHashCode();
        }
    }
}
