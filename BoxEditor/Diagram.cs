using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NGraphics;

namespace BoxEditor
{
    public class Diagram
    {
        public readonly ImmutableArray<Box> Boxes;
        public readonly ImmutableArray<Arrow> Arrows;
		public readonly DiagramStyle Style;
        public DiagramPaths Paths => paths.Value;

        readonly Lazy<DiagramPaths> paths;

        public static Diagram Empty =
			new Diagram(ImmutableArray<Box>.Empty, ImmutableArray<Arrow>.Empty, DiagramStyle.Default);

		public Diagram(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows, DiagramStyle style)
        {
            Boxes = boxes;
            Arrows = arrows;
			Style = style;
			paths = new Lazy<DiagramPaths> (() => PathPlanning.Plan(boxes, arrows));
        }

        public override string ToString() => $"Diagram with {Boxes.Length} boxes";

        public Diagram WithBoxes(ImmutableArray<Box> newBoxes)
		{
			return new Diagram(newBoxes, Arrows, Style);
		}

		public Diagram WithArrows(ImmutableArray<Arrow> newArrows)
		{
			return new Diagram(Boxes, newArrows, Style);
		}

        public Diagram AddBox (Box box)
        {
            return WithBoxes(Boxes.Concat(new[] { box }).ToImmutableArray());
        }

        public Diagram RemoveBox(Box box)
        {
            return WithBoxes(Boxes.RemoveAll (x => x.Id == box.Id));
        }

        public Diagram AddArrow (Arrow arrow)
        {
            return WithArrows(Arrows.Concat(new[] { arrow }).ToImmutableArray());
        }

        public Diagram UpdateArrow(Arrow oldArrow, Arrow newArrow)
        {
            return WithArrows(Arrows.Replace(oldArrow, newArrow));
        }

        public Diagram UpdateBoxes(IEnumerable<Tuple<Box, Box>> boxes)
		{
			var newBoxes = Boxes;
			var newArrows = Arrows;
			foreach (var e in boxes)
			{
				var b = e.Item1;
				var newb = e.Item2;
                //Debug.WriteLine($"D.Update {b} -> {newb}");
				newBoxes = newBoxes.Replace(b, newb);

				var q = from a in newArrows
						where a.StartBox == b || a.EndBox == b
						let na = a.UpdateBox(b, newb)
						select Tuple.Create(a, na);

				foreach (var aa in q)
				{
					newArrows = newArrows.Replace(aa.Item1, aa.Item2);
				}
			}
			return new Diagram(newBoxes, newArrows, Style);
		}

		public Tuple<Diagram, ImmutableArray<DragGuide>, ImmutableArray<Box>> MoveBoxes(ImmutableArray<Box> boxes, Point offset, bool snapToGuides, double minDist, TimeSpan maxTime)
		{
			if (boxes.Length == 0)
				return Tuple.Create(this, ImmutableArray<DragGuide>.Empty, boxes);
			
			var d = this;

			var b0 = boxes[0];
			var b0c = b0.Frame.Center + offset;

			var moveGuides =
				boxes.SelectMany(b => b.GetDragGuides(offset, Boxes.IndexOf(b)))
				     .ToImmutableArray();
			;
			var staticGuides =
				Boxes.Select((x, i) => Tuple.Create(x, i))
				     .Where(x => !boxes.Contains(x.Item1))
				     .OrderBy(x => x.Item1.Frame.Center.DistanceTo(b0c))
				     .SelectMany(x => x.Item1.GetDragGuides(Point.Zero, x.Item2))
					 .ToImmutableArray();

			var compares = new List<Tuple<DragGuide, DragGuide, double>>();
			foreach (var m in moveGuides)
			{
				foreach (var s in staticGuides)
				{
					if (m.CanCompareTo(s))
					{
						compares.Add(Tuple.Create(m, s, s.Offset - m.Offset));
					}
				}
			}

			//Debug.WriteLine($"{staticGuides.Length} STATIC GUIDES");
			//Debug.WriteLine($"{moveGuides.Length} MOVE GUIDES");
			//Debug.WriteLine($"{compares.Count} COMPARES");

			var vert =
				compares.Where(x => x.Item1.IsVertical && Math.Abs(x.Item3) < minDist)
				        .OrderBy(x => Math.Abs(x.Item3))
				        .FirstOrDefault()
				        ;

			var horz =
				compares.Where(x => !x.Item1.IsVertical && Math.Abs(x.Item3) < minDist)
						.OrderBy(x => Math.Abs(x.Item3))
				        .FirstOrDefault()
						;

			var snapOffset = Point.Zero;

			if (snapToGuides)
			{
				snapOffset = new Point(
					vert != null ? vert.Item3 : 0,
					horz != null ? horz.Item3 : 0);
			}

			var newBs = boxes
				.Select(b => Tuple.Create(b, b.Move(offset + snapOffset)))
				.ToImmutableArray();
			d = d.UpdateBoxes(newBs);
			var newBsI = newBs.Select(x => x.Item2).ToImmutableArray();

			d = d.PreventOverlaps(newBsI, offset, maxTime);

			var guides = new List<DragGuide>();
			if (snapToGuides)
			{
				if (vert != null)
				{
					var mf = d.Boxes[vert.Item1.Tag].Frame;
					var si = vert.Item2.Tag;
					var sf = d.Boxes[si].Frame;
					var g = d.Boxes[si].GetDragGuides(Point.Zero, si).First(x => x.Source == vert.Item2.Source);
					//Debug.WriteLine($"V G.S={g.Source}");
					guides.Add(g.Clip(sf.Union(mf)));
				}
				if (horz != null)
				{
					var mf = d.Boxes[horz.Item1.Tag].Frame;
					var si = horz.Item2.Tag;
					var sf = d.Boxes[si].Frame;
					var g = d.Boxes[si].GetDragGuides(Point.Zero, si).First(x => x.Source == horz.Item2.Source);
					//Debug.WriteLine($"H G.S={g.Source}");
					guides.Add(g.Clip(sf.Union(mf)));
				}
			}

			return Tuple.Create(d, guides.ToImmutableArray(), newBsI);
		}

