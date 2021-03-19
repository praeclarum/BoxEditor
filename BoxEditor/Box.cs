using NGraphics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BoxEditor
{
	/// <summary>
	/// Immutable object representing a "box" that is a collection of "ports".
	/// Boxes also have a frame, a value, and a style.
	/// </summary>
	public class Box : ISelectable
    {
		readonly string id;
		public string Id => id;
		public readonly object Value;
        public readonly Rect Frame;
		public readonly BoxStyle Style;
        public readonly ImmutableArray<Port> Ports;
		/// <summary>
		/// Used when routing to prevent overlapping boxes.
		/// </summary>
		public readonly Rect PreventOverlapFrame;

		public Box(string id, object value, Rect frame, Rect preventOverlapFrame, BoxStyle style, ImmutableArray<Port> ports)
        {
			if (string.IsNullOrEmpty(id))
			{
				throw new ArgumentException("Id must be set");
			}
			this.id = id;
			Value = value;
            Frame = frame;
			Style = style;
            Ports = ports;
			PreventOverlapFrame = preventOverlapFrame;
        }

		public Box(string id, object value, Rect frame, BoxStyle style)
			: this (id, value, frame, frame, style, ImmutableArray<Port>.Empty)
		{
		}

        public override string ToString() => $"Box {Id}";

		public Rect FrameWithMargin => Frame.GetInflated(Style.Margin);

		/// <summary>
		/// The list of resize handles to display when selected.
		/// </summary>
		/// <returns>The points of the resize handles.</returns>
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

		/// <summary>
		/// Whether the point is in the Frame of this box.
		/// </summary>
		/// <param name="point">The point to test.</param>
		public bool HitTest(Point point)
		{
			return Frame.Contains(point);
		}

        public Port GetPort(string portId)
        {
            return Ports.FirstOrDefault(x => x.Id == portId);
        }

		/// <summary>
		/// Whether the point is in a resize handle.
		/// </summary>
		/// <returns>null if no handle was hit; otherwise,
		/// the index of the hit handle and the distance to it.</returns>
		/// <param name="point">The point to test.</param>
		/// <param name="handleSize">The size of a handle.</param>
		/// <param name="maxDistance">The farthest out from a handle the point
		/// can be while still considered a hit.</param>
		public Tuple<int, double> HitTestHandles(Point point, Size handleSize, double maxDistance)
		{
            //Debug.WriteLine("HIT? {0} {1} {2}", this, point, maxDistance);

			var hpoints = GetHandlePoints();
			var q =
				from i in Enumerable.Range(0, hpoints.Length)
				let d = hpoints[i].DistanceTo(point)
				where d < maxDistance
				orderby d
				select Tuple.Create(i, d);

			var r = q.FirstOrDefault();
            //Debug.WriteLine("  ..HIT {0}", r);
            return r;
		}

		/// <summary>
		/// Get a new box with a new Frame.
		/// </summary>
		/// <returns>The new box.</returns>
		/// <param name="newFrame">The new frame.</param>
		public Box WithFrame(Rect newFrame, Rect newPreventOverlapFrame)
		{
			return new Box (Id, Value, newFrame, newPreventOverlapFrame, Style, Ports);
		}

        public Box WithPorts(ImmutableArray<Port> ports)
        {
            return new Box(Id, Value, Frame, PreventOverlapFrame, Style, ports);
        }

        /// <summary>
        /// Get a new box moved by a distance d.
        /// </summary>
        /// <param name="d">The distance to move the box.</param>
        public Box Move(Point d)
		{
			var newFrame = new Rect(Frame.TopLeft + d, Frame.Size);
			var newPreventOverlapFrame = new Rect(PreventOverlapFrame.TopLeft + d, PreventOverlapFrame.Size);
			return new Box(Id, Value, newFrame, newPreventOverlapFrame, Style, Ports);
		}

		/// <summary>
		/// Get a new box with a resize handle moved by a distance d.
		/// </summary>
		/// <returns>The new box.</returns>
		/// <param name="index">The index of the handle to move.</param>
		/// <param name="d">The distance to move the handle.</param>
		public Box MoveHandle(int index, Point d)
		{
			var dx = new Point(d.X, 0);
			var dy = new Point(0, d.Y);

			var minw = Style.MinSize.Width;
			var minh = Style.MinSize.Height;

			var dl = Point.Zero;
			var ds = Point.Zero;

			switch (index)
			{
				case 0:
					dl = d;
					ds = -d;
					break;
				case 1:
					dl = dy;
					ds = dx - dy;
					break;
				case 2:
					dl = dx;
					ds = -dx + dy;
					break;
				case 3:
					ds = d;
					break;
				case 4:
					dl = dy;
					ds = -dy;
					break;
				case 5:
					dl = dx;
					ds = -dx;
					break;
				case 6:
					ds = dx;
					break;
				case 7:
					ds = dy;
					break;
			}
			if (Frame.Width + ds.X < minw)
			{
				var nds = minw - Frame.Width;
				var dds = nds - ds.X;
				if (Math.Abs(dl.X) > 1e-12) dl.X -= dds;
				ds.X = nds;
			}
			if (Frame.Height + ds.Y < minh)
			{
				var nds = minh - Frame.Height;
				var dds = nds - ds.Y;
				if (Math.Abs(dl.Y) > 1e-12) dl.Y -= dds;
				ds.Y = nds;
			}
			var newFrame = new Rect(Frame.TopLeft + dl, Frame.Size + ds);
			var newPreventOverlapFrame = new Rect(PreventOverlapFrame.TopLeft + dl, PreventOverlapFrame.Size + ds);
			return WithFrame(newFrame, newPreventOverlapFrame);
		}

		/// <summary>
		/// Get a list of all the guides this box uses.
		/// </summary>
		/// <returns>The drag guides.</returns>
		/// <param name="offset">Amount to offset this box before getting its guides.</param>
		/// <param name="boxIndex">A box index to store in the guides.</param>
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

	/// <summary>
	/// Mutable object to help construct immutable Boxes.
	/// </summary>
	public class BoxBuilder
    {
		public string Id;
        public object Value;
		public Rect Frame;
        public List<Port> Ports = new List<Port>();
		public BoxStyle Style = BoxStyle.Default;

		/// <summary>
		/// Add a port given its frame.
		/// </summary>
		/// <param name="value">The value to associate with the port.</param>
		/// <param name="relativePoint">The position of the port relative to the box's frame.</param>
		/// <param name="direction">Direction arrows should leave this port.</param>
		public void AddPort(string id, object value, Point relativePoint, Size size, Point direction, FlowDirection flowDirection)
        {
			Ports.Add(new Port(id, value, 1, uint.MaxValue, int.MaxValue, relativePoint, size, direction, flowDirection));
        }

		/// <summary>
		/// Add a port given its location.
		/// </summary>
		/// <param name="value">The value to associate with the port.</param>
		/// <param name="relativePoint">The location of the port relative to the box's frame.</param>
		/// <param name="direction">Direction arrows should leave this port.</param>
		public void AddPort(string id, object value, Point relativePoint, Point direction, FlowDirection flowDirection)
		{
			Ports.Add(new Port(id, value, 1, uint.MaxValue, int.MaxValue, relativePoint, Size.Zero, direction, flowDirection));
		}

		/// <summary>
		/// Get the immutable box as a snapshot of this builder.
		/// </summary>
		/// <returns>The box.</returns>
        public Box ToBox()
        {
			if (string.IsNullOrEmpty(Id))
			{
				throw new InvalidOperationException("Id must be set");
			}
			return new Box(Id, Value, Frame, Frame, Style, Ports.ToImmutableArray());
        }
    }

	/// <summary>
	/// Immutable styling information for boxes.
	/// </summary>
	public class BoxStyle
	{
		public readonly Color BackgroundColor;
		public readonly Color BorderColor;
		public readonly double BorderWidth;
		public readonly Size Margin;
		public readonly Size MinSize;

		public static readonly BoxStyle Default =
			new BoxStyle(Colors.White, Colors.Black, 1, new Size(11, 22), new Size(44, 44));

		public BoxStyle(Color backgroundColor, Color borderColor, double borderWidth, Size margin, Size minSize)
		{
			BackgroundColor = backgroundColor;
			BorderColor = borderColor;
			BorderWidth = borderWidth;
			Margin = margin;
			MinSize = minSize;
		}
	}
}
