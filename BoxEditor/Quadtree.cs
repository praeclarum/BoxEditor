using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using NGraphics;

namespace BoxEditor
{
	class Quadtree
	{
		public class Node
		{
			public readonly int Depth;
			public readonly Rect Frame;
			public readonly Node[] Children = new Node[4];
			public readonly Rect[] ChildFrames;
			public List<int> Values;
			public Node(int depth, Rect frame)
			{
				Depth = depth;
				Frame = frame;
				ChildFrames = CalculateChildFrames(frame);
			}
			public void AddValue(int box, Rect boxFrame)
			{
				if (Values == null) Values = new List<int>();
				Values.Add(box);
			}
			public void RemoveValue(int box)
			{
				if (Values == null)
					throw new ArgumentException($"Box {box} not in {this} (no values)");
				var i = Values.IndexOf(box);
				if (i < 0)
					throw new ArgumentException($"Box {box} not in {this}");
				Values.RemoveAt(i);
			}
			static Rect[] CalculateChildFrames(Rect parentFrame)
			{
				var s = parentFrame.Size / 2;
				var c = new Rect[4];
				c[0] = new Rect(parentFrame.TopLeft, s);
				c[1] = new Rect(parentFrame.TopLeft + new Point(0, s.Height), s);
				c[2] = new Rect(parentFrame.TopLeft + new Point(s.Width, 0), s);
				c[3] = new Rect(parentFrame.TopLeft + new Point(s.Width, s.Height), s);
				return c;
			}
			public override string ToString()
			{
				var cc = Children.Sum(c => c != null ? 1 : 0);
				return $"Node d={Depth}, c={cc}, v={Values?.Count}, f={Frame}";
			}
		}

		readonly Node rootNode;
		readonly int maxDepth;

		public Quadtree(double w, double h, int maxDepth = 8)
		{
			this.maxDepth = maxDepth;
			rootNode = new Node(0, new Rect(-w / 2, -h / 2, w, h));
		}

		public void Add(int box, Rect boxFrame)
		{
			var n = NodeForFrame(boxFrame);
			n.AddValue(box, boxFrame);
		}

		public void Move(int box, Rect oldFrame, Rect newFrame)
		{
			var oldn = GetNodeForBox(box, oldFrame);
			var newn = NodeForFrame(newFrame);
			if (oldn != newn)
			{
				oldn.RemoveValue(box);
				newn.AddValue(box, newFrame);
			}
		}

		Node NodeForFrame(Rect boxFrame)
		{
			var q = new Queue<Node>();
			q.Enqueue(rootNode);
			var cinter = new bool[4];
			while (q.Count > 0)
			{
				var n = q.Dequeue();

				var cintercount = 0;
				for (var i = 0; i < 4; i++)
				{
					var inter = n.ChildFrames[i].Intersects(boxFrame);
					if (inter) cintercount++;
					cinter[i] = inter;
				}
				if (cintercount > 1 || n.Depth + 1 > maxDepth)
				{
					return n;
				}
				else {
					for (var i = 0; i < 4; i++)
					{
						if (cinter[i])
						{
							var cn = n.Children[i];
							if (cn == null)
							{
								cn = new Node(n.Depth + 1, n.ChildFrames[i]);
								n.Children[i] = cn;
							}
							q.Enqueue(cn);
						}
					}
				}
			}
			throw new Exception($"Failed to find node for {boxFrame}");
		}

		Node GetNodeForBox(int box, Rect boxFrame)
		{
			var q = new Queue<Node>();
			q.Enqueue(rootNode);
			while (q.Count > 0)
			{
				var m = q.Dequeue();

				if (m.Values != null)
				{
					for (var i = 0; i < m.Values.Count; i++)
					{
						if (m.Values[i] == box) return m;
					}
				}

				for (var i = 0; i < 4; i++)
				{
					if (m.Children[i] != null && m.ChildFrames[i].Intersects(boxFrame))
					{
						q.Enqueue(m.Children[i]);
					}
				}
			}

			throw new Exception($"Cannot find box {box}");
		}

		public bool GetOverlap(ImmutableArray<Box> boxes, Point[] offsets, int box, Rect boxFrame, out int otherBox, out Point overlap)
		{
			var q = new Queue<Node>();
			q.Enqueue(rootNode);
			var a = boxes[box];
			var eps = Math.Abs(a.Frame.Size.Min) / 1000.0;
			while (q.Count > 0)
			{
				var m = q.Dequeue();

				if (m.Values != null)
				{
					for (var i = 0; i < m.Values.Count; i++)
					{
						if (m.Values[i] == box) continue;

						var b = boxes[m.Values[i]];

						var maxMargin = new Size(
							Math.Max(a.Style.Margin.Width, b.Style.Margin.Width),
							Math.Max(a.Style.Margin.Height, b.Style.Margin.Height));
						var amr = a.Frame.GetInflated(maxMargin / 2) + offsets[box];
						var bmr = b.Frame.GetInflated(maxMargin / 2) + offsets[m.Values[i]];
						if (amr.Intersects(bmr))
						{
							//if (bmr.Right
							var dx1 = amr.Left - bmr.Right;
							var dx2 = amr.Right - bmr.Left;
							var dx = (Math.Abs(dx1) <= Math.Abs(dx2)) ? dx1 : dx2;
							var dy1 = amr.Top - bmr.Bottom;
							var dy2 = amr.Bottom - bmr.Top;
							var dy = (Math.Abs(dy1) <= Math.Abs(dy2)) ? dy1 : dy2;
							if (Math.Abs(dx) <= Math.Abs(dy))
							{
								dy = 0;
							}
							else
							{
								dx = 0;
							}

							if (Math.Abs(dx) > eps || Math.Abs(dy) > eps)
							{
								//Debug.WriteLine($"  DX = {dx} DY = {dy} AMR = {amr}");
								otherBox = m.Values[i];
								overlap = new Point(dx, dy);
								return true;
							}
						}
					}
				}

				for (var i = 0; i < 4; i++)
				{
					if (m.Children[i] != null && m.ChildFrames[i].Intersects(boxFrame))
					{
						q.Enqueue(m.Children[i]);
					}
				}
			}

			otherBox = -1;
			overlap = Point.Zero;
			return false;
		}
	}
}
