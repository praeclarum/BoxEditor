using System;

using AppKit;
using Foundation;
using NGraphics;

namespace BoxEditor.Mac
{
	public partial class ViewController : NSViewController
	{
		Document document;

		public Document Document
		{
			get
			{
				return document;
			}
			set
			{
				document = value;
				OnDocumentChanged();
			}
		}

		public ViewController(IntPtr handle) : base(handle)
		{
		}

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();
			// Do any additional setup after loading the view.
		}

		public override NSObject RepresentedObject
		{
			get
			{
				return base.RepresentedObject;
			}
			set
			{
				base.RepresentedObject = value;
				// Update the view, if already loaded.
			}
		}

		void OnDocumentChanged()
		{
			var vals = new[] { "AA", "BA", "CC", "DB", "AC", "AD", "BB" };
			var conns = new[] {
				Tuple.Create(Tuple.Create("AA", "TopCenter"), Tuple.Create("CC", "BottomCenter")),
			     Tuple.Create(Tuple.Create("AA", "CenterLeft"), Tuple.Create("CC", "CenterRight")),
				Tuple.Create(Tuple.Create("AD", "Center"), Tuple.Create("BA", "Center")),
				Tuple.Create(Tuple.Create("BA", "CenterRight"), Tuple.Create("DB", "CenterLeft")),
				     Tuple.Create(Tuple.Create("BB", "CenterRight"), Tuple.Create("CC", "TopCenter")),
			     Tuple.Create(Tuple.Create("BA", "TopCenter"), Tuple.Create("DB", "CenterRight")),
				     Tuple.Create(Tuple.Create("AC", "TopCenter"), Tuple.Create("DB", "CenterRight")),
			};

			var d = Diagram.Create(
				DiagramStyle.Default,
				vals,
				conns,
				o =>
				{
					var v = (string)o;
					var b = new BoxBuilder();
					b.Id = v;
					b.Value = v;
					b.Frame = new NGraphics.Rect(
						100 + (v[0] - 'A') * 125,
						50 + (v[1] - 'A') * 125,
						100, 100);
					b.AddPort("Center", new Point(0.5, 0.5), Point.Zero);
					b.AddPort("TopCenter", new Point(0.5, 0), -Point.OneY);
					b.AddPort("BottomCenter", new Point(0.5, 1), Point.OneY);
					b.AddPort("CenterLeft", new Point(0, 0.5), -Point.OneX);
					b.AddPort("CenterRight", new Point(1, 0.5), Point.OneX);
					return b.ToBox();
				},
				(f, o) =>
				{
					var c = (Tuple<Tuple<string, string>,Tuple<string, string>>)o;
					var fp = f(c.Item1.Item1, c.Item1.Item2);
					var tp = f(c.Item2.Item1, c.Item2.Item2);
					return new Arrow(o.ToString(), o, ArrowStyle.Default, fp, tp);
				});

			editorView.Editor.BoxDrawn += (b, c) =>
			{
				var v = (string)b.Value;
				c.DrawText(v, b.Frame.BottomLeft+new Point(8, -8), new Font("Helvetica-Regular", 20), brush: new SolidBrush(Colors.Gray));
			};

			editorView.Editor.Diagram = d;
		}
	}
}
