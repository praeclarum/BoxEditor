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
        public event EventHandler<ArrowChangedEventArgs> ArrowChanged;
        public event EventHandler<ArrowEventArgs> ArrowRemoved;

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
			Redraw?.Invoke(this, EventArgs.Empty);
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
			set { viewToDiagram = value; Redraw?.Invoke(this, EventArgs.Empty); }
		}
		public Transform DiagramToViewTransform
		{
			get { return viewToDiagram.GetInverse(); }
			set { viewToDiagram = value.GetInverse(); Redraw?.Invoke(this, EventArgs.Empty); }
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
		Diagram dragOriginalDiagram = Diagram.Empty;
        Diagram dragDiagram = Diagram.Empty;
        ImmutableArray<Box> dragBoxes = ImmutableArray<Box>.Empty;
		ImmutableArray<Box> dragLastBoxes = ImmutableArray<Box>.Empty;
		Box dragBoxHandleOriginalBox = null;
		Box dragBoxHandleBox = null;
        Point dragArrowLastDiagramLoc = Point.Zero;
        Arrow dragArrow = null;
        Box dragArrowEndBox = null;
        Arrow dragArrowExisting = null;
        bool dragArrowDragged = false;
        Tuple<Box, Port> dragArrowPortHit = null;
        Tuple<Box, Port> dragArrowSnap = null;

        ImmutableArray<DragGuide> dragGuides = ImmutableArray<DragGuide>.Empty;

		double handleSize = 8;

        public PortRef? DraggingPort => dragArrow?.Start;

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

                var portHit = diagram.HitTestPorts(diagramLoc, viewToDiagram.A).FirstOrDefault();
                //Debug.WriteLine("PORT HIT " + portHit);

				var arrowHit = diagram.HitTestArrows(diagramLoc, viewToDiagram.A).FirstOrDefault(x => x.Id != "TEMPDRAG");

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
                else if (portHit != null)
                {
                    BeginDragArrow(diagramLoc, portHit);
                    dragArrowExisting = null;
                    dragOriginalDiagram = diagram;
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
                    var startD = diagramLoc.DistanceTo (arrowHit.Start.PortPoint);
                    var endD = diagramLoc.DistanceTo(arrowHit.End.PortPoint);
                    var nearStart = startD < endD;

                    var pr = nearStart ? arrowHit.End : arrowHit.Start;
                    BeginDragArrow(diagramLoc, Tuple.Create(pr.Box, pr.Port));
                    dragArrowExisting = arrowHit;
                    dragOriginalDiagram = diagram;
                }
                else {
					touchGesture = TouchGesture.None;
					dragBoxes = ImmutableArray<Box>.Empty;
					dragDiagram = Diagram.Empty;
					SelectNone();

                    touchGesture = TouchGesture.Pan;
                    panLastCenter = activeTouches.Values.Aggregate(Point.Zero, (a, x) => a + x) / activeTouches.Count;
                    panLastRadius = 1.0;
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
						var radius = activeTouches.Count <= 1 ? 1.0 : activeTouches.Values.Average(x => x.DistanceTo(panLastCenter));
						viewToDiagram =
							viewToDiagram *
							Transform.Translate(panLastCenter - center) *
							Transform.ScaleAt(panLastRadius / radius, center);

						panLastCenter = center;
						panLastRadius = radius;
						Redraw?.Invoke(this, EventArgs.Empty);
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
						Redraw?.Invoke(this, EventArgs.Empty);
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
						Redraw?.Invoke(this, EventArgs.Empty);
					}
					break;
                case TouchGesture.DragArrow:
                    {
                        var loc = ViewToDiagram(activeTouches.Values.First());
                        if (!dragArrowDragged)
                        {
                            FirstMoveDragArrow(loc);
                        }
                        dragArrowDragged = true;
                        var d = loc - dragArrowLastDiagramLoc;
                        //					Console.WriteLine ("MOVE HANDLE = {0}", dragBoxHandle);
                        var newb = dragArrowEndBox.Move(d);
                        dragArrowSnap = SnapArrow(loc);
                        if (dragArrowSnap != null)
                        {
                            var point = dragArrowSnap.Item2.GetPoint(dragArrowSnap.Item1);
                            var f = newb.Frame;
                            var nf = new Rect(point.X - f.Width / 2, point.Y - f.Height / 2, f.Width, f.Height);
                            var p = newb.Ports[0].WithDirection(dragArrowSnap.Item2.Direction);
                            newb = newb.WithFrame(nf, new Rect(loc, Size.Zero)).WithPorts(ImmutableArray.Create(p));
                        }
                        var newd = dragDiagram
                            .UpdateBoxes(new[] { Tuple.Create(dragArrowEndBox, newb) });
                        UpdateDiagram(newd);
                        hoverSelection = null;
                        Redraw?.Invoke(this, EventArgs.Empty);
                    }
                    break;
            }
        }

		public void TouchEnded(TouchEvent touch)
        {
            TouchEndedOrCancelled(touch, false);
        }

		public void TouchCancelled(TouchEvent touch)
        {
            TouchEndedOrCancelled(touch, true);
		}

        void TouchEndedOrCancelled(TouchEvent touch, bool cancelled)
        {
            if (changedBoxes.Count > 0)
            {
                BoxesChanged?.Invoke(this, new BoxesChangedEventArgs
                {
                    Boxes = changedBoxes.Values.ToImmutableArray()
                });
                changedBoxes = ImmutableDictionary<object, Box>.Empty;
            }
            else if (!cancelled)
            {
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

            if (touchGesture == TouchGesture.DragArrow)
            {
                if (dragArrowExisting != null && !dragArrowDragged)
                {
                    dragArrowStartSelected = IsSelected(dragArrowExisting) ? dragArrowExisting : null;
                    if (touch.IsShiftDown)
                    {
                        if (!selection.Contains(dragArrowExisting))
                        {
                            Select(selection.Add(dragArrowExisting));
                        }
                    }
                    else
                    {
                        Select(new[] { dragArrowExisting });
                    }
                }
                else
                {
                    if (!cancelled && dragArrowSnap != null)
                    {
                        var newArrow = dragArrow.WithEnd(dragArrowSnap.Item1, dragArrowSnap.Item2);
                        var d = dragDiagram.RemoveBox(dragArrow.EndBox).UpdateArrow(dragArrow, newArrow);
                        UpdateDiagram(d);
                        ArrowChanged?.Invoke(this, new ArrowChangedEventArgs { OldArrow = null, NewArrow = newArrow });
                    }
                    else
                    {
                        if (dragArrow != null)
                        {
                            var d = dragDiagram.RemoveBox(dragArrow.EndBox).RemoveArrow(dragArrow);
                            UpdateDiagram(d);
                        }
                        else
                        {
                            UpdateDiagram(dragDiagram);
                        }
                    }
                }
            }

            touchGesture = TouchGesture.None;
            dragBoxes = ImmutableArray<Box>.Empty;
            dragLastBoxes = ImmutableArray<Box>.Empty;
            dragDiagram = Diagram.Empty;
            dragOriginalDiagram = null;
            dragGuides = ImmutableArray<DragGuide>.Empty;
            dragBoxStartSelected = null;
            dragArrowStartSelected = null;
            dragArrow = null;
            dragArrowEndBox = null;
            dragArrowSnap = null;
            dragArrowDragged = false;
            dragArrowPortHit = null;
            activeTouches.Remove(touch.TouchId);
            Redraw?.Invoke(this, EventArgs.Empty);
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

			newHover = selection.Contains(newHover) ? null : newHover;

			if (newHover != hoverSelection)
			{
				//Debug.WriteLine($"CHOVER {newHover} <--- {hoverSelection}");
				hoverSelection = newHover;
				Redraw?.Invoke(this, EventArgs.Empty);
				SelectionChanged?.Invoke(this, EventArgs.Empty);
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
			Redraw?.Invoke(this, EventArgs.Empty);
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
			Redraw?.Invoke(this, EventArgs.Empty);
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
            DragArrow,
			Pan,
		}

        Tuple<Box, Port> SnapArrow(Point point)
        {
            var ports = diagram.HitTestPorts(point, ViewToDiagramScale);
            var r = ports.FirstOrDefault(x => x.Item1.Id != dragArrow.StartBox.Id && x.Item2 != dragArrow.Start.Port &&
                x.Item1.Id != dragArrow.EndBox.Id && x.Item2 != dragArrow.End.Port &&
                diagram.CanConnectPorts (dragArrow.Start, new PortRef(x.Item1, x.Item2)));
            if (r == null)
            {
                var boxes = diagram.HitTestBoxes(point);
                var q = from b in boxes
                        from p in b.Ports
                        where diagram.CanConnectPorts (dragArrow.Start, new PortRef(b, p))
                        let d = p.GetPoint(b).DistanceTo(point)
                        orderby d
                        select Tuple.Create(b, p);
                r = q.FirstOrDefault();
            }
            return r;
        }

        void BeginDragArrow(Point diagramLoc, Tuple<Box, Port> portHit)
        {
            touchGesture = TouchGesture.DragArrow;
            dragArrowLastDiagramLoc = diagramLoc;
            dragArrowDragged = false;
            dragArrowPortHit = portHit;
        }

        void FirstMoveDragArrow(Point diagramLoc)
        {
            var portHit = dragArrowPortHit;
            touchGesture = TouchGesture.DragArrow;
            dragArrowLastDiagramLoc = diagramLoc;
            var dragBoxPort = new Port("TEMPDRAGPORT", null, 256, uint.MaxValue, int.MaxValue, new Point(0.5, 0.5), new Size(11, 11), Point.Zero);
            var dragBoxFrame = new Rect(diagramLoc - new Point(11, 11), new Size(22, 22));
            var dragBox = new Box("TEMPDRAGBOX", null, dragBoxFrame, new Rect(diagramLoc, Size.Zero), BoxStyle.Default, new[] { dragBoxPort }.ToImmutableArray());
            var endRef = new PortRef(dragBox, dragBoxPort);
            var startRef = new PortRef(portHit.Item1, portHit.Item2);
            dragArrow = new Arrow("TEMPDRAGARROW", null, ArrowStyle.Default, startRef, endRef);
            dragArrowEndBox = dragBox;
            dragDiagram = diagram.AddBox(dragArrowEndBox).AddArrow(dragArrow);

            if (dragArrowExisting != null)
            {
                dragDiagram = dragDiagram.RemoveArrow(dragArrowExisting);
                ArrowRemoved?.Invoke(this, new ArrowEventArgs(dragArrowExisting, new Rect(diagramLoc, Size.Zero)));
            }
        }

        #endregion

        #region Selection

        ImmutableArray<ISelectable> selection = ImmutableArray<ISelectable>.Empty;

		ISelectable hoverSelection = null;

        bool IsSelected(ISelectable s)
        {
            return selection.Contains(s);
            //return selection.Any(x => {
            //    var r = x.Id == s.Id;
            //    //Debug.WriteLine($"ISEL? {x.Id} == {s.Id} == {r}");
            //    return r;
            //});
		}

		public void Select(IEnumerable<string> ids, string hoverId)
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

			ISelectable hover;
			allobjs.TryGetValue(hoverId, out hover);

			SetSelection(sels.ToImmutableArray(), hover, false); 
		}

		void Select(IEnumerable<ISelectable> selects)
		{
			var newSels = selects.ToImmutableArray();
			SetSelection(newSels, newSels.Contains(hoverSelection) ? null : hoverSelection, true);
		}

		void SetSelection(ImmutableArray<ISelectable> newSels, ISelectable newHover, bool userInitiated)
		{
			if (newHover != hoverSelection || !newSels.SequenceEqual(selection))
			{
				selection = newSels;

				hoverSelection = newHover;
				//Debug.WriteLine("SCH SetSelection HOVER === " + newHover);

				Redraw?.Invoke(this, EventArgs.Empty);
				if (userInitiated)
				{
					SelectionChanged?.Invoke(this, EventArgs.Empty);
				}
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

		public ISelectable HoverSelection
		{
			get { return hoverSelection; }
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
			Redraw?.Invoke(this, EventArgs.Empty);
		}

		#endregion

		#region Drawing

		public event EventHandler Redraw;
        public DiagramDrawer Drawer { get; set; } = new DiagramDrawer();

        public void Draw(ICanvas canvas, Rect dirtyViewRect)
        {
            Drawer.Draw(Diagram, canvas, dirtyViewRect, viewToDiagram, () => this.DrawEdits (canvas));
        }

        void DrawEdits(ICanvas canvas)
        {
            var handlePen = new Pen(diagram.Style.HandleBorderColor, 1.0 * viewToDiagram.A);
            var handleLinePen = new Pen(diagram.Style.HandleBorderColor.WithAlpha(0.5), 1.0 * viewToDiagram.A);
            var handleBrush = new SolidBrush(diagram.Style.HandleBackgroundColor);

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

            if (DraggingPort.HasValue) {
                var dport = DraggingPort.Value;
                foreach (var b in diagram.Boxes) {
                    foreach (var p in b.Ports)
                    {
                        DrawPortWhileConnecting(canvas, dport, b, p);
                    }
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
                else
                {
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
        }

        protected virtual void DrawPortWhileConnecting(ICanvas canvas, PortRef fromPort, Box box, Port port)
        {
            if (port.Id == "TEMPDRAGPORT")
                return;
            var color = diagram.CanConnectPorts(fromPort, new PortRef(box, port)) ? Colors.Green : Colors.Red;
            var rect = port.GetFrame(box);
            canvas.FillEllipse(rect, color);
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

    public class PortDrawnEventArgs : EventArgs
    {
        public Box Box { get; set; }
        public Port Port { get; set; }
        public ICanvas Canvas { get; }
        public PortDrawnEventArgs(ICanvas canvas)
        {
            Canvas = canvas;
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

    public class ArrowChangedEventArgs : EventArgs
    {
        public Arrow OldArrow;
        public Arrow NewArrow;
    }

	public interface ISelectable
	{
		string Id { get; }
	}

}
