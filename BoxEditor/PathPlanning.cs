using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using NGraphics;

namespace BoxEditor
{
	public static class PathPlanning
	{
		public static DiagramPaths Plan(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows)
		{
			var planner = new VisibilityPlanner();
			return planner.Plan(boxes, arrows);
		}
	}

	public class DiagramPaths
	{
		public readonly ImmutableArray<PlannedPath> ArrowPaths;
		public readonly ImmutableArray<IDrawable> DebugDrawings;
		public DiagramPaths(ImmutableArray<PlannedPath> arrowPaths, ImmutableArray<IDrawable> debugDrawings)
		{
			ArrowPaths = arrowPaths;
			DebugDrawings = debugDrawings;
		}
	}

	public class PlannedPath
	{
		public readonly ImmutableArray<Point> Points;
		public PlannedPath(ImmutableArray<Point> points)
		{
			Points = points;
		}
	}

	public interface IPathPlanner
	{
		DiagramPaths Plan(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows);
	}

	public class DumbPlanner : IPathPlanner
	{
		public DiagramPaths Plan(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows)
		{
			var q = arrows
				.Select(a =>
				{
					var points = new[] {
						a.Start.PortFrame.Center,
						a.End.PortFrame.Center,
					};
					return new PlannedPath (points.ToImmutableArray());
				});
			return new DiagramPaths(q.ToImmutableArray(), ImmutableArray<IDrawable>.Empty);
		}
	}

	public class VisibilityPlanner : IPathPlanner
	{
		public DiagramPaths Plan(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows)
		{
			var arrowPaths = new List<PlannedPath>();
			var debugs = new List<IDrawable>();

			var vmap = new VisibilityMap(boxes);
			var graph = new Graph(vmap);

			foreach (var b in boxes)
			{
				var bb = b.PortBoundingBox;
				var mb = bb.GetInflated(b.Style.Margin);
				graph.AddVertex(mb.TopLeft);
				graph.AddVertex(mb.TopRight);
				graph.AddVertex(mb.BottomLeft);
				graph.AddVertex(mb.BottomRight);
			}
			foreach (var a in arrows)
			{
				var sp = a.Start.PortPoint;
				var ep = a.End.PortPoint;

				//
				// Ignore the box if the port is actually
				// inside the bounding box
				//
				var sbb = a.StartBox.PortBoundingBox;
				sbb.Inflate(-sbb.Size * 0.005);
				var sIgBox = sbb.Contains(sp) ? boxes.IndexOf(a.StartBox) : -1;

				var ebb = a.EndBox.PortBoundingBox;
				ebb.Inflate(-ebb.Size * 0.005);
				var eIgBox = ebb.Contains(ep) ? boxes.IndexOf(a.EndBox) : -1;

				//
				// Add the nodes ready for planning
				//
				var startNode = graph.AddVertex(sp, sIgBox);
				var endNode = graph.AddVertex(ep, eIgBox);

				var nodePath = AStar(graph, startNode, endNode);

				graph.RemoveVertex(startNode);
				graph.RemoveVertex(endNode);

				var points = nodePath.Select(n => n.Point);
				var pp = new PlannedPath(points.ToImmutableArray());
				arrowPaths.Add(pp);
			}
			debugs.AddRange(graph.Vertices.Select(n =>
			{
				var c = n.IgnoreBox >= 0 ? Colors.Red : Colors.Blue;
				return new Ellipse(n.Point-new Size(4), new Size(8), brush: new SolidBrush(c));
			}));

			return new DiagramPaths(arrowPaths.ToImmutableArray(), debugs.ToImmutableArray());
		}

		/// <summary>
		/// "Theta*: Any-Angle Path Planning on Grids"
		/// - Kenny Daniel, Alex Nash, Sven Koenig - University of Southern California
		/// </summary>
		List<Vertex> AStar(Graph graph, Vertex startVert, Vertex endVert)
		{
			//
			// Initialize
			//
			graph.InitializeAStar(endVert);
			startVert.G = 0;
			startVert.Parent = startVert;

			//
			// Loop
			//

			//
			// Safety...
			//
			if (endVert.Parent == null)
			{
				endVert.Parent = startVert;
			}

			//
			// Walk backwards to get the path
			//
			var r = new List<Vertex>();
			var rn = endVert;
			while (rn != null && rn != startVert)
			{
				r.Add(rn);
				rn = rn.Parent;
			}
			r.Add(startVert);
			r.Reverse();
			return r;
		}

