﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using NGraphics;
using Priority_Queue;

namespace BoxEditor
{
	public static class PathPlanning
	{
		public static DiagramPaths Plan(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows)
		{
			if (arrows.Length == 0)
				return DiagramPaths.Empty;
			
			var planner = 0 < boxes.Length && boxes.Length < 100
				? (IPathPlanner)new VisibilityPlanner()
				: new DumbPlanner();
			
			return planner.Plan(boxes, arrows);
		}
	}

	public class DiagramPaths
	{
		public readonly ImmutableArray<PlannedPath> ArrowPaths;
		public readonly ImmutableArray<IDrawable> DebugDrawings;
		public static readonly DiagramPaths Empty = new DiagramPaths(
			ImmutableArray<PlannedPath>.Empty,
			ImmutableArray<IDrawable>.Empty);
		public DiagramPaths(ImmutableArray<PlannedPath> arrowPaths, ImmutableArray<IDrawable> debugDrawings)
		{
			ArrowPaths = arrowPaths;
			DebugDrawings = debugDrawings;
		}
	}

	public class PlannedPath
	{
		public readonly ImmutableArray<Point> Points;

		public readonly Path CurvedPath;

		public PlannedPath(ImmutableArray<Point> points, Point startDir, Point endDir)
		{
			Points = points;

			CurvedPath = CreateCurvedPath(startDir, endDir);
		}

