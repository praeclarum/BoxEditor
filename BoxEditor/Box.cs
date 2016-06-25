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
        public readonly ImmutableArray<Port> Ports;

        public Box(object value, Rect frame, ImmutableArray<Port> ports)
        {
            Value = value;
            Frame = frame;
            Ports = ports;
        }
    }

    public class BoxBuilder
    {
        public object Value;
		public Rect Frame;
        public List<Port> Ports = new List<Port>();

        public void AddPort(string id, Rect frame)
        {
            Ports.Add(new Port(id, frame));
        }

        public Box ToBox()
        {
            return new Box(Value, Frame, Ports.ToImmutableArray());
        }
    }
}
