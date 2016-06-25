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
			var vals = new[] { "AA", "BA", "CC", "DB", "AC" };
			var conns = new[] { Tuple.Create("AA", "CC") };

			var d = Diagram.Create(
				DiagramStyle.Default,
				vals,
				conns,
				o =>
				{
					var v = (string)o;
					var b = new BoxBuilder();
					b.Value = v;
					b.Frame = new NGraphics.Rect(
						100 + (v[0] - 'A') * 125,
						100 + (v[1] - 'A') * 125,
						100, 100);
					b.AddPort("Center", b.Frame.Center, Directions.Any);
					return b.ToBox();
				},
				(f, o) =>
				{
					var c = (Tuple<string,string>)o;
					var fp = f(c.Item1, "Center");
					var tp = f(c.Item2, "Center");
					return new Arrow(o, ArrowStyle.Default, State.None, fp, tp);
				});

			editorView.Editor.BoxDrawn += (b, c) =>
			{
				var v = (string)b.Value;
				c.DrawText(v, b.Frame.BottomLeft+new Point(8, -8), new Font("Helvetica-Regular", 20), brush: new SolidBrush(Colors.Red));
			};

			editorView.Editor.Diagram = d;
		}
	}
}
