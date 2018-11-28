using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TimeLapseView;
using WpfUI.Renderer;

namespace WpfUI {
	public class TimeLapseLineRenderer : BaseBackgroundRenderer, IBackgroundRenderer {
		private static Pen pen;
		private bool isParentSnapshot;

		private static SolidColorBrush selectedBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xd8, 0x1A));

		static TimeLapseLineRenderer() {
			var blackBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0)); blackBrush.Freeze();
			pen = new Pen(blackBrush, 0.0);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="host"></param>
		/// <param name="isParentSnapshot">Answer is renderer for current snapshot or parent</param>
		public TimeLapseLineRenderer(ViewData host, bool isParentSnapshot) {
			this.host = host;
			this.isParentSnapshot = isParentSnapshot;
		}

		public KnownLayer Layer {
			get { return KnownLayer.Background; }
		}

		public void Draw(TextView textView, DrawingContext drawingContext) {
			if (host == null) return;
			var snapshot = isParentSnapshot ? host.SnapshotParent : host.Snapshot;
			if (snapshot == null) return;

			foreach (var v in textView.VisualLines) {
				var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, v, 0, 1000).First();
				var linenum = v.FirstDocumentLine.LineNumber - 1;
				if (linenum >= snapshot.FileDetails.Count) continue;

				drawingContext.DrawRectangle(GetLineBackgroundBrush(snapshot, linenum), pen, new Rect(0, rc.Top, textView.ActualWidth, rc.Height));
			}
		}
	}
}
