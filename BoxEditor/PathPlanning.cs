using System;
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
		public DiagramPaths (ImmutableArray<PlannedPath> arrowPaths)
		{
			ArrowPaths = arrowPaths;
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
			var planner = new DumbPlanner();
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
			return new DiagramPaths(q.ToImmutableArray());
		}
	}

	public class SmartPlanner : IPathPlanner
	{
		public DiagramPaths Plan(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows)
		{
			throw new NotImplementedException();
		}
	}

	public class ObstacleMap
	{
		public bool Visible(Point start, Point target, int ignoreId = int.MaxValue)
		{
			throw new NotImplementedException();
		}
	}

}

