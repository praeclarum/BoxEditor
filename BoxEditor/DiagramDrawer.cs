using System;
using System.Diagnostics;
using NGraphics;

namespace BoxEditor
{
    public class DiagramDrawer
    {
        public virtual void Draw(Diagram diagram, ICanvas canvas, Rect dirtyViewRect, Transform viewToDiagram, Action draw)
        {
            //Debug.WriteLine("DRAW");

            //
            // Draw the background in View-scale
            //
            DrawBackground(canvas, diagram, dirtyViewRect);

            //
            // Transform the view
            //
            var diagramToView = viewToDiagram.GetInverse();
            var needsTx = diagramToView != Transform.Identity;
            if (needsTx)
            {
                canvas.SaveState();
                canvas.Transform(diagramToView);
            }

            var dirtyDiagramRect = viewToDiagram.TransformRect(dirtyViewRect);
            //Debug.WriteLine(dirtyDiagramRect);

            foreach (var b in diagram.Boxes)
            {
                if (b.Frame.Intersects(dirtyDiagramRect))
                {
                    DrawBox(canvas, b);
                }
            }

            foreach (var b in diagram.Boxes)
            {
                foreach (var p in b.Ports)
                {
                    DrawPort(canvas, b, p);
                }
            }

            foreach (var a in diagram.Arrows)
            {
                DrawArrow(canvas, a, diagram, viewToDiagram);
            }

            draw();

            //
            // Debug
            //
            foreach (var d in diagram.Paths.DebugDrawings)
            {
                d.Draw(canvas);
            }

            //
            // Untransform
            //
            if (needsTx)
            {
                canvas.RestoreState();
            }
        }

        public virtual void DrawArrow(ICanvas canvas, Arrow a, Diagram diagram, Transform viewToDiagram)
        {
            var p = diagram.GetArrowPath(a);
            p.Pen = new Pen(a.Style.LineColor, a.Style.ViewDependent ? a.Style.LineWidth * viewToDiagram.A : a.Style.LineWidth);
            p.Draw(canvas);
        }

        public virtual void DrawPort(ICanvas canvas, Box b, Port p)
        {
        }

        public virtual void DrawBox(ICanvas canvas, Box b)
        {
            if (b.Style.BackgroundColor.A > 0)
            {
                canvas.FillRectangle(b.Frame, b.Style.BackgroundColor);
            }
            if (b.Style.BorderColor.A > 0)
            {
                canvas.DrawRectangle(b.Frame, b.Style.BorderColor, b.Style.BorderWidth);
            }
        }

        public virtual void DrawBackground(ICanvas canvas, Diagram diagram, Rect dirtyViewRect)
        {
            if (diagram.Style.BackgroundColor.A > 0)
            {
                canvas.FillRectangle(dirtyViewRect, diagram.Style.BackgroundColor);
            }
        }
    }
}
