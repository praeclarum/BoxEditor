using NGraphics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace BoxEditor
{
	public class Box : ISelectable
    {
        public readonly object Value;
        public readonly Rect Frame;
		public readonly BoxStyle Style;
        public readonly ImmutableArray<Port> Ports;

		public Box(object value, Rect frame, BoxStyle style, ImmutableArray<Port> ports)
        {
            Value = value;
            Frame = frame;
			Style = style;
            Ports = ports;
        }

		public Rect PortBoundingBox
		{
			get
			{
				var bb = new BoundingBoxBuilder();
				foreach (var p in Ports)
				{
					bb.Add(p.GetPoint(this));
				}
				return bb.BoundingBox;
			}
		}

		public Point[] GetHandlePoints()
		{
			return new[] {
				Frame.TopLeft,
				Frame.TopRight,
				Frame.BottomLeft,
				Frame.BottomRight,
				(Frame.TopLeft + Frame.TopRight) / 2,
				(Frame.TopLeft + Frame.BottomLeft) / 2,
				(Frame.TopRight + Frame.BottomRight) / 2,
				(Frame.BottomLeft + Frame.BottomRight) / 2,
			};
		}

		public bool HitTest(Point point)
		{
			return Frame.Contains(point);
		}

		public Tuple<int, double> HitTestHandles(Point point, Size handleSize, double tolerance)
		{
			//			Console.WriteLine ("HIT? {0} {1} {2}", this, point, maxDistance);

			var maxDistance = handleSize.Diagonal / 2 + tolerance;

			var nohit = Frame.GetInflated(-handleSize / 2);
			if (nohit.Contains(point))
				return null;

			var hpoints = GetHandlePoints();
			var q =
				from i in Enumerable.Range(0, hpoints.Length)
				let d = hpoints[i].DistanceTo(point)
				where d < maxDistance
				orderby d
				select Tuple.Create(i, d);

			return q.FirstOrDefault();
		}

		public Box WithFrame(Rect newFrame)
		{
			return new Box (Value, newFrame, Style, Ports);
		}

		public Box Move(Point d)
		{
			var newFrame = new Rect(Frame.TopLeft + d, Frame.Size);
			return new Box(Value, newFrame, Style, Ports);
		}

		public Box MoveHandle(int index, Point d)
		{
			var dx = new Point(d.X, 0);
			var dy = new Point(0, d.Y);

			var newFrame = Frame;

			switch (index)
			{
				case 0:
					newFrame = new Rect(Frame.TopLeft + d, Frame.Size - d);
					break;
				case 1:
					newFrame = new Rect(Frame.TopLeft + dy, Frame.Size + dx - dy);
					break;
				case 2:
					newFrame = new Rect(Frame.TopLeft + dx, Frame.Size - dx + dy);
					break;
				case 3:
					newFrame = new Rect(Frame.TopLeft, Frame.Size + d);
					break;
				case 4:
					newFrame = new Rect(Frame.TopLeft + dy, Frame.Size - dy);
					break;
				case 5:
					newFrame = new Rect(Frame.TopLeft + dx, Frame.Size - dx);
					break;
				case 6:
					newFrame = new Rect(Frame.TopLeft, Frame.Size + dx);
					break;
				case 7:
					newFrame = new Rect(Frame.TopLeft, Frame.Size + dy);
					break;
			}
			return WithFrame(newFrame);
		}

		public IEnumerable<DragGuide> GetDragGuides(Point offset, int boxIndex)
		{
			yield return DragGuide.Vertical(Frame.Center.X + offset.X, DragGuideSource.CenterV, boxIndex);
			yield return DragGuide.Horizontal(Frame.Center.Y + offset.Y, DragGuideSource.CenterH, boxIndex);
			var mx = Style.Margin.Width;
			var my = Style.Margin.Height;
			if (mx > 0)
			{
				yield return DragGuide.Vertical(Frame.Left + offset.X - mx, DragGuideSource.LeftMargin, boxIndex);
				yield return DragGuide.Vertical(Frame.Right + offset.X + mx, DragGuideSource.RightMargin, boxIndex);
			}
			if (my > 0)
			{
				yield return DragGuide.Horizontal(Frame.Top + offset.Y - my, DragGuideSource.TopMargin, boxIndex);
				yield return DragGuide.Horizontal(Frame.Bottom + offset.Y + my, DragGuideSource.BottomMargin, boxIndex);
			}
			yield return DragGuide.Vertical(Frame.Left + offset.X, DragGuideSource.LeftEdge, boxIndex);
			yield return DragGuide.Vertical(Frame.Right + offset.X, DragGuideSource.RightEdge, boxIndex);
			yield return DragGuide.Horizontal(Frame.Top + offset.Y, DragGuideSource.TopEdge, boxIndex);
			yield return DragGuide.Horizontal(Frame.Bottom + offset.Y, DragGuideSource.BottomEdge, boxIndex);
			for (int i = 0; i < Ports.Length; i++)
			{
				var p = Ports[i];
				var f = p.GetFrame(this);
				var c = f.Center + offset;
				yield return DragGuide.Vertical(c.X, DragGuideSource.PortVS + i, boxIndex);
				yield return DragGuide.Horizontal(c.Y, DragGuideSource.PortHS + i, boxIndex);
			}
		}
	}

	public class BoxBuilder
    {
        public object Value;
		public Rect Frame;
        public List<Port> Ports = new List<Port>();
		public BoxStyle Style = BoxStyle.Default;

		public void AddPort(object value, Rect relativeFrame, Point directions)
        {
			Ports.Add(new Port(value, relativeFrame, directions));
        }

		public void AddPort(object value, Point relativePoint, Point directions)
		{
			Ports.Add(new Port(value, new Rect(relativePoint, Size.Zero), directions));
		}

        public Box ToBox()
        {
			return new Box(Value, Frame, Style, Ports.ToImmutableArray());
        }
    }

	public class BoxStyle
	{
		public readonly Color BackgroundColor;
		public readonly Color BorderColor;
		public readonly double BorderWidth;
		public readonly Size Margin;

		public static readonly BoxStyle Default =
			new BoxStyle(Colors.White, Colors.Black, 1, new Size (10, 20));

		public BoxStyle(Color backgroundColor, Color borderColor, double borderWidth, Size margin)
		{
			BackgroundColor = backgroundColor;
			BorderColor = borderColor;
			BorderWidth = borderWidth;
			Margin = margin;
		}
	}
}
