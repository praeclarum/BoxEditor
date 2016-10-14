using NGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Immutable;

namespace BoxEditor
{
    public class Editor
    {
		readonly TimeSpan MaxConstraintSolveTime = TimeSpan.FromSeconds(0.25);
		                                                   
        Diagram diagram = Diagram.Empty;

		public event EventHandler<BoxesChangedEventArgs> BoxesChanged;
		public event EventHandler SelectionChanged;
		public event EventHandler<BoxEventArgs> ShowBoxEditor;
		public event EventHandler<ArrowEventArgs> ShowArrowEditor;

        public Diagram Diagram
        {
            get
            {
                return diagram;
            }
            set
            {
				var selIds = selection.Where(x => x.Id != null).Select(x => x.Id).ToImmutableHashSet();
				var hoverId = hoverSelection?.Id;
                
				diagram = value;

				var newSels = ImmutableArray.CreateBuilder<ISelectable>();
				foreach (var b in diagram.Boxes)
				{
					if (selIds.Contains(b.Id))
						newSels.Add(b);
					if (b.Id == hoverId)
						hoverSelection = b;
				}
				foreach (var b in diagram.Arrows)
				{
					if (selIds.Contains(b.Id))
						newSels.Add(b);
					if (b.Id == hoverId)
						hoverSelection = b;
				}
				selection = newSels.ToImmutable();

				changedBoxes = ImmutableDictionary<object, Box>.Empty;

				OnDiagramChanged();
            }
        }

		void OnDiagramChanged()
		{
			Redraw?.Invoke();
		}

		void UpdateDiagram(Diagram newDiagram)
		{
			diagram = newDiagram;
		}

		ImmutableDictionary<object, Box> changedBoxes = ImmutableDictionary<object, Box>.Empty;

		void OnBoxChanged(Box b, Box newb)
		{
			var s = selection;
			if (s.Contains(b))
				selection = s.Replace(b, newb);
			if (hoverSelection == b)
				hoverSelection = newb;

			changedBoxes = changedBoxes.SetItem(newb.Id, newb);
		}

		public void ResizeView(Size newViewSize)
        {
        }

		Transform viewToDiagram = Transform.Identity;

		public Transform ViewToDiagramTransform
		{
			get { return viewToDiagram; }
			set { viewToDiagram = value; Redraw?.Invoke(); }
		}
		public Transform DiagramToViewTransform
		{
			get { return viewToDiagram.GetInverse(); }
			set { viewToDiagram = value.GetInverse(); Redraw?.Invoke(); }
		}
		public double DiagramToViewScale
		{
			get { return 1.0 / ViewToDiagramScale; }
		}
		public double ViewToDiagramScale
		{
			get { return viewToDiagram.A; }
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
		Box dragBoxStartSelected = null;
		Arrow dragArrowStartSelected = null;
		int dragBoxHandle = 0;
		Diagram dragDiagram = Diagram.Empty;
		ImmutableArray<Box> dragBoxes = ImmutableArray<Box>.Empty;
		ImmutableArray<Box> dragLastBoxes = ImmutableArray<Box>.Empty;
		Box dragBoxHandleOriginalBox = null;
		Box dragBoxHandleBox = null;

		ImmutableArray<DragGuide> dragGuides = ImmutableArray<DragGuide>.Empty;

		double handleSize = 8;

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
					 let h = b.HitTestHandles(diagramLoc, new Size(handleSize), diagram.Style.DragHandleDistance * viewToDiagram.A)
					 where h != null
					 orderby h.Item2
					 select Tuple.Create(b, h.Item1)).FirstOrDefault();

				var arrowHit = diagram.HitTestArrows(diagramLoc, viewToDiagram.A).FirstOrDefault();

