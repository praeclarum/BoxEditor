using System;

using AppKit;
using Foundation;

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
			var vals = new[] { "A", "B", "C", "D" };
			var conns = new[] { Tuple.Create("A", "C") };

			var d = Diagram.Create(
				DiagramStyle.Default,
				vals,
				conns,
				o =>
				{
					var v = (string)o;
					var b = new BoxBuilder();
					b.Value = v;
					b.Frame = new NGraphics.Rect(100 + (v[0]-'A')*125, 100, 100, 100);
					var pf = b.Frame;
					pf.Inflate(-0.4 * pf.Width, -0.4 * pf.Height);
					b.AddPort("Center", pf, PortStyle.Default);
					return b.ToBox();
				},
				(f, o) =>
				{
					var c = (Tuple<string,string>)o;
					var fp = f(c.Item1, "Center");
					var tp = f(c.Item2, "Center");
					return new Arrow(o, ArrowStyle.Default, fp, tp);
				});

			editorView.Editor.Diagram = d;
		}
	}
}
