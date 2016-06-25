using NGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoxEditor
{
    public class Editor
    {
		double zoom = 1;
		//Point center = Point.Zero;

        Diagram diagram = Diagram.Empty;

        public Diagram Diagram
        {
            get
            {
                return diagram;
            }

            set
            {
                diagram = value;
                OnDiagramChanged();
            }
        }

        public event Action Redraw;
		public event Action<Rect, ICanvas> BackgroundDrawn;
		public event Action<Box, ICanvas> BoxDrawn;
		public event Action<Box, Port, ICanvas> PortDrawn;
		public event Action<Arrow, ICanvas> ArrowDrawn;

        public void ResizeView(Size newViewSize)
        {
        }

        public void TouchBegan (long touchId, Point point)
        {
        }

        public void TouchMoved(long touchId, Point point)
        {
        }

        public void TouchEnded(long touchId, Point point)
        {
        }

        public void TouchCanceled(long touchId, Point point)
        {
        }

        void OnDiagramChanged ()
        {
            Redraw?.Invoke();
        }

        public void Draw(ICanvas canvas, Rect dirtyFrame)
        {
			var handlePen = new Pen(diagram.Style.HandleBorderColor, 1.0 / zoom);
			var handleBrush = new SolidBrush(diagram.Style.HandleBackgroundColor);

			if (diagram.Style.BackgroundColor.A > 0)
			{
				canvas.FillRectangle(dirtyFrame, diagram.Style.BackgroundColor);
			}
			BackgroundDrawn?.Invoke(dirtyFrame, canvas);
            foreach (var b in diagram.Boxes)
            {
				if (b.Style.BackgroundColor.A > 0)
				{
					canvas.FillRectangle(b.Frame, b.Style.BackgroundColor);
				}
				if (b.Style.BorderColor.A > 0)
				{
					canvas.DrawRectangle(b.Frame, b.Style.BorderColor, b.Style.BorderWidth);
				}
				BoxDrawn?.Invoke(b, canvas);
                foreach (var p in b.Ports)
                {
					PortDrawn?.Invoke(b, p, canvas);
                }
            }
            foreach (var a in diagram.Arrows)
            {
                canvas.DrawLine(
					a.From.Port.Point,
					a.To.Port.Point,
					a.Style.LineColor,
					a.Style.LineWidth);
				ArrowDrawn?.Invoke(a, canvas);
            }
			foreach (var b in diagram.Boxes)
			{
				if (b.State.IsSelected)
				{
					DrawBoxHandles(b, canvas, handlePen, handleBrush);
				}
			}
			foreach (var a in diagram.Arrows)
			{
				if (a.State.IsSelected)
				{
					DrawArrowHandles(a, canvas, handlePen, handleBrush);
				}
			}
		}

		void DrawBoxHandles(Box box, ICanvas canvas, Pen handlePen, Brush handleBrush)
		{
			DrawBoxHandle(box.Frame.TopLeft, canvas, handlePen, handleBrush);
			DrawBoxHandle(box.Frame.TopLeft + new Point(box.Frame.Width / 2, 0), canvas, handlePen, handleBrush);
			DrawBoxHandle(box.Frame.TopRight, canvas, handlePen, handleBrush);
			DrawBoxHandle(box.Frame.TopRight + new Point(0, box.Frame.Height / 2), canvas, handlePen, handleBrush);
			DrawBoxHandle(box.Frame.BottomRight, canvas, handlePen, handleBrush);
			DrawBoxHandle(box.Frame.BottomLeft + new Point(box.Frame.Width / 2, 0), canvas, handlePen, handleBrush);
			DrawBoxHandle(box.Frame.BottomLeft, canvas, handlePen, handleBrush);
			DrawBoxHandle(box.Frame.TopLeft + new Point(0, box.Frame.Height / 2), canvas, handlePen, handleBrush);
		}

		void DrawBoxHandle(Point point, ICanvas canvas, Pen handlePen, Brush handleBrush)
		{
			var s = 8.0 / zoom;
			canvas.DrawRectangle(point.X - s / 2, point.Y - s / 2, s, s, handlePen, handleBrush);
		}
		void DrawArrowHandles(Arrow arrow, ICanvas canvas, Pen handlePen, Brush handleBrush)
		{
			DrawArrowHandle(arrow.From.Port.Point, canvas, handlePen, handleBrush);
			DrawArrowHandle(arrow.To.Port.Point, canvas, handlePen, handleBrush);
		}
		void DrawArrowHandle(Point point, ICanvas canvas, Pen handlePen, Brush handleBrush)
		{
			var s = 8.0 / zoom;
			canvas.DrawEllipse(point.X - s / 2, point.Y - s / 2, s, s, handlePen, handleBrush);
		}
	}
}
