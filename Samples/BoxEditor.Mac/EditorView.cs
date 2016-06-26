using System;
using System.Diagnostics;
using AppKit;
using Foundation;

using NGraphics;

namespace BoxEditor.Mac
{
	[Register("EditorView")]
	public class EditorView : NSView
	{
		Editor editor;

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
			Initialize();
		}

		public EditorView()
		{
			Initialize();
		}

		void Initialize()
		{
			WantsLayer = true;
			Editor = new Editor();
		}

		public override bool IsFlipped
		{
			get
			{
				return true;
			}
		}
		NSTrackingArea lastMouseMoveArea;
		public override void UpdateTrackingAreas()
		{
			base.UpdateTrackingAreas();
			var options =
				NSTrackingAreaOptions.ActiveInKeyWindow
				| NSTrackingAreaOptions.InVisibleRect
				| NSTrackingAreaOptions.MouseEnteredAndExited
				| NSTrackingAreaOptions.MouseMoved
				;
			var mouseMoveArea = new NSTrackingArea(Bounds, options, this, null);
			if (lastMouseMoveArea != null)
			{
				RemoveTrackingArea(lastMouseMoveArea);
			}
			lastMouseMoveArea = mouseMoveArea;
			AddTrackingArea(mouseMoveArea);
		}

		void Editor_Redraw()
		{
			//Debug.WriteLine("REDRAW");
			SetNeedsDisplayInRect(Bounds);
		}

		public override void DrawRect(CoreGraphics.CGRect dirtyRect)
		{
			base.DrawRect(dirtyRect);
			var context = NSGraphicsContext.CurrentContext.GraphicsPort;
			var canvas = new CGContextCanvas(context);
			editor.Draw(canvas, dirtyRect.GetRect());
		}

		TouchEvent GetTouchEvent(NSEvent theEvent)
		{
			var winloc = theEvent.LocationInWindow;
			var loc = this.ConvertPointToView(winloc, this);
			loc.Y = Bounds.Height - loc.Y;
			var mods = theEvent.ModifierFlags;
			var shift = mods.HasFlag(NSEventModifierMask.ShiftKeyMask);
			return new TouchEvent
			{
				TouchId = theEvent.EventNumber,
				Location = new Point(loc.X, loc.Y),
				IsShiftDown = shift,
			};
		}

		public override void MouseDown(NSEvent theEvent)
		{
			base.MouseDown(theEvent);
			editor.TouchBegan(GetTouchEvent(theEvent));
		}

		public override void MouseDragged(NSEvent theEvent)
		{
			base.MouseMoved(theEvent);
			editor.TouchMoved(GetTouchEvent(theEvent));
		}

		public override void MouseMoved(NSEvent theEvent)
		{
			base.MouseMoved(theEvent);
			editor.MouseMoved(GetTouchEvent(theEvent));
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

