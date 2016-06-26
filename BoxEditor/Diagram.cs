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

        public static Diagram Empty =
			new Diagram(ImmutableArray<Box>.Empty, ImmutableArray<Arrow>.Empty, DiagramStyle.Default);

		public Diagram(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows, DiagramStyle style)
        {
            Boxes = boxes;
            Arrows = arrows;
			Style = style;
        }

		public Diagram WithBoxes(ImmutableArray<Box> newBoxes)
		{
			return new Diagram(newBoxes, Arrows, Style);
		}

		public Diagram WithArrows(ImmutableArray<Arrow> newArrows)
		{
			return new Diagram(Boxes, newArrows, Style);
		}

		public Diagram UpdateBox(Box b, Box newb)
		{
			var newBoxes = Boxes.Replace(b, newb);

			var q = from a in Arrows
					where a.StartBox == b || a.EndBox == b
		               let na = a.UpdateBox (b, newb)
		               select Tuple.Create(a, na);

			var newArrows = Arrows;
			foreach (var aa in q)
			{
				newArrows = newArrows.Replace(aa.Item1, aa.Item2);
			}

			return new Diagram(newBoxes, newArrows, Style);
		}

		public Tuple<Diagram, ImmutableArray<DragGuide>, ImmutableArray<Box>> MoveBoxes(ImmutableArray<Box> boxes, Point offset)
		{
			var d = this;

			var staticGuides =
				Boxes.Where(x => !boxes.Contains(x))
				     .SelectMany(b => b.GetDragGuides(Point.Zero))
					 .Distinct()
					 .ToImmutableArray();
			var moveGuides =
				boxes.SelectMany(b => b.GetDragGuides(offset))
				     .Distinct()
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

			var minDist = 8.0;

			var verts =
				compares.Where(x => x.Item1.IsVertical && Math.Abs(x.Item3) < minDist)
				        .OrderBy(x => Math.Abs(x.Item3))
				        .Take(1)
				        .ToList()
				        ;

			var horzs =
				compares.Where(x => !x.Item1.IsVertical && Math.Abs(x.Item3) < minDist)
						.OrderBy(x => Math.Abs(x.Item3))
						.Take(1)
				        .ToList()
						;
			
			var snapOffset = new Point(
				verts.Count > 0 ? verts[0].Item3 : 0,
				horzs.Count > 0 ? horzs[0].Item3 : 0);

			var guides = verts.Select(x => x.Item2)
			                  .Concat(horzs.Select(x => x.Item2))
			                  .ToImmutableArray();

			var newBs = new List<Box>();
			foreach (var b in boxes)
			{
				var nb = b.Move(offset + snapOffset);
				d = d.UpdateBox(b, nb);
				newBs.Add(nb);
			}
			var newBsI = newBs.ToImmutableArray();

			d = d.PreventOverlaps(newBsI, offset);

			return Tuple.Create(d, guides, newBsI);
		}

		public Diagram PreventOverlaps(ImmutableArray<Box> staticBoxes, Point staticOffset)
		{
			var d = this;
			var n = d.Boxes.Length;

			var iterChanged = true;
			var maxIters = 100;

			var offsets = d.Boxes.Select(x => Point.Zero).ToList();

			for (var iter = 0; iter < maxIters && iterChanged; iter++)
			{
				iterChanged = false;
				for (var i = 0; i < n; i++)
				{
					var a = d.Boxes[i];

					for (var j = i + 1; j < n; j++)
					{
						var b = d.Boxes[j];

						var overlap = GetMarginOverlap(a, offsets[i], b, offsets[j], staticOffset);

						if (Math.Abs(overlap.X) < 1e-5 && Math.Abs(overlap.Y) < 1e-5) continue;

						if (staticBoxes.Contains(a))
						{
							if (staticBoxes.Contains(b))
							{
								// Nothing
							}
							else
							{
								iterChanged = true;
								var dx = a.Frame.Center.X < b.Frame.Center.X ? 1 : -1;
								var dy = a.Frame.Center.Y < b.Frame.Center.Y ? 1 : -1;
								offsets[j] += new Point(dx * overlap.X, dy * overlap.Y);
							}
						}
						else {
							if (staticBoxes.Contains(b))
							{
								iterChanged = true;
								var dx = a.Frame.Center.X < b.Frame.Center.X ? -1 : 1;
								var dy = a.Frame.Center.Y < b.Frame.Center.Y ? -1 : 1;
								offsets[i] += new Point(dx * overlap.X, dy * overlap.Y);
							}
							else
							{
								iterChanged = true;
								var dx = a.Frame.Center.X < b.Frame.Center.X ? -1 : 1;
								var dy = a.Frame.Center.Y < b.Frame.Center.Y ? -1 : 1;
								offsets[i] += new Point(dx * overlap.X / 2, dy * overlap.Y / 2);
								offsets[j] -= new Point(dx * overlap.X / 2, dy * overlap.Y / 2);
							}
						}

						//Debug.WriteLine($"I={iter}, OVERLAP {overlap}, C={iterChanged}");
					}
				}
			}

			for (var i = 0; i < n; i++)
			{
				var b = d.Boxes[i];
				var o = offsets[i];
				if (Math.Abs(o.X) > 1e-12 || Math.Abs(o.Y) > 1e-12)
				{
					d = d.UpdateBox(b, b.Move(o));
				}
			}

			return d;
		}

		Point GetMarginOverlap(Box a, Point ao, Box b, Point bo, Point offset)
		{
			var maxMargin = new Size(Math.Max(a.Style.Margin.Width, b.Style.Margin.Width),
									 Math.Max(a.Style.Margin.Height, b.Style.Margin.Height));
			var amr = a.Frame.GetInflated(maxMargin / 2) + ao;
			var bmr = b.Frame.GetInflated(maxMargin / 2) + bo;
			if (amr.Intersects(bmr))
			{
				var dx1 = bmr.Right - amr.Left;
				var dx2 = amr.Right - bmr.Left;
				var dx = (Math.Abs(dx1) <= Math.Abs(dx2)) ? dx1 : dx2;
				var dy1 = bmr.Bottom - amr.Top;
				var dy2 = amr.Bottom - bmr.Top;
				var dy = (Math.Abs(dy1) <= Math.Abs(dy2)) ? dy1 : dy2;
				if (Math.Abs(dx) <= Math.Abs(dy))
				{
					dy = 0;
				}
				else
				{
					dx = 0;
				};
				//Debug.WriteLine($"{DateTime.Now} DX = {dx} AMR = {amr}");
				return new Point(dx, dy);
			}
			else {
				return Point.Zero;
			}
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
					let p = a.GetPath()
					let d = p.DistanceTo(point)
	                where d < a.Style.LineWidth / 2 + maxDist
					orderby d ascending
					select a;
			return q.ToList();
		}
	}

	public enum DragGuideSource
	{
		Center,
		Port,
		LeftEdge,
		RightEdge,
		TopEdge,
		BottomEdge,
		LeftMargin,
		RightMargin,
		TopMargin,
		BottomMargin,
	}

	public class DragGuide
	{
		public readonly Point Start;
		public readonly Point End;
		public readonly bool IsVertical;
		public readonly DragGuideSource Source;
		public double Offset => IsVertical ? Start.X : Start.Y;
		public DragGuide (Point start, Point end, DragGuideSource source)
		{
			Start = start;
			End = end;
			Source = source;
			IsVertical = Math.Abs(end.X - start.X) < Math.Abs(end.Y - start.Y);
		}
		public static DragGuide Vertical(double x, DragGuideSource source)
		{
			return new DragGuide(new Point(x, -1e12), new Point(x, 1e12), source);
		}
		public static DragGuide Horizontal(double y, DragGuideSource source)
		{
			return new DragGuide(new Point(-1e12, y), new Point(1e12, y), source);
		}
		public bool CanCompareTo(DragGuide o)
		{
			if (IsVertical != o.IsVertical) return false;
			return (Source == DragGuideSource.Center && o.Source == DragGuideSource.Center)
				|| (Source == DragGuideSource.Port && o.Source == DragGuideSource.Port)
				|| (Source == DragGuideSource.LeftMargin && o.Source == DragGuideSource.RightEdge)
				|| (Source == DragGuideSource.RightMargin && o.Source == DragGuideSource.LeftEdge)
				|| (Source == DragGuideSource.TopMargin && o.Source == DragGuideSource.BottomEdge)
				|| (Source == DragGuideSource.BottomMargin && o.Source == DragGuideSource.TopEdge)
				;

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
	}
}