        public ImmutableArray<Box> GetBoxes (IEnumerable<string> ids)
        {
            var s = new HashSet<string>(ids);
            return Boxes.Where(x => s.Contains(x.Id)).ToImmutableArray();
        }

		public Diagram PreventOverlaps(ImmutableArray<Box> staticBoxes, Point staticOffset, TimeSpan maxTime)
		{
			var d = this;
			var n = d.Boxes.Length;


			var iterChanged = true;
			var sw = new Stopwatch();
			sw.Start();
			var maxMillis = (long)maxTime.TotalMilliseconds;

			var offsets = d.Boxes.Select(x => Point.Zero).ToArray();
			var boxFrames = d.Boxes.Select(x => x.FrameWithMargin).ToArray();

			var staticBoxSet = staticBoxes
				.Select(x => d.Boxes.IndexOf(x))
				.ToImmutableHashSet();

			var quadtree = new Quadtree(1 << 16, 1 << 16, 16);
			for (var i = 0; i < n; i++)
			{
				var b = d.Boxes[i];
				quadtree.Add(i, b.Frame);
			}

			var iter = 0;
			while (iterChanged && sw.ElapsedMilliseconds < maxMillis)
			{
				iter++;
				iterChanged = false;
				for (var i = 0; i < n; i++)
				{
					var a = d.Boxes[i];
					int j;
					Point overlap;

					if (quadtree.GetOverlap(d.Boxes, offsets, i, boxFrames[i] + offsets[i], out j, out overlap))
					{
						var b = d.Boxes[j];

						if (staticBoxSet.Contains(i))
						{
							if (staticBoxSet.Contains(j))
							{
								// Nothing
							}
							else
							{
								iterChanged = true;
								var oldfj = boxFrames[j] + offsets[j];
								offsets[j] += new Point(overlap.X, overlap.Y);
								quadtree.Move(j, oldfj, boxFrames[j] + offsets[j]);
							}
						}
						else {
							if (staticBoxSet.Contains(j))
							{
								iterChanged = true;
								var oldfi = boxFrames[i] + offsets[i];
								offsets[i] -= new Point(overlap.X, overlap.Y);
								quadtree.Move(i, oldfi, boxFrames[i] + offsets[i]);
							}
							else
							{
								iterChanged = true;
								var oldfi = boxFrames[i] + offsets[i];
								var oldfj = boxFrames[j] + offsets[j];
								var ir = 0.5;
								var jr = 1.0 - ir;
								offsets[i] -= new Point(overlap.X * ir, overlap.Y * ir);
								offsets[j] += new Point(overlap.X * jr, overlap.Y * jr);
								quadtree.Move(i, oldfi, boxFrames[i] + offsets[i]);
								quadtree.Move(j, oldfj, boxFrames[j] + offsets[j]);
							}
						}

						//Debug.WriteLine($"I={iter}, OVERLAP {overlap}, C={iterChanged}");
					}
				}
			}

			sw.Stop();

			//Debug.WriteLine($"ITER={iter}, TIME={sw.Elapsed}");

			var newBs = d
				.Boxes
				.Select((b, i) => Tuple.Create(b, i))
				.Where(bt => Math.Abs(offsets[bt.Item2].X) > 1e-12 || Math.Abs(offsets[bt.Item2].Y) > 1e-12)
				.Select(bt => Tuple.Create(bt.Item1, bt.Item1.Move(offsets[bt.Item2])))
				.ToList();

			d = d.UpdateBoxes(newBs);

			return d;
		}

