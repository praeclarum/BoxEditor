using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

using NGraphics;

namespace BoxEditor
{
	/// <summary>
	/// Object representing a "box" that is a collection of "ports".
	/// Boxes have a frame that places them on the diagram.
	/// </summary>
	public class Box : ISelectable
    {
		Rect frame;
		IList<Port> ports;

		Size margin;
        Size minSize = new Size (16, 16);

		public Rect Frame
		{
			get
			{
				return frame;
			}
			set
			{
				if (frame != value)
				{
					frame = value;
				}
			}
		}

		/// <summary>
		/// Used when routing to prevent overlapping boxes.
		/// </summary>
		public virtual Rect PreventOverlapFrame => frame;

		public IList<Port> Ports
		{
			get
			{
				return ports;
			}
			set
			{
				//UnbindPorts ();
				ports = value;
				//BindPorts ();
			}
		}

		public Size Margin
		{
			get
			{
				return margin;
			}
			set
			{
				if (margin != value)
				{
					margin = value;
				}
			}
		}

		public Size MinSize
		{
			get
			{
				return minSize;
			}
			set
			{
				if (minSize != value)
				{
					minSize = value;
				}
			}
		}

		public Box(Rect frame)
        {
			this.frame = frame;
			this.ports = new ObservableCollection<Port> ();
        }

		public Box ()
			: this (new Rect())
		{
		}

		public Rect FrameWithMargin => Frame.GetInflated(Margin);

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

        public void AddPort(string v, Point point1, Point point2)
        {
            Ports.Add(new Port(v, new Rect (point1, Size.Zero), point2));
        }

        /// <summary>
        /// Whether the point is in the Frame of this box.
        /// </summary>
        /// <param name="point">The point to test.</param>
        public bool HitTest(Point point)
		{
			return Frame.Contains(point);
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
			//Debug.WriteLine ("HIT? {0} {1} {2}", this, point, maxDistance);

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

		/// <summary>
		/// Get a new box with a resize handle moved by a distance d.
		/// </summary>
		/// <returns>The new box.</returns>
		/// <param name="index">The index of the handle to move.</param>
		/// <param name="d">The distance to move the handle.</param>
		public void MoveHandle(int index, Point d)
		{
			var dx = new Point(d.X, 0);
			var dy = new Point(0, d.Y);

			var minw = minSize.Width;
			var minh = minSize.Height;

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
            //Debug.WriteLine($"MOVE HANDLE from {frame} to {newFrame}");
			this.frame = newFrame;
		}

		/// <summary>
		/// Get a list of all the guides this box uses.
		/// </summary>
		/// <returns>The drag guides.</returns>
		/// <param name="offset">Amount to offset this box before getting its guides.</param>
		/// <param name="boxIndex">A box index to store in the guides.</param>
        public IEnumerable<DragGuide> GetDragGuides(Rect frame, Point offset, int boxIndex)
		{
			yield return DragGuide.Vertical(frame.Center.X + offset.X, DragGuideSource.CenterV, boxIndex);
			yield return DragGuide.Horizontal(frame.Center.Y + offset.Y, DragGuideSource.CenterH, boxIndex);
			var mx = margin.Width;
			var my = margin.Height;
			if (mx > 0)
			{
				yield return DragGuide.Vertical(frame.Left + offset.X - mx, DragGuideSource.LeftMargin, boxIndex);
				yield return DragGuide.Vertical(frame.Right + offset.X + mx, DragGuideSource.RightMargin, boxIndex);
			}
			if (my > 0)
			{
				yield return DragGuide.Horizontal(frame.Top + offset.Y - my, DragGuideSource.TopMargin, boxIndex);
				yield return DragGuide.Horizontal(frame.Bottom + offset.Y + my, DragGuideSource.BottomMargin, boxIndex);
			}
			yield return DragGuide.Vertical(frame.Left + offset.X, DragGuideSource.LeftEdge, boxIndex);
			yield return DragGuide.Vertical(frame.Right + offset.X, DragGuideSource.RightEdge, boxIndex);
			yield return DragGuide.Horizontal(frame.Top + offset.Y, DragGuideSource.TopEdge, boxIndex);
			yield return DragGuide.Horizontal(frame.Bottom + offset.Y, DragGuideSource.BottomEdge, boxIndex);
			for (int i = 0; i < Ports.Count; i++)
			{
				var p = Ports[i];
				var f = p.GetFrame(this);
				var c = f.Center + offset;
				yield return DragGuide.Vertical(c.X, DragGuideSource.PortVS + i, boxIndex);
				yield return DragGuide.Horizontal(c.Y, DragGuideSource.PortHS + i, boxIndex);
			}
		}

		public virtual void Draw (ICanvas canvas)
		{
			canvas.DrawRectangle (frame, pen: new Pen (Colors.Black, 2), brush: new SolidBrush (Colors.White));
		}
	}
}
