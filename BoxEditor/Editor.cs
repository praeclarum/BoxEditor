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

        //public event Action<Box> MovedBox;
        public event Action Redraw;

        public Editor()
        {
        }

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

        public void Draw(ICanvas canvas)
        {
            foreach (var b in diagram.Boxes)
            {
                canvas.FillRectangle(b.Frame, Colors.White);
                canvas.DrawRectangle(b.Frame, Colors.Black);
                foreach (var p in b.Ports)
                {
                    canvas.FillRectangle(p.Frame, Colors.Gray);
                }
            }
            foreach (var a in diagram.Arrows)
            {
                canvas.DrawLine(a.From.Port.Frame.Center, a.To.Port.Frame.Center, Colors.Red);
            }
        }
    }
}
