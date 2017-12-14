using ICSharpCode.AvalonEdit.Rendering;
using System.Linq;
using System.Windows.Media;
using TimeLapseView;
using System.Windows;

namespace WpfUI.Renderer {
	/// <summary>
	/// Background render for differences between current and previous commit
	/// </summary>
	public class IncrementalDiffLineBackgroundRenderer : IBackgroundRenderer {
		private static Pen pen;

		private static SolidColorBrush addedBackground = new SolidColorBrush(Color.FromRgb(0xdd, 0xff, 0xdd));
		private static SolidColorBrush notChangedBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));

		private ViewData host;

		public IncrementalDiffLineBackgroundRenderer(ViewData host) {
			this.host = host;
			var blackBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0)); blackBrush.Freeze();
			pen = new Pen(blackBrush, 0.0);
		}

		public KnownLayer Layer {
			get { return KnownLayer.Background; }
		}

		public void Draw(TextView textView, DrawingContext drawingContext) {
			if (host == null) return;

			foreach (var v in textView.VisualLines) {
				var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, v, 0, 1000).First();
				var linenum = v.FirstDocumentLine.LineNumber - 1;
				if (linenum >= host.Snapshot.Lines.Count) continue;

				var brush = default(Brush);

				switch (host.Snapshot.Lines[linenum].State) {
					case LineState.Modified:
					case LineState.Inserted:
						brush = addedBackground;
						break;
					case LineState.Unchanged:
					default:
						brush = notChangedBackground;
						break;
				}

				drawingContext.DrawRectangle(brush, pen, new Rect(0, rc.Top, textView.ActualWidth, rc.Height));
			}
		}
	}
}
