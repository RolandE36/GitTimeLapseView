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
	public class DeletionsRenderer : BaseBackgroundRenderer, IBackgroundRenderer {
		private SolidColorBrush deletedBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xdd, 0xdd));
		private Pen pen;

		public DeletionsRenderer(ViewData host) {
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

				if (!host.DiffManager.IsDeleted(host.Snapshot, host.SnapshotParent, linenum)) continue;

				var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, v, 0, 1000).First();
				drawingContext.DrawRectangle(deletedBackground, pen, new Rect(0, rc.Top, textView.ActualWidth, rc.Height));
			}
		}
	}
}
