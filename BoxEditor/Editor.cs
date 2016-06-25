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

		void OnDiagramChanged()
		{
			Redraw?.Invoke();
		}

        public void ResizeView(Size newViewSize)
        {
        }

		Transform viewToDiagram = Transform.Identity;

		public Transform ViewToDiagramTransform
		{
			get { return viewToDiagram; }
			set { viewToDiagram = value; }
		}
		public Transform DiagramToViewTransform
		{
			get { return viewToDiagram.GetInverse(); }
			set { viewToDiagram = value.GetInverse(); }
		}
		public double DiagramToViewScale
		{
			get { return viewToDiagram.GetInverse().A; }
		}

		Point ViewToDiagram(Point point)
		{
			return viewToDiagram.TransformPoint(point);
		}

		Point DiagramToView(Point point)
		{
			var diagramToView = viewToDiagram.GetInverse();
			return diagramToView.TransformPoint(point);
		}

		Rect DiagramToView(Rect rect)
		{
			var diagramToView = viewToDiagram.GetInverse();
			return diagramToView.TransformRect(rect);
		}

		#region Interaction

		Dictionary<long, Point> activeTouches = new Dictionary<long, Point>();

		Point panLastCenter = Point.Zero;
		double panLastRadius = 0;

		TouchGesture touchGesture = TouchGesture.None;
		Point dragBoxLastDiagramLoc = Point.Zero;
		bool dragBoxStartSelected = false;
		int dragBoxHandle = 0;
		Box dragBoxHandleBox = null;

		double handleSize = 22;

		public void TouchBegan (TouchEvent touch)
        {
			activeTouches[touch.TouchId] = touch.Location;

			var diagramLoc = ViewToDiagram(touch.Location);


			//			Console.WriteLine ("TOUCH BEGAN " + activeTouches.Count);

			if (activeTouches.Count == 1)
			{
				var boxHit = diagram.HitTestBoxes(diagramLoc).FirstOrDefault();

				var handleHit =
					(from b in diagram.Boxes
					 where IsSelected(b)
					 let h = b.HitTestHandles(diagramLoc, new Size(handleSize), 22 * viewToDiagram.A)
					 where h != null
					 orderby h.Item2
					 select Tuple.Create(b, h.Item1)).FirstOrDefault();

				//				Console.WriteLine ("SELS = {0}", selection.Count);
				if (handleHit != null)
				{
					touchGesture = TouchGesture.DragBoxHandle;
					dragBoxLastDiagramLoc = diagramLoc;
					dragBoxHandle = handleHit.Item2;
					dragBoxHandleBox = handleHit.Item1;
				}
				else if (boxHit != null && selection.Count > 0)
				{
					dragBoxStartSelected = IsSelected(boxHit);
					if (!dragBoxStartSelected)
					{
						lastEditMenuObject = null;
					}
					if (selection.Count == 1)
					{
						Select(new[] { boxHit });
					}
					else {
						if (!dragBoxStartSelected)
						{
							Select(new[] { boxHit });
						}
					}
					touchGesture = TouchGesture.DragSelection;
					dragBoxLastDiagramLoc = diagramLoc;
					dragBoxHandle = 0;
					dragBoxHandleBox = null;
				}
				else {
					touchGesture = TouchGesture.None;
				}
			}
			else {
				touchGesture = TouchGesture.Pan;
				panLastCenter = activeTouches.Values.Aggregate(Point.Zero, (a, x) => a + x) / activeTouches.Count;
				panLastRadius = activeTouches.Values.Average(x => x.DistanceTo(panLastCenter));
				//				Console.WriteLine ("C == {0} {1}", panLastCenter, panLastRadius);
			}
		}

		public void TouchMoved(TouchEvent touch)
        {
			//			Console.WriteLine ("TOUCH " + touch.Location);

			activeTouches[touch.TouchId] = touch.Location;

			switch (touchGesture)
			{
				case TouchGesture.Pan:
					{
						var center = activeTouches.Values.Aggregate(Point.Zero, (a, x) => a + x) / activeTouches.Count;
						var radius = activeTouches.Values.Average(x => x.DistanceTo(panLastCenter));
						viewToDiagram =
							viewToDiagram *
							Transform.Translate(panLastCenter - center) *
							Transform.ScaleAt(panLastRadius / radius, center);

						panLastCenter = center;
						panLastRadius = radius;
						Redraw?.Invoke();
					}
					break;
				case TouchGesture.DragSelection:
					{
						var loc = ViewToDiagram(activeTouches.Values.First());
						foreach (var b in selection.OfType<Box>())
						{
							var d = loc - dragBoxLastDiagramLoc;
							b.Move(d);
						}
						dragBoxLastDiagramLoc = loc;
						Redraw?.Invoke();
					}
					break;
				case TouchGesture.DragBoxHandle:
					if (dragBoxHandleBox != null)
					{
						var loc = ViewToDiagram(activeTouches.Values.First());
						var d = loc - dragBoxLastDiagramLoc;
						//					Console.WriteLine ("MOVE HANDLE = {0}", dragBoxHandle);
						dragBoxHandleBox.MoveHandle(dragBoxHandle, d);
						dragBoxLastDiagramLoc = loc;
						Redraw?.Invoke();
					}
					break;
			}
		}

		public void TouchEnded(TouchEvent touch)
        {
			touchGesture = TouchGesture.None;
			activeTouches.Remove(touch.TouchId);
		}

		public void TouchCanceled(TouchEvent touch)
        {
			touchGesture = TouchGesture.None;
			activeTouches.Remove(touch.TouchId);
		}

		enum TouchGesture
		{
			None,
			DragSelection,
			DragBoxHandle,
			Pan,
		}

		#endregion

		#region Selection

		readonly List<ISelectable> selection = new List<ISelectable>();

		bool IsSelected(ISelectable s)
		{
			return selection.Contains(s);
		}

		public void Select(IEnumerable<ISelectable> selects)
		{
			selection.Clear();
			selection.AddRange(selects);
			Redraw?.Invoke();
		}

		public bool HasSelection
		{
			get { return selection.Count > 0; }
		}

		public IReadOnlyList<ISelectable> Selection
		{
			get { return selection; }
		}

		public void SelectNone()
		{
			Select(Enumerable.Empty<ISelectable>());
		}

		public void SelectAll()
		{
			var sels = new List<ISelectable>();
			sels.AddRange(diagram.Boxes);
			sels.AddRange(diagram.Arrows);
			Select(sels);
		}

		#endregion


		#region Edit

		ISelectable lastEditMenuObject = null;

		public event Action<Point, Rect> EditMenuRequested;

		class Background : ISelectable
		{
		}

		readonly Background background = new Background();

		void HandleTap(TouchEvent touch)
		{
			//			Console.WriteLine ("TAP");

			var p = ViewToDiagram(touch.Location);
			ISelectable s = diagram.HitTestBoxes(p).FirstOrDefault();

			if (s == null)
			{
				s = diagram.HitTestArrows(p, viewToDiagram.A).FirstOrDefault();
			}

			if (s == null)
			{
				s = background;
			}

			if ((s != null && IsSelected(s)) || (s == background && selection.Count == 0))
			{
				//				Console.WriteLine ("EMR");
				if (lastEditMenuObject != s)
				{
					var emr = EditMenuRequested;
					if (emr != null)
					{
						var bb = new Rect(touch.Location, Size.Zero);
						foreach (var ss in selection)
						{
							var box = ss as Box;
							if (box != null)
							{
								bb = bb.Union(DiagramToView(box.Frame));
							}
						}
						emr(touch.Location, bb);
					}
					lastEditMenuObject = s;
				}
				else {
					lastEditMenuObject = null;
				}
				return;
			}

			selection.Clear();
			if (s != background)
			{
				selection.Add(s);
			}
		}

		public void Delete()
		{
			var data = diagram;
			var todelete = selection.ToList();

			SelectNone();

			foreach (var b in todelete.OfType<Box>().ToList())
			{
				foreach (var a in data.Arrows)
				{
					if (a.StartBox == b || a.EndBox == b)
						todelete.Add(a);
				}
			}

			foreach (var s in todelete)
			{
				var b = s as Box;
				if (b != null)
				{
					data.Boxes.Remove(b);
					continue;
				}
				var a = s as Arrow;
				if (a != null)
				{
					data.Arrows.Remove(a);
					continue;
				}
			}

			//DeletedBox?.Invoke("Delete");
			Redraw?.Invoke();
		}

		#endregion

		#region Drawing

		public event Action Redraw;
		public event Action<Rect, ICanvas> BackgroundDrawn;
		public event Action<Box, ICanvas> BoxDrawn;
		public event Action<Box, Port, ICanvas> PortDrawn;
		public event Action<Arrow, ICanvas> ArrowDrawn;

		public void Draw(ICanvas canvas, Rect dirtyViewRect)
        {
			//
			// Draw the background in View-scale
			//
			if (diagram.Style.BackgroundColor.A > 0)
			{
				canvas.FillRectangle(dirtyViewRect, diagram.Style.BackgroundColor);
			}
			BackgroundDrawn?.Invoke(dirtyViewRect, canvas);

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

			//var dirtyDiagramRect = viewToDiagram.TransformRect(dirtyViewRect);

			var handlePen = new Pen(diagram.Style.HandleBorderColor, 1.0 * viewToDiagram.A);
			var handleBrush = new SolidBrush(diagram.Style.HandleBackgroundColor);

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
					a.Start.Port.Point,
					a.End.Port.Point,
					a.Style.LineColor,
					a.Style.LineWidth);
				ArrowDrawn?.Invoke(a, canvas);
            }
			foreach (var b in diagram.Boxes)
			{
				if (IsSelected(b))
				{
					DrawBoxHandles(b, canvas, handlePen, handleBrush);
				}
			}
			foreach (var a in diagram.Arrows)
			{
				if (IsSelected(a))
				{
					DrawArrowHandles(a, canvas, handlePen, handleBrush);
				}
			}

			//
			// Untransform
			//
			if (needsTx)
			{
				canvas.RestoreState();
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
			var s = handleSize * viewToDiagram.A;
			canvas.DrawRectangle(point.X - s / 2, point.Y - s / 2, s, s, handlePen, handleBrush);
		}

		void DrawArrowHandles(Arrow arrow, ICanvas canvas, Pen handlePen, Brush handleBrush)
		{
			DrawArrowHandle(arrow.Start.Port.Point, canvas, handlePen, handleBrush);
			DrawArrowHandle(arrow.End.Port.Point, canvas, handlePen, handleBrush);
		}

		void DrawArrowHandle(Point point, ICanvas canvas, Pen handlePen, Brush handleBrush)
		{
			var s = handleSize * viewToDiagram.A;
			canvas.DrawEllipse(point.X - s / 2, point.Y - s / 2, s, s, handlePen, handleBrush);
		}

		#endregion
	}

	public struct TouchEvent
	{
		public long TouchId;
		public Point Location;

		public override string ToString()
		{
			return $"{TouchId}@{Location}";
		}
	}

	public interface ISelectable
	{
	}

}