		public static Diagram Create(
			DiagramStyle style,
			IEnumerable<object> boxValues,
			Func<object, Box> getBox)
		{
			
			return Create(
				style,
				boxValues,
				Enumerable.Empty<object>(),
				getBox,
				(arg1, arg2) => { throw new InvalidOperationException(); });
		}

		public static Diagram Create(
			DiagramStyle style,
            IEnumerable<object> boxValues,
            IEnumerable<object> arrowValues,
            Func<object, Box> getBox,
            Func<Func<object, object, PortRef>, object, Arrow> getArrow)
        {
            var boxes = boxValues.Select(getBox).ToImmutableArray();

            var portIndex = new Dictionary<Tuple<object, object>, PortRef>();
            foreach (var b in boxes)
            {
                foreach (var p in b.Ports)
                {
                    portIndex[Tuple.Create(b.Value, p.Value)] = new PortRef (b, p);
                }
            }
            Func<object, object, PortRef> portF = (o, n) => portIndex[Tuple.Create(o, n)];

            var arrows = arrowValues.Select (o => getArrow(portF, o)).ToImmutableArray();

            return new Diagram(boxes, arrows, style);
		}

		public IEnumerable<Box> HitTestBoxes(Point point)
		{
			return Boxes.Where(x => x.HitTest(point)).Reverse().ToList();
		}

		public IEnumerable<Arrow> HitTestArrows(Point point, double viewToDiagramScale)
		{
			var maxDist = viewToDiagramScale * 22;
			var q = from a in Arrows
					let p = GetArrowPath (a)
					let d = p.DistanceTo(point)
	                where d < a.Style.LineWidth / 2 + maxDist
					orderby d ascending
					select a;
			return q.ToList();
		}

        public IEnumerable<Tuple<Box, Port>> HitTestPorts(Point point, double viewToDiagramScale)
        {
            var maxDist = viewToDiagramScale * 22;
            var q = from b in Boxes
                    from p in b.Ports
                    let pfr = p.GetFrame (b)
                    let d = pfr.Center.DistanceTo(point)
                    where d < maxDist
                    orderby d ascending
                    select Tuple.Create(b, p);
            return q.ToList();
        }

        public Path GetArrowPath(Arrow arrow)
		{
			var arrowIndex = Arrows.IndexOf(arrow);
			return Paths.ArrowPaths[arrowIndex].CurvedPath;
		}

		public Path GetDirectlyCurvedArrowPath(Arrow arrow)
		{
			var startCenter = arrow.StartBox.Frame.Center;
			var endCenter = arrow.EndBox.Frame.Center;

			Point s, e;

			s = arrow.Start.PortFrame.Center;
			e = arrow.End.PortFrame.Center;

			var sDir = (s - startCenter).Normalized;
			var eDir = (e - endCenter).Normalized;

			var dist = s.DistanceTo(e);
			var c1 = s + sDir * (dist / 3);
			var c2 = e + eDir * (dist / 3);

			var p = new Path();
			p.MoveTo(s);
			p.CurveTo(c1, c2, e);
			return p;
		}

	}

	public enum DragGuideSource
	{
		CenterV,
		CenterH,
		LeftEdge,
		RightEdge,
		TopEdge,
		BottomEdge,
		LeftMargin,
		RightMargin,
		TopMargin,
		BottomMargin,
		PortVS = 1000,
		PortVE = 1999,
		PortHS = 2000,
		PortHE = 2999,
	}