		class Vertex
		{
			public readonly Point Point;
			public readonly int IgnoreBox;

			//
			// Retained between A* runs
			//
			public List<Vertex> Neighbors;

			//
			// Reset for each A* run
			//

			/// <summary>
			/// Preview vertex on the path.
			/// </summary>
			public Vertex Parent;
			/// <summary>
			/// Accumulated distance along the path.
			/// </summary>
			public double G;
			/// <summary>
			/// Distance to the goal.
			/// </summary>
			public double H;
			/// <summary>
			/// True if a member of the closed set.
			/// </summary>
			public bool IsClosed;

			public Vertex(Point point, int ignoreBox)
			{
				Point = point;
				IgnoreBox = ignoreBox;
			}
		}

		class Graph
		{
			readonly VisibilityMap vmap;
			public Graph(VisibilityMap vmap)
			{
				this.vmap = vmap;
			}
			public readonly List<Vertex> Vertices = new List<Vertex>();
			public Vertex AddVertex(Point p, int ignoreBox = -1)
			{
				//
				// Add it to the list
				//
				var vert = new Vertex(p, ignoreBox);

				//
				// Calculate its visibility
				//
				foreach (var n in Vertices)
				{
					if (n.Neighbors == null) continue;
					if (vmap.LineOfSight(n.Point, vert.Point))
					{
						n.Neighbors.Add(vert);
					}
				}

				Vertices.Add(vert);
				return vert;
			}
			public void RemoveVertex(Vertex vert)
			{
				Vertices.Remove(vert);
				foreach (var n in Vertices)
				{
					n.Neighbors?.Remove(vert);
					if (n.Parent == vert) n.Parent = null;
				}
			}
			public void InitializeAStar(Vertex endVert)
			{
				foreach (var v in Vertices)
				{
					v.Parent = null;
					v.G = double.MaxValue;
					v.H = v.Point.DistanceTo(endVert.Point);
					v.IsClosed = false;
				}
			}
		}

		class VisibilityMap
		{
			readonly Point[] bounds;

			public VisibilityMap(ImmutableArray<Box> boxes)
			{
				bounds = new Point[boxes.Length * 2];
				for (int i = 0; i < boxes.Length; i++)
				{
					var b = boxes[i];
					var bb = b.PortBoundingBox;
					bounds[i * 2] = bb.TopLeft;
					bounds[i * 2 + 1] = bb.BottomRight;
				}
			}

			/// <summary>
			/// "An Efficient and Robust Ray–Box Intersection Algorithm"
			/// - Amy Williams, Steve Barrus, R. Keith Morley, Peter Shirley - University of Utah
			/// </summary>
			public bool LineOfSight(Point origin, Point destination)
			{
				//
				// Cached ray properties
				//
				var delta = (destination - origin);
				var distance = delta.Distance;
				if (distance < 1e-12) return true;

				var direction = delta * (1 / distance);
				var invDirection = new Point(1 / direction.X, 1 / direction.Y);
				var sign0 = invDirection.X < 0 ? 1 : 0;
				var sign1 = invDirection.Y < 0 ? 1 : 0;

				//
				// Go through the boxes...
				//
				for (var i = 0; i < bounds.Length; i += 2)
				{
					var tmin = (bounds[i + sign0].X - origin.X) * invDirection.X;
					var tmax = (bounds[i + 1 - sign0].X - origin.X) * invDirection.X;
					var tymin = (bounds[i + sign1].Y - origin.Y) * invDirection.Y;
					var tymax = (bounds[i + 1 - sign1].Y - origin.X) * invDirection.Y;

					var isect = false;
					if ((tmin > tymax) || (tymin > tmax))
					{
						//isect = false;
					}
					else {
						if (tymin > tmin) tmin = tymin;
						if (tymax < tmax) tmax = tymax;
						isect = (tmin < 1 && tmax > 0);
					}

					if (isect)
					{
						Debug.WriteLine($"ISECT [{bounds[i]},{bounds[i + 1]}] with <{origin},{destination}>");
						return false;
					}
				}

				// No intersections
				return true;
			}
		}
	}
}

