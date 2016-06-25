// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace BoxEditor.Mac
{
	[Register ("ViewController")]
	partial class ViewController
	{
		[Outlet]
		BoxEditor.Mac.EditorView editorView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (editorView != null) {
				editorView.Dispose ();
				editorView = null;
			}
		}
	}
}
