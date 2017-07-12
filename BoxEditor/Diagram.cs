﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NGraphics;

namespace BoxEditor
{
    public class Diagram
    {
        public readonly IList<Box> Boxes;
        public readonly IList<Arrow> Arrows;
		public readonly DiagramStyle Style;
		public readonly DiagramPaths Paths;
		public readonly double DragHandleDistance;

        public Diagram()
            : this (DiagramStyle.Default)
        {
        }

		public Diagram(DiagramStyle style)
        {
            Boxes = new ObservableCollection<Box> ();
            Arrows = new ObservableCollection<Arrow>();
			Style = style;
            Paths = PathPlanning.Plan(ImmutableArray<Box>.Empty, ImmutableArray<Arrow>.Empty);
        }

		public void UpdateBoxFrames(IEnumerable<Tuple<Box, Rect>> boxes)
		{
			foreach (var e in boxes)
			{
				var b = e.Item1;
				var newf = e.Item2;
				b.Frame = newf;
			}
		}

        public void DragBoxHandle(Box box, int handle, Point offset)
        {
        }

        public List<DragGuide> DragBoxes(Dictionary<Box, Rect> startFrames, List<Box> boxes, Point offset, bool snapToGuides, double minDist, TimeSpan maxTime)
		{
			if (boxes.Count == 0)
				return new List<DragGuide>();

            foreach (var f in startFrames)
            {
                f.Key.Frame = f.Value;
            }
			
			var d = this;

			var b0 = boxes.First();
            var b0c = b0.Frame.Center + offset;

			var moveGuides =
                boxes.SelectMany(b => b.GetDragGuides(b.Frame, offset, Boxes.IndexOf(b)))
				     .ToImmutableArray();
			
			var staticGuides =
				Boxes
                    .Select((x, i) => (Box: x, Index: i))
                    .Where(x => !boxes.Contains(x.Box))
                    .OrderBy(x => x.Box.Frame.Center.DistanceTo(b0c))
                    .SelectMany(x => x.Box.GetDragGuides(x.Box.Frame, Point.Zero, x.Index))
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

            var dragOffsets = boxes
                .Select(b => (b, b.Frame + (offset + snapOffset)))
				.ToList();
            foreach (var b in dragOffsets)
			{
				b.Item1.Frame = b.Item2;
			}

            var overlapOffsets = PreventOverlaps(dragOffsets, offset, maxTime);
			foreach (var b in overlapOffsets)
			{
				b.Item1.Frame = b.Item2;
			}

			var guides = new List<DragGuide>();
			if (snapToGuides)
			{
				if (vert != null)
				{
					var mf = d.Boxes[vert.Item1.Tag].Frame;
					var si = vert.Item2.Tag;
					var sf = d.Boxes[si].Frame;
                    var g = d.Boxes[si].GetDragGuides(d.Boxes[si].Frame, Point.Zero, si).First(x => x.Source == vert.Item2.Source);
					//Debug.WriteLine($"V G.S={g.Source}");
					guides.Add(g.Clip(sf.Union(mf)));
				}
				if (horz != null)
				{
					var mf = d.Boxes[horz.Item1.Tag].Frame;
					var si = horz.Item2.Tag;
					var sf = d.Boxes[si].Frame;
                    var g = d.Boxes[si].GetDragGuides(d.Boxes[si].Frame, Point.Zero, si).First(x => x.Source == horz.Item2.Source);
					//Debug.WriteLine($"H G.S={g.Source}");
					guides.Add(g.Clip(sf.Union(mf)));
				}
			}

			return guides;
		}

        List<(Box, Rect)> PreventOverlaps(List<(Box, Rect)> staticBoxes, Point staticOffset, TimeSpan maxTime)
		{
			var n = Boxes.Count;

			var iterChanged = true;
			var sw = new Stopwatch();
			sw.Start();
			var maxMillis = (long)maxTime.TotalMilliseconds;

			var offsets = Boxes.Select(x => Point.Zero).ToArray();
			var boxFrames = Boxes.Select(x => x.FrameWithMargin).ToArray();

			var staticBoxSet = staticBoxes
				.Select(x => Boxes.IndexOf(x.Item1))
				.ToImmutableHashSet();

			var quadtree = new Quadtree(1 << 16, 1 << 16, 16);
			for (var i = 0; i < n; i++)
			{
				var b = Boxes[i];
				quadtree.Add(i, b.Frame);
			}

			var iter = 0;
			while (iterChanged && sw.ElapsedMilliseconds < maxMillis)
			{
				iter++;
				iterChanged = false;
				for (var i = 0; i < n; i++)
				{
					var a = Boxes[i];
					int j;
					Point overlap;

					if (quadtree.GetOverlap(Boxes, offsets, i, boxFrames[i] + offsets[i], out j, out overlap))
					{
						var b = Boxes[j];

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

			return Boxes
				.Select((b, i) => (Box: b, Index: i))
				.Where(bt => Math.Abs(offsets[bt.Index].X) > 1e-12 || Math.Abs(offsets[bt.Index].Y) > 1e-12)
				.Select(bt => (bt.Box, new Rect(bt.Box.Frame.Position + offsets[bt.Index], bt.Box.Frame.Size)))
				.ToList();
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

		public Path GetArrowPath(Arrow arrow)
		{
			var arrowIndex = Arrows.IndexOf(arrow);
            if (arrowIndex < 0 || arrowIndex >= Paths.ArrowPaths.Length)
                return new Path();
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



		public static readonly DiagramStyle Default = new DiagramStyle(
			Color.FromWhite(236/255.0, 1),
			Colors.White,
			Colors.Black,
			new Color("#45C0FE"),
			new Color("#FF2600"));

		public DiagramStyle(
			Color backgroundColor, 
			Color handleBackgroundColor, 
			Color handleBorderColor, 
			Color hoverSelectionColor,
			Color dragGuideColor)
		{
			BackgroundColor = backgroundColor;
			HandleBackgroundColor = handleBackgroundColor;
			HandleBorderColor = handleBorderColor;
			HoverSelectionColor = hoverSelectionColor;
			DragGuideColor = dragGuideColor;
		}

		public DiagramStyle WithBackgroundColor(Color color)
		{
			return new DiagramStyle(color, HandleBackgroundColor, HandleBorderColor, HoverSelectionColor, DragGuideColor);
		}
	}
}