		/// <summary>
		/// Creates the curved path for the segmented path.
		/// See http://www.ibiblio.org/e-notes/Splines/Cardinal.htm
		/// </summary>
		Path CreateCurvedPath(Point startDir, Point endDir)
		{
			var alpha = 2.0;
			var p = new Path();
			var n = Points.Length;
			if (n > 1)
			{
				p.MoveTo(Points[0]);

				var pd0 = (Points[1] - Points[0]) / alpha;
				if (startDir.DistanceSquared > 0.9)
				{
					pd0 = pd0.Distance * startDir;
				}

				var pdn = (Points[n - 1] - Points[n - 2]) / alpha;
				if (endDir.DistanceSquared > 0.9)
				{
					pdn = -pdn.Distance * endDir;
				}

				for (var i = 0; i < n - 1; i++)
				{
					var pi = Points[i];
					var pi1 = Points[i + 1];

					var pdi = i > 0 ? (pi1 - Points[i - 1]) / alpha : pd0;
					var pdi1 = i + 2 < n ? (Points[i + 2] - pi) / alpha : pdn;

					var b1 = pdi / 3.0 + pi;
					var b2 = -pdi1 / 3.0 + pi1;

					p.CurveTo(b1, b2, pi1);
				}
			}
			return p;
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
					return new PlannedPath (points.ToImmutableArray(), a.Start.Port.Direction, a.End.Port.Direction);
				});
			return new DiagramPaths(q.ToImmutableArray(), ImmutableArray<IDrawable>.Empty);
		}
	}

	public class VisibilityPlanner : IPathPlanner
	{
        public double MaxNeighborDistance = double.MaxValue;

        public DiagramPaths Plan(ImmutableArray<Box> boxes, ImmutableArray<Arrow> arrows)
		{
			var arrowPaths = new List<PlannedPath>();

			var vmap = new VisibilityMap(boxes);
			var graph = new Graph(vmap, MaxNeighborDistance);

			foreach (var b in boxes)
			{
				var bb = b.PreventOverlapFrame;
				if (bb.Width > 1e-7 && bb.Height > 1e-7)
				{
					var mb = bb.GetInflated(b.Style.Margin);
					graph.AddVertex(mb.TopLeft);
					graph.AddVertex(mb.TopRight);
					graph.AddVertex(mb.BottomLeft);
					graph.AddVertex(mb.BottomRight);
				}
			}

			var astarOpenQueue = new FastPriorityQueue<Vertex>(graph.Vertices.Count + 2);

			foreach (var a in arrows)
			{
				var sp = a.Start.PortPoint;
				var ep = a.End.PortPoint;

				//
				// Ignore the box if the port is actually
				// inside the bounding box
				//
				var sbb = a.StartBox.PreventOverlapFrame;
				sbb.Inflate(-sbb.Size * 0.005);
				var sIgBox = sbb.Contains(sp) ? boxes.IndexOf(a.StartBox) : -1;

				var ebb = a.EndBox.PreventOverlapFrame;
				ebb.Inflate(-ebb.Size * 0.005);
				var eIgBox = ebb.Contains(ep) ? boxes.IndexOf(a.EndBox) : -1;

				//
				// Add the nodes ready for planning
				//
				var startNode = graph.AddVertex(sp, sIgBox);
				var endNode = graph.AddVertex(ep, eIgBox);

				var nodePath = AStar(graph, astarOpenQueue, startNode, endNode);

				graph.RemoveVertex(startNode);
				graph.RemoveVertex(endNode);

				var points = nodePath.Select(n => n.Point);
				var pp = new PlannedPath(
					points.ToImmutableArray(),
					a.Start.Port.Direction,
					a.End.Port.Direction);
				arrowPaths.Add(pp);
			}

			var debugs = new List<IDrawable>();
#if false
			var npen = new Pen(Colors.Green, 1);
			foreach (var v in graph.Vertices)
			{
				var c = v.IgnoreBox >= 0 ? Colors.Red : Colors.Blue;
				if (v.Neighbors == null) continue;
				foreach (var n in v.Neighbors)
				{
					var p = new Path();
					p.MoveTo(v.Point);
					p.LineTo(n.Point);
					p.Pen = npen;
					debugs.Add(p);
				}
				var e = new Ellipse(v.Point-new Size(4), new Size(8), brush: new SolidBrush(c));
				debugs.Add(e);
			}
#endif

			return new DiagramPaths(arrowPaths.ToImmutableArray(), debugs.ToImmutableArray());
		}

		/// <summary>
		/// "Theta*: Any-Angle Path Planning on Grids"
		/// - Kenny Daniel, Alex Nash, Sven Koenig - University of Southern California
		/// </summary>
		List<Vertex> AStar(Graph graph, FastPriorityQueue<Vertex> open, Vertex startVert, Vertex endVert)
		{
			//
			// Initialize
			//
			graph.InitializeAStar(endVert);
			startVert.G = 0;
			startVert.Parent = startVert;
			open.Clear();
			open.Enqueue(startVert, startVert.G + startVert.H);
			startVert.IsOpen = true;

			//
			// Loop
			//
			Vertex s = null;
			while (open.Count > 0)
			{
				//
				// Get the next best vertex
				//
				s = open.Dequeue();
				s.IsOpen = false;

				//
				// If it's the end, we're done!
				//
				if (s == endVert)
				{
					//Debug.WriteLine($"PATH FOUND");
					break;
				}

				//
				// Not at then end :-(
				// Add it to the closed set so we don't try it again
				//
				s.IsClosed = true;

				//
				// Update the neighbors with this new path
				//
				graph.EnsureNeighbors(s);
				foreach (var sp in s.Neighbors)
				{
					if (!sp.IsClosed)
					{
						if (!sp.IsOpen)
						{
							sp.G = double.MaxValue;
							sp.Parent = null;
						}
						//
						// Calculate a potential new score for the vertex
						// if it follows our path.
						//
						var spg = s.G + s.Point.DistanceTo(sp.Point);
						if (spg < sp.G)
						{
							//
							// It's a winner, let's queue it up for inspection
							//
							sp.G = spg;
							sp.Parent = s;
							if (sp.IsOpen)
							{
								open.UpdatePriority(sp, sp.G + sp.H);
							}
							else
							{
								sp.IsOpen = true;
								open.Enqueue(sp, sp.G + sp.H);
							}
						}
					}
				}
			}

			//
			// Safety...
			//
			if (endVert.Parent == null)
			{
				//Debug.WriteLine($"PATH NOT FOUND");
				endVert.Parent = s ?? startVert;
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

		class Vertex : FastPriorityQueueNode
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
			/// True if a member of the open set.
			/// </summary>
			public bool IsOpen;
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
            readonly double ignoreDistSq;
			public Graph(VisibilityMap vmap, double maxNeighborDistance)
			{
				this.vmap = vmap;
                this.ignoreDistSq = maxNeighborDistance * maxNeighborDistance;
			}
			public readonly List<Vertex> Vertices = new List<Vertex>();
			public Vertex AddVertex(Point p, int ignoreBox = -1)
			{
				//
				// Add it to the list
				//
				var vert = new Vertex(p, ignoreBox);

                //
                // Calculate its visibility for already calculated verts
                //
				foreach (var v in Vertices)
				{
					if (v.Neighbors == null) continue;
                    var dx = v.Point.X - vert.Point.X;
                    var dy = v.Point.Y - vert.Point.Y;
                    if (dx * dx + dy * dy < ignoreDistSq)
                    {
                        if (vmap.LineOfSight(v.Point, vert.Point, v.IgnoreBox, vert.IgnoreBox))
                        {
                            v.Neighbors.Add(vert);
                        }
                    }
				}

				Vertices.Add(vert);
				return vert;
			}
			public void EnsureNeighbors(Vertex vert)
			{
				if (vert.Neighbors != null) return;
				vert.Neighbors = new List<Vertex>();
				foreach (var v in Vertices)
				{
					if (v == vert) continue;
                    var dx = v.Point.X - vert.Point.X;
                    var dy = v.Point.Y - vert.Point.Y;
                    if (dx * dx + dy * dy < ignoreDistSq)
                    {
                        if (vmap.LineOfSight(vert.Point, v.Point, vert.IgnoreBox, v.IgnoreBox))
                        {
                            vert.Neighbors.Add(v);
                        }
                    }
				}
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
					v.IsOpen = false;
					v.IsClosed = false;
				}
			}
		}

		class VisibilityMap
		{
			readonly Point[] bounds;
            readonly bool[] ignoreBox;
            //readonly Quadtree quad;

			public VisibilityMap(ImmutableArray<Box> boxes)
			{
				bounds = new Point[boxes.Length * 2];
                ignoreBox = new bool[boxes.Length];
                var ignoresToCascade = new List<int>();
                for (int i = 0; i < boxes.Length; i++)
				{
					var b = boxes[i];
					var bb = b.PreventOverlapFrame;
					bb.Inflate(-bb.Size * 0.005);
					bounds[i * 2] = bb.TopLeft;
					bounds[i * 2 + 1] = bb.BottomRight;
                    ignoreBox[i] = bb.Size.Width < 1;
                    if (ignoreBox[i])
                    {
                        ignoresToCascade.Add(i);
                    }
				}
                var cascaded = new HashSet<int>();
                while (ignoresToCascade.Count > 0)
                {
                    var igs = ignoresToCascade.ToArray();
                    ignoresToCascade.Clear();
                    foreach (var i in igs)
                    {
                        cascaded.Add (i);
                        ignoreBox[i] = true;
                        var f = boxes[i].Frame;
                        for (var j = 0; j < ignoreBox.Length; j++)
                        {
                            if (i == j)
                                continue;
                            if (boxes[j].Frame.Intersects(f) && !cascaded.Contains(j))
                                ignoresToCascade.Add(j);
                        }
                    }
                }
                //var minx = double.MaxValue;
                //var miny = double.MaxValue;
                //var maxx = double.MinValue;
                //var maxy = double.MinValue;                    
                //for (int i = 0; i < boxes.Length; i++)
                //{
                //    if (!ignoreBox[i])
                //    {
                //        var tl = bounds[i * 2];
                //        var br = bounds[i * 2 + 1];
                //        minx = Math.Min(minx, tl.X);
                //        miny = Math.Min(miny, tl.Y);
                //        maxx = Math.Max(maxx, br.X);
                //        maxy = Math.Max(maxy, br.Y);
                //    }
                //}
                //quad = new Quadtree(new Rect(minx-1,miny-1,maxx-minx+2,maxy-miny+2), 6);
                //for (int i = 0; i < boxes.Length; i++)
                //{
                //    if (!ignoreBox[i])
                //    {
                //        var tl = bounds[i * 2];
                //        var br = bounds[i * 2 + 1];
                //        quad.AddToAncestry(i, new Rect(tl, new Size(br.X - tl.X, br.Y - tl.Y)));
                //    }
                //}
                //Debug.WriteLine(quad);
            }

            /// <summary>
            /// "An Efficient and Robust Ray–Box Intersection Algorithm"
            /// - Amy Williams, Steve Barrus, R. Keith Morley, Peter Shirley - University of Utah
            /// </summary>
            public bool LineOfSight(Point origin, Point destination, int ignoreBox0, int ignoreBox1)
			{
                //
                // Cached ray properties
                //
                var delta = (destination - origin);
				var distance = delta.Distance;
				if (distance < 1e-12) return true;

				var direction = delta * (1 / distance);
				// Divide by 0 is valid as +/- Inf is supported
				var invDirection = new Point(1.0 / direction.X, 1.0 / direction.Y);
				var sign0 = invDirection.X < 0.0 ? 1 : 0;
				var sign1 = invDirection.Y < 0.0 ? 1 : 0;
				var t0 = 0.0;
				var t1 = distance;

                var minx = Math.Min(origin.X, destination.X);
                var miny = Math.Min(origin.Y, destination.Y);
                var maxx = Math.Max(origin.X, destination.X);
                var maxy = Math.Max(origin.Y, destination.Y);
                //var node = quad.NodeForFrame(new Rect (minx, miny, maxx-minx, maxy-miny), -1);

                //Debug.WriteLine($"LOS {origin} -> {destination} #{node?.Values?.Count}");

                //
                // Go through the boxes...
                //
                for (int boxIndex = 0; boxIndex < bounds.Length/2; boxIndex++)
                //for (int j = 0; node?.Values != null && j < node.Values.Count; j++)
				{
                    //var boxIndex = node.Values[j];
					if (ignoreBox[boxIndex] || boxIndex == ignoreBox0 || boxIndex == ignoreBox1)
						continue;
					
                    var i = boxIndex * 2;
					var tmin = (bounds[i + sign0].X - origin.X) * invDirection.X;
					var tmax = (bounds[i + 1 - sign0].X - origin.X) * invDirection.X;
					var tymin = (bounds[i + sign1].Y - origin.Y) * invDirection.Y;
					var tymax = (bounds[i + 1 - sign1].Y - origin.Y) * invDirection.Y;

					var isect = false;
					if ((tmin > tymax) || (tymin > tmax))
					{
						//isect = false;
					}
					else {
						if (tymin > tmin) tmin = tymin;
						if (tymax < tmax) tmax = tymax;
						isect = (tmin < t1 && tmax > t0);
					}

					//Debug.WriteLine($"ISECT {isect} [{bounds[i]},{bounds[i + 1]}] with <{origin},{destination}>");
					if (isect)
					{
						return false;
					}
				}

				// No intersections
				return true;
			}
		}
	}
}

