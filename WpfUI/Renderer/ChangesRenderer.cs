using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TimeLapseView;

namespace WpfUI.Renderer {
	public class ChangesRenderer : BaseBackgroundRenderer, IBackgroundRenderer {
		private SolidColorBrush selectedBackground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0x19));
		private Pen pen;

		public ChangesRenderer(ViewData host) {
			this.host = host;
			var blackBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
			blackBrush.Freeze();
			pen = new Pen(blackBrush, 0.0);
		}

		public KnownLayer Layer {
			get { return KnownLayer.Background; }
		}

		public void Draw(TextView textView, DrawingContext drawingContext) {
			if (host == null) return;

			foreach (var v in textView.VisualLines) {
				var linenum = v.FirstDocumentLine.LineNumber - 1;
				if (linenum >= host.Snapshot.FileDetails.Count) continue;

				var val = host.DiffManager.GetChangesType(host.Snapshot, host.SnapshotParent, linenum);
				if (val != LineState.Inserted && val != LineState.Modified) continue;

				var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, v, 0, 1000).First();
				drawingContext.DrawRectangle(selectedBackground, pen, new Rect(0, rc.Top, textView.ActualWidth, rc.Height));
			}
		}
	}
}
