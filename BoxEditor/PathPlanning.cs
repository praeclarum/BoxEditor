using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
			ImmutableArray<Box> boxes;
			public VisibilityMap(ImmutableArray<Box> boxes)
			{
				this.boxes = boxes;
			}
			public bool LineOfSight(Point start, Point target)
			{
				throw new NotImplementedException();
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

