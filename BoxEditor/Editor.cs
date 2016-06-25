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
        }
    }
}
