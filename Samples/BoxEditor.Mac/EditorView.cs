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

		nint lastDragEv = 0;

		TouchEvent GetTouchEvent(NSEvent theEvent)
		{
			var winloc = theEvent.LocationInWindow;
			var loc = this.ConvertPointToView(winloc, this);
			loc.Y = Bounds.Height - loc.Y;
			return new TouchEvent()
			{
				TouchId = theEvent.EventNumber,
				Location = new Point(loc.X, loc.Y),
			};
		}

		public override void MouseDragged(NSEvent theEvent)
		{
			base.MouseMoved(theEvent);
			var ev = theEvent.EventNumber;
			if (lastDragEv != ev)
			{
				lastDragEv = ev;
				editor.TouchBegan(GetTouchEvent(theEvent));
			}
			else {
				editor.TouchMoved(GetTouchEvent(theEvent));
			}
		}

		public override void MouseUp(NSEvent theEvent)
		{
			base.MouseUp(theEvent);
			editor.TouchEnded(GetTouchEvent(theEvent));
		}

		public override void MouseExited(NSEvent theEvent)
		{
			base.MouseUp(theEvent);
			editor.TouchCanceled(GetTouchEvent(theEvent));
		}
	}
}

