using System;

using AppKit;
using Foundation;

using NGraphics;

namespace BoxEditor.Mac
{
	[Register("EditorView")]
	public class EditorView : NSView
	{
		Editor editor = new Editor();

		public Editor Editor
		{
			get
			{
				return editor;
			}
			set
			{				
				if (value != null)
				{
					if (editor != null)
					{
						editor.Redraw -= Editor_Redraw;
					}
					editor = value;
					editor.Redraw += Editor_Redraw;
					editor.ResizeView(Bounds.Size.GetSize());
					SetNeedsDisplayInRect(Bounds);
				}
			}
		}

		public EditorView(IntPtr handle) : base(handle)
		{
		}

		public EditorView()
		{
		}

		public override bool IsFlipped
		{
			get
			{
				return true;
			}
		}

		void Editor_Redraw()
		{
			SetNeedsDisplayInRect(Bounds);
		}

		public override void DrawRect(CoreGraphics.CGRect dirtyRect)
		{
			base.DrawRect(dirtyRect);
			var context = NSGraphicsContext.CurrentContext.GraphicsPort;
			var canvas = new CGContextCanvas(context);
			editor.Draw(canvas, dirtyRect.GetRect());
		}
	}
}

