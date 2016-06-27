using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using NGraphics;

namespace BoxEditor
{
	public class PlannedPath
	{
		public readonly ImmutableArray<Point> Points;
		public PlannedPath(ImmutableArray<Point> points)
		{
			Points = points;
		}
	}

	public class DiagramPaths
	{
		public readonly ImmutableArray<PlannedPath> ArrowPaths;
		public readonly ImmutableArray<IDrawable> DebugDrawings;
		public DiagramPaths (ImmutableArray<PlannedPath> arrowPaths, ImmutableArray<IDrawable> debugDrawings)
		{
			ArrowPaths = arrowPaths;
			DebugDrawings = debugDrawings;
		}
	}

	public interface IPathPlanner
	{
		DiagramPaths Plan(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows);
	}

	public static class PathPlanning
	{
		public static DiagramPaths Plan(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows)
		{
			var planner = new VisibilityPlanner();
			return planner.Plan(boxes, arrows);
		}
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
		class Node
		{
			public readonly Point Point;
			public readonly int IgnoreBox;
			public List<Node> Neighbors;
			public Node(Point point, int ignoreBox)
			{
				Point = point;
				IgnoreBox = ignoreBox;
			}
		}

		class NodeMap
		{
			readonly VisibilityMap vmap;
			public NodeMap(VisibilityMap vmap)
			{
				this.vmap = vmap;
			}
			public readonly List<Node> Nodes = new List<Node>();
			public Node AddNode(Point p, int ignoreBox = -1)
			{
				//
				// Add it to the list
				//
				var node = new Node(p, ignoreBox);

				//
				// Calculate its visibility
				//
				foreach (var n in Nodes)
				{
					if (n.Neighbors == null) continue;
					if (vmap.LineOfSight(n.Point, node.Point))
					{
						n.Neighbors.Add(node);
					}
				}

				Nodes.Add(node);
				return node;
			}
			public void RemoveNode(Node node)
			{
				Nodes.Remove(node);
				foreach (var n in Nodes)
				{
					n.Neighbors?.Remove(node);
				}
			}
		}

		class VisibilityMap
		{
			readonly ImmutableArray<Box> boxes;
			readonly Point[] bounds;
			public VisibilityMap(ImmutableArray<Box> boxes)
			{
				this.boxes = boxes;
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
			/// - Amy Williams Steve Barrus R. Keith Morley Peter Shirley - University of Utah
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
						Debug.WriteLine($"ISECT [{bounds[i]},{bounds[i+1]}] with <{origin},{destination}>");
						return false;
					}
				}

				// No intersections
				return true;
			}
		}

		public DiagramPaths Plan(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows)
		{
			var arrowPaths = new List<PlannedPath>();
			var debugs = new List<IDrawable>();

			var vmap = new VisibilityMap(boxes);
			var nodes = new NodeMap(vmap);

			foreach (var b in boxes)
			{
				var bb = b.PortBoundingBox;
				var mb = bb.GetInflated(b.Style.Margin);
				nodes.AddNode(mb.TopLeft);
				nodes.AddNode(mb.TopRight);
				nodes.AddNode(mb.BottomLeft);
				nodes.AddNode(mb.BottomRight);
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
				var startNode = nodes.AddNode(sp, sIgBox);
				var endNode = nodes.AddNode(ep, eIgBox);

				PlanPath(startNode, endNode);

				//nodes.RemoveNode(startNode);
				//nodes.RemoveNode(endNode);

				var points = new[] { sp, ep };
				var pp = new PlannedPath(points.ToImmutableArray());
				arrowPaths.Add(pp);
			}
			debugs.AddRange(nodes.Nodes.Select(n =>
			{
				var c = n.IgnoreBox >= 0 ? Colors.Red : Colors.Blue;
				return new Ellipse(n.Point-new Size(4), new Size(8), brush: new SolidBrush(c));
			}));

			return new DiagramPaths(arrowPaths.ToImmutableArray(), debugs.ToImmutableArray());
		}

		void PlanPath(Node startNode, Node endNode)
		{
			//throw new NotImplementedException();
		}
	}
}

