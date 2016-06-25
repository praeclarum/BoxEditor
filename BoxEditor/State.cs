using NGraphics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace BoxEditor
{
	public class State
	{
		public readonly bool IsSelected;
		public readonly bool IsHover;

		public static State None = new State(false, false);
		public static State Selected = new State(true, false);
		public static State Hover = new State(false, true);
		public static State SelectedAndHover = new State(true, true);

		public State(bool isSelected, bool isHover)
		{
			IsSelected = isSelected;
			IsHover = isHover;   
		}
	}	
}
