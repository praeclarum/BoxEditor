using NUnit.Framework;
using System;
using BoxEditor;
using System.Linq;
using NGraphics;
using System.Collections.Immutable;
using System.Diagnostics;

namespace BoxEditor.Tests
{
	[TestFixture]
	public class PerfTests
	{
		Diagram CreateDiagram(int numBoxes, double maxBoxSize, double maxPosition)
		{
			var rand = new Random(1234);
			var boxvals =
				Enumerable
				.Range(0, numBoxes)
				.Select(x => x.ToString())
				.ToArray();
			var d = Diagram.Create(
				DiagramStyle.Default,
				boxvals,
				o =>
				{
					var p = new Point(rand.NextDouble() * maxPosition, rand.NextDouble() * maxPosition);
					var s = new Size(rand.NextDouble() * maxBoxSize, rand.NextDouble() * maxBoxSize);
					return new Box(o.ToString(), o, new Rect(p, s), BoxStyle.Default);
				});
			Assert.AreEqual(numBoxes, d.Boxes.Length);
			return d;
		}

		Diagram MoveOneBox(Diagram d, TimeSpan maxTime)
		{
			var sw = new Stopwatch();
			sw.Start();
			var nd = d.MoveBoxes(d.Boxes.Take(1).ToImmutableArray(), new Point(10, 10), true, 8);
			sw.Stop();
			if (sw.Elapsed > maxTime)
			{
				Assert.Fail($"Move took too long: {sw.Elapsed} vs {maxTime}");
			}
			return nd.Item1;
		}

		[Test]
		public void Move10WithBigSpread()
		{
			var d = CreateDiagram(10, 100, 10000);
			MoveOneBox(d, TimeSpan.FromSeconds(0.01));
		}
		[Test]
		public void Move10WithSmallSpread()
		{
			var d = CreateDiagram(10, 100, 100);
			MoveOneBox(d, TimeSpan.FromSeconds(0.01));
		}
		[Test]
		public void Move1000WithBigSpread()
		{
			var d = CreateDiagram(1000, 100, 10000);
			MoveOneBox(d, TimeSpan.FromSeconds(0.2));
		}
		[Test]
		public void Move1000WithSmallSpread()
		{
			var d = CreateDiagram(1000, 100, 100);
			MoveOneBox(d, TimeSpan.FromSeconds(3.0));
		}
	}
}
