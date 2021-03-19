using System;
using NGraphics;

namespace BoxEditor
{
	public class Arrow : ISelectable
    {
		public readonly string id;
		public string Id => id;
		public readonly object Value;
		public readonly ArrowStyle Style;
		public readonly PortRef Start;
        public readonly PortRef End;

		public Box StartBox => Start.Box;
		public Box EndBox => End.Box;
		public Port StartPort => Start.Port;
		public Port EndPort => End.Port;

		public Arrow(string id, object value, ArrowStyle style, PortRef start, PortRef end)
        {
			if (string.IsNullOrEmpty(id))
			{
				throw new ArgumentException("Id must be set");
			}
			this.id = id;
            Value = value;
			Style = style;
            Start = start;
            End = end;
        }

        public override string ToString() => $"Arrow {Id}";

        public Arrow UpdateBox(Box b, Box newb)
		{
			return new Arrow(Id, Value, Style, Start.UpdateBox(b,newb), End.UpdateBox(b,newb));
		}

        public Arrow WithEnd(Box box, Port port)
        {
            return new Arrow(Id, Value, Style, Start, new PortRef (box, port));
        }

		public bool NeedsFlip
		{
			get
			{
				var arrowNeedsFlip = Start.Port.FlowDirection switch
				{
					FlowDirection.Input => true,
					FlowDirection.Output => false,
					_ => false,
				};
				if (!arrowNeedsFlip)
				{
					arrowNeedsFlip = End.Port.FlowDirection switch
					{
						FlowDirection.Input => false,
						FlowDirection.Output => true,
						_ => false,
					};
				}
				return arrowNeedsFlip;
			}
		}

		public Arrow Flip()
		{
			return new Arrow(Id, Value, Style, End, Start);
		}

		public Arrow FlipIfNeeded()
		{
			return NeedsFlip ? Flip() : this;
		}
	}

	public class ArrowStyle
	{
		public readonly Color LineColor;
		public readonly double LineWidth;
		public readonly bool ViewDependent;

		public static readonly ArrowStyle Default = new ArrowStyle(Colors.Black, 4, false);

		public ArrowStyle(Color lineColor, double lineWidth, bool viewDependent)
		{
			LineColor = lineColor;
			LineWidth = lineWidth;
			ViewDependent = viewDependent;
		}
	}
}