	public class DragGuide
	{
		public readonly Point Start;
		public readonly Point End;
		public readonly bool IsVertical;
		public readonly DragGuideSource Source;
		public readonly int Tag;
		public double Offset => IsVertical ? Start.X : Start.Y;
		public DragGuide (Point start, Point end, DragGuideSource source, int sourceFrame)
		{
			Start = start;
			End = end;
			Source = source;
			Tag = sourceFrame;
			IsVertical = Math.Abs(end.X - start.X) < Math.Abs(end.Y - start.Y);
		}
		public static DragGuide Vertical(double x, DragGuideSource source, int sourceFrame)
		{
			return new DragGuide(new Point(x, -1e12), new Point(x, 1e12), source, sourceFrame);
		}
		public static DragGuide Horizontal(double y, DragGuideSource source, int sourceFrame)
		{
			return new DragGuide(new Point(-1e12, y), new Point(1e12, y), source, sourceFrame);
		}
		public bool CanCompareTo(DragGuide o)
		{
			if (IsVertical != o.IsVertical) return false;
			return (Source == DragGuideSource.CenterV && o.Source == DragGuideSource.CenterV)
				|| (Source == DragGuideSource.CenterH && o.Source == DragGuideSource.CenterH)
				|| (Source >= DragGuideSource.PortVS && DragGuideSource.PortVE >= Source &&
				    o.Source >= DragGuideSource.PortVS && DragGuideSource.PortVE >= o.Source)
				|| (Source >= DragGuideSource.PortHS && DragGuideSource.PortHE >= Source &&
					o.Source >= DragGuideSource.PortHS && DragGuideSource.PortHE >= o.Source)
				|| (Source == DragGuideSource.LeftMargin && o.Source == DragGuideSource.RightEdge)
				|| (Source == DragGuideSource.RightMargin && o.Source == DragGuideSource.LeftEdge)
				|| (Source == DragGuideSource.TopMargin && o.Source == DragGuideSource.BottomEdge)
				|| (Source == DragGuideSource.BottomMargin && o.Source == DragGuideSource.TopEdge)
				;

		}
		public DragGuide Clip(Rect clipRect)
		{
			var s = Start;
			var e = End;
			if (IsVertical)
			{
				var mn = Math.Max(Math.Min(Start.Y, End.Y), clipRect.Top);
				var mx = Math.Min(Math.Max(Start.Y, End.Y), clipRect.Bottom);
				s = new Point(s.X, mn);
				e = new Point(s.X, mx);
			}
			else
			{
				var mn = Math.Max(Math.Min(Start.X, End.X), clipRect.Left);
				var mx = Math.Min(Math.Max(Start.X, End.X), clipRect.Right);
				s = new Point(mn, s.Y);
				e = new Point(mx, s.Y);
			}
			return new DragGuide(s, e, Source, Tag);				
		}
		public override bool Equals(object obj)
		{
			var o = obj as DragGuide;
			if (o == null) return false;
			return (IsVertical == o.IsVertical && Source == o.Source && Math.Abs(Offset - o.Offset) < 1e-12);
		}
		public override int GetHashCode()
		{
			return IsVertical.GetHashCode () 
				             + 573259391 * Source.GetHashCode()
							 + 373587883 * Offset.GetHashCode();
		}
		public override string ToString()
		{
			return $"{(IsVertical?"Vertical":"Horizontal")} {Source} @ {Offset}";
		}
	}

	public class DiagramStyle
	{
		public readonly Color BackgroundColor;
		public readonly Color HandleBackgroundColor;
		public readonly Color HandleBorderColor;
		public readonly Color HoverSelectionColor;
		public readonly Color DragGuideColor;

		public readonly double DragHandleDistance;

		public static readonly DiagramStyle Default = new DiagramStyle(
			Color.FromWhite(236/255.0, 1),
			Colors.White,
			Colors.Black,
			new Color("#45C0FE"),
			new Color("#FF2600"),
			22.0);

		public DiagramStyle(
			Color backgroundColor, 
			Color handleBackgroundColor, 
			Color handleBorderColor, 
			Color hoverSelectionColor,
			Color dragGuideColor,
			double dragHandleDistance)
		{
			BackgroundColor = backgroundColor;
			HandleBackgroundColor = handleBackgroundColor;
			HandleBorderColor = handleBorderColor;
			HoverSelectionColor = hoverSelectionColor;
			DragGuideColor = dragGuideColor;
			DragHandleDistance = dragHandleDistance;
		}

		public DiagramStyle WithBackgroundColor(Color color)
		{
			return new DiagramStyle(color, HandleBackgroundColor, HandleBorderColor, HoverSelectionColor, DragGuideColor, DragHandleDistance);
		}

		public DiagramStyle WithDragHandleDistance(double distance)
		{
			return new DiagramStyle(BackgroundColor, HandleBackgroundColor, HandleBorderColor, HoverSelectionColor, DragGuideColor, distance);
		}
	}
}
