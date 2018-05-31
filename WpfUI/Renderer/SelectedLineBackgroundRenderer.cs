using ICSharpCode.AvalonEdit.Rendering;
using System.Linq;
using System.Windows.Media;
using TimeLapseView;
using System.Windows;

namespace WpfUI.Renderer {
	/// <summary>
	/// Background render for selected commit and line life
	/// </summary>
	public class SelectedLineBackgroundRenderer : IBackgroundRenderer {
		/// <summary>
		/// Selected line background color
		/// </summary>
		private static SolidColorBrush selectedLineBackground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD8, 0x1A));

		/// <summary>
		/// Commit background color
		/// </summary>
		// TODO: Less yelow
		private static SolidColorBrush selectedCommitBackground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0x19));

		private static Pen pen;
		private ViewData host;

		public SelectedLineBackgroundRenderer(ViewData host) {
			this.host = host;
		}

		public KnownLayer Layer {
			get { return KnownLayer.Background; }
		}

		public void Draw(TextView textView, DrawingContext drawingContext) {
			if (host == null) return;

			foreach (var v in textView.VisualLines) {
				var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, v, 0, 1000).First();
				var linenum = v.FirstDocumentLine.LineNumber - 1;
				if (linenum >= host.Snapshot.FileDetails.Count) continue;

				var brush = default(Brush);

				if (host.SelectedSnapshotIndex != -1 && host.Snapshot.GetLineBirth(linenum) == host.SelectedSnapshotIndex) {
					brush = selectedCommitBackground;
				}

				if (host.SelectedSnapshotIndex != -1 && host.Snapshot.FileDetails[linenum].LID == host.SelectedLineLID) {
					brush = selectedLineBackground;
				}

				drawingContext.DrawRectangle(brush, pen, new Rect(0, rc.Top, textView.ActualWidth, rc.Height));
			}
		}
	}
}
