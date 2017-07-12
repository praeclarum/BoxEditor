using System;
using NGraphics;

namespace BoxEditor
{
	public class Arrow : ISelectable
    {
        public PortRef Start { get; set; }
        public PortRef End { get; set; }
        public Path Path { get; set; }

		public Box StartBox => Start.Box;
		public Box EndBox => End.Box;

        public double Width
        {
            get => Path.Pen.Width;
            set => Path.Pen.Width = value;
        }

		public Arrow(PortRef start, PortRef end, double width = 1.0)
        {
            Start = start;
            End = end;

            Path = new Path();
            Path.Pen = new Pen(Colors.Black, width);
            Path.MoveTo(start.PortPoint);
            Path.LineTo(end.PortPoint);
        }

		public virtual void Draw (ICanvas canvas)
		{
			Path.Draw (canvas);
		}
	}
}