				//				Console.WriteLine ("SELS = {0}", selection.Count);
				if (handleHit != null)
				{
					touchGesture = TouchGesture.DragBoxHandle;
					dragBoxLastDiagramLoc = diagramLoc;
					dragDiagram = diagram;
					dragBoxHandle = handleHit.Item2;
					dragBoxHandleOriginalBox = handleHit.Item1;
					dragBoxHandleBox = dragBoxHandleOriginalBox;
				}
				else if (boxHit != null)
				{
					dragBoxStartSelected = IsSelected(boxHit) ? boxHit : null;
					if (dragBoxStartSelected == null)
					{
						if (touch.IsShiftDown)
						{
							if (!selection.Contains(boxHit))
							{
								Select(selection.Add(boxHit));
							}
						}
						else {
							Select(new[] { boxHit });
						}
						lastEditMenuObject = null;
					}
					touchGesture = TouchGesture.DragSelection;
					dragBoxLastDiagramLoc = diagramLoc;
					dragBoxes = selection.OfType<Box>().ToImmutableArray();
					dragLastBoxes = dragBoxes;
					dragDiagram = diagram;
					dragBoxHandle = 0;
					dragBoxHandleBox = null;
				}
				else if (arrowHit != null)
				{
					dragArrowStartSelected = IsSelected(arrowHit) ? arrowHit : null;
					if (touch.IsShiftDown)
					{
						if (!selection.Contains(arrowHit))
						{
							Select(selection.Add(arrowHit));
						}
					}
					else {
						Select(new[] { arrowHit });
					}
				}
				else {
					touchGesture = TouchGesture.None;
					dragBoxes = ImmutableArray<Box>.Empty;
					dragDiagram = Diagram.Empty;
					SelectNone();
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
						var d = loc - dragBoxLastDiagramLoc;
						var minDist = 8.0 * viewToDiagram.A;
						var mr = dragDiagram.MoveBoxes(dragBoxes, d, !touch.IsCommandDown, minDist, MaxConstraintSolveTime);
						var newd = mr.Item1;
						dragGuides = mr.Item2;
						UpdateDiagram(newd);
						foreach (var b in dragLastBoxes.Zip(mr.Item3, (x, y) => Tuple.Create(x, y)))
						{
							OnBoxChanged(b.Item1, b.Item2);
						}
						dragLastBoxes = mr.Item3;
						Redraw?.Invoke();
					}
					break;
				case TouchGesture.DragBoxHandle:
					if (dragBoxHandleBox != null)
					{
						var loc = ViewToDiagram(activeTouches.Values.First());
						var d = loc - dragBoxLastDiagramLoc;
						//					Console.WriteLine ("MOVE HANDLE = {0}", dragBoxHandle);
						var newb = dragBoxHandleOriginalBox.MoveHandle(dragBoxHandle, d);
						var newd = dragDiagram
							.UpdateBoxes(new[] { Tuple.Create(dragBoxHandleOriginalBox, newb) })
							.PreventOverlaps(ImmutableArray.Create(newb), Point.Zero, MaxConstraintSolveTime);
						UpdateDiagram(newd);
						OnBoxChanged(dragBoxHandleBox, newb);
						hoverSelection = null;
						dragBoxHandleBox = newb;
						Redraw?.Invoke();
					}
					break;
			}
		}

		public void TouchEnded(TouchEvent touch)
        {
			if (changedBoxes.Count > 0)
			{
				BoxesChanged?.Invoke(this, new BoxesChangedEventArgs
				{
					Boxes = changedBoxes.Values.ToImmutableArray()
				});
				changedBoxes = ImmutableDictionary<object, Box>.Empty;
			}
			else {
				if (dragBoxStartSelected != null)
				{
					//Debug.WriteLine("SHOW BOX EDItoR: " + dragBoxStartSelected.Id);
					var r = new Rect(touch.Location, new Size(1, 1));
					ShowBoxEditor?.Invoke(this, new BoxEventArgs(dragBoxStartSelected, r));
				}
				else if (dragArrowStartSelected != null)
				{
					//Debug.WriteLine("SHOW BOX EDItoR: " + dragBoxStartSelected.Id);
					var r = new Rect(touch.Location, new Size(1, 1));
					ShowArrowEditor?.Invoke(this, new ArrowEventArgs(dragArrowStartSelected, r));
				}
			}

			touchGesture = TouchGesture.None;
			dragBoxes = ImmutableArray<Box>.Empty;
			dragLastBoxes = ImmutableArray<Box>.Empty;
			dragDiagram = Diagram.Empty;
			dragGuides = ImmutableArray<DragGuide>.Empty;
			dragBoxStartSelected = null;
			dragArrowStartSelected = null;
			activeTouches.Remove(touch.TouchId);
			Redraw?.Invoke();
		}

		public void TouchCancelled(TouchEvent touch)
        {
			if (changedBoxes.Count > 0)
			{
				BoxesChanged?.Invoke(this, new BoxesChangedEventArgs
				{
					Boxes = changedBoxes.Values.ToImmutableArray()
				});
				changedBoxes = ImmutableDictionary<object, Box>.Empty;
			}
				
			touchGesture = TouchGesture.None;
			dragBoxes = ImmutableArray<Box>.Empty;
			dragLastBoxes = ImmutableArray<Box>.Empty;
			dragDiagram = Diagram.Empty;
			dragGuides = ImmutableArray<DragGuide>.Empty;
			activeTouches.Remove(touch.TouchId);
			Redraw?.Invoke();
		}

		public void MouseMoved(TouchEvent touch)
		{
			var diagramLoc = ViewToDiagram(touch.Location);

			//Debug.WriteLine($"MOUSE {touch} b={boxHit}");

			ISelectable newHover = diagram.HitTestBoxes(diagramLoc).FirstOrDefault();

			if (newHover == null)
			{
				newHover = diagram.HitTestArrows(diagramLoc, viewToDiagram.A).FirstOrDefault();
			}

			if (newHover != hoverSelection)
			{
				//Debug.WriteLine($"CHOVER {newHover} <--- {hoverSelection}");
				hoverSelection =
					selection.Contains(newHover) ? null : newHover;
				Redraw?.Invoke();
			}
		}

		Point magLoc;

		public void MagnificationBegan(double magnification, TouchEvent touch)
		{
			magLoc = ViewToDiagram(touch.Location);
		}
		public void MagnificationChanged(double magnification, TouchEvent touch)
		{
			//Debug.WriteLine("MC {0}", magnification);
			var scale = Transform.Scale(1.0 - magnification, 1.0 - magnification);
			var offset = Transform.Translate(magLoc);
			var offsetn = Transform.Translate(-magLoc);
			var t = offset * scale * offsetn * viewToDiagram;
			viewToDiagram = t;
			Redraw?.Invoke();
		}
		public void MagnificationEnded(double magnification, TouchEvent touch)
		{
		}
		public void MagnificationCancelled(double magnification, TouchEvent touch)
		{
		}

		public void ScrollBegan(Point scroll, TouchEvent touch)
		{
		}
		public void ScrollChanged(Point scroll, TouchEvent touch)
		{
			//Debug.WriteLine("SC {0}", scroll);
			var offset = Transform.Translate(-scroll / DiagramToViewScale);
			var t = offset * viewToDiagram;
			viewToDiagram = t;
			Redraw?.Invoke();
		}
		public void ScrollEnded(Point scroll, TouchEvent touch)
		{
		}
		public void ScrollCancelled(Point scroll, TouchEvent touch)
		{
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

		ImmutableArray<ISelectable> selection = ImmutableArray<ISelectable>.Empty;

		ISelectable hoverSelection = null;

		bool IsSelected(ISelectable s)
		{
			return selection.Contains(s);
		}

		public void Select(IEnumerable<string> ids)
		{
			var allobjs =
				diagram.Boxes.OfType<ISelectable>()
					   .Concat(diagram.Arrows.OfType<ISelectable>())
					   .ToDictionary(x => x.Id);
						

			var sels = new List<ISelectable>();
			foreach (var id in ids)
			{
				ISelectable s;
				if (allobjs.TryGetValue(id, out s))
				{
					sels.Add(s);
				}
			}

			Select(sels); 
		}

		public void Select(IEnumerable<ISelectable> selects)
		{
			var newSels = selects.ToImmutableArray();

			if (!newSels.SequenceEqual(selection))
			{
				selection = newSels;

				if (selection.Contains(hoverSelection))
				{
					hoverSelection = null;
				}

				Redraw?.Invoke();
				SelectionChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public bool HasSelection
		{
			get { return selection.Length > 0; }
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
			public string Id => "_Background";
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

			if ((s != null && IsSelected(s)) || (s == background && selection.Length == 0))
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
			//Debug.WriteLine("DRAW");

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
			var handleLinePen = new Pen(diagram.Style.HandleBorderColor.WithAlpha(0.5), 1.0 * viewToDiagram.A);
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
				var p = diagram.GetArrowPath(a);
				p.Pen = new Pen(a.Style.LineColor, a.Style.ViewDependent ? a.Style.LineWidth * viewToDiagram.A : a.Style.LineWidth);
				p.Draw(canvas);
				ArrowDrawn?.Invoke(a, canvas);
            }
			foreach (var b in diagram.Boxes)
			{
				if (IsSelected(b))
				{
					canvas.DrawRectangle(b.Frame, handleLinePen);
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
			//Debug.WriteLine($"HOVER SEL = {hoverSelection}");
			if (hoverSelection != null)
			{
				var b = hoverSelection as Box;
				if (b != null)
				{
					var selWidth = 2.0 * viewToDiagram.A;
					var f = b.Frame.GetInflated(b.Style.BorderWidth / 2.0 + selWidth / 2.0);
					canvas.DrawRectangle(f, diagram.Style.HoverSelectionColor, selWidth);
				}
				else {
					var a = hoverSelection as Arrow;
					if (a != null)
					{
						var path = diagram.GetArrowPath(a);
						path.Pen = new Pen(diagram.Style.HoverSelectionColor, a.Style.ViewDependent ? a.Style.LineWidth * viewToDiagram.A : a.Style.LineWidth);
						path.Draw(canvas);
					}
				}
			}

			//
			// Drag guides
			//
			foreach (var g in dragGuides)
			{
				canvas.DrawLine(g.Start, g.End, diagram.Style.DragGuideColor, viewToDiagram.A);
			}

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
			DrawArrowHandle(arrow.Start.PortFrame.Center, canvas, handlePen, handleBrush);
			DrawArrowHandle(arrow.End.PortFrame.Center, canvas, handlePen, handleBrush);
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
		public bool IsShiftDown;
		public bool IsCommandDown;
		public bool IsControlDown;

		public override string ToString()
		{
			return $"{TouchId}@{Location}";
		}
	}

	public class BoxesChangedEventArgs : EventArgs
	{
		public ImmutableArray<Box> Boxes { get; set; }
	}

	public class BoxEventArgs : EventArgs
	{
		public Box Box { get; set; }
		public Rect Rect { get; set; }
		public BoxEventArgs(Box box, Rect rect)
		{
			Box = box;
			Rect = rect;
		}
	}

	public class ArrowEventArgs : EventArgs
	{
		public Arrow Arrow { get; set; }
		public Rect Rect { get; set; }
		public ArrowEventArgs(Arrow arrow, Rect rect)
		{
			Arrow = arrow;
			Rect = rect;
		}
	}

	public interface ISelectable
	{
		string Id { get; }
	}

}
