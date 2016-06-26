using System;
using System.Collections.Immutable;
using NGraphics;

namespace BoxEditor
{
	public class ObstacleMap
	{
		public bool Visible(Point start, Point target, int ignoreId = int.MaxValue)
		{
			throw new NotImplementedException();
		}
	}

	public class PlannedPath
	{
		public readonly ImmutableArray<Point> Points;
	}

	public class PathPlan
	{
		public readonly ImmutableArray<PlannedPath> ArrowPaths;
	}

	public interface IPathPlanner
	{
		PathPlan Plan(Diagram diagram);
	}

	public class SmartPlanner : IPathPlanner
	{
		public PathPlan Plan(Diagram diagram)
		{
			throw new NotImplementedException();
		}
	}

	public class DumbPlanner : IPathPlanner
	{
		public PathPlan Plan(Diagram diagram)
		{
			throw new NotImplementedException();
		}
	}

	public static class PathPlanning
	{
		public static PathPlan Plan(Diagram diagram)
		{
			var planner = new DumbPlanner();
			return planner.Plan(diagram);
		}
	}
}

