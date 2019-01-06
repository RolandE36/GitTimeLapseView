using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TimeLapseView;

namespace WpfUI.Renderer {
	public class ScrollRenderer : BaseBackgroundRenderer, IBackgroundRenderer {
		private const int SCROLL_VIEWER_BUTTON_SIZE = 17;
		private const int LINE_HEIGHT = 1;
		private const int LINE_WIDTH = 10;
		private const int RECTANGLE_BORDER = 1;
		private readonly SolidColorBrush blackBrush = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

		private Canvas canvas;

		public ScrollRenderer(ViewData host, Canvas canvas) {
			this.canvas = canvas;
			this.host = host;
		}

		public KnownLayer Layer {
			get { return KnownLayer.Background; }
		}

		public void Draw(TextView textView, DrawingContext drawingContext) {
			var changes = host.DiffManager.GetChanges(host.Snapshot, host.SnapshotParent);
			var deletions = host.DiffManager.GetDeletions(host.Snapshot, host.SnapshotParent);
			var canvasHeight = canvas.ActualHeight - 2.0 * SCROLL_VIEWER_BUTTON_SIZE;
			var firstLineNumber = textView.VisualLines.First().FirstDocumentLine.LineNumber;
			var lastLineNumber = textView.VisualLines.Last().FirstDocumentLine.LineNumber;
			var proportion = canvasHeight / host.Snapshot.FileLinesCount;
			canvas.Children.Clear();

			// Current code view
			var scrollHeight = textView.VisualLines.Count * proportion;
			var src = new Rectangle();
			src.StrokeThickness = RECTANGLE_BORDER;
			src.Stroke = blackBrush;
			src.Width = LINE_WIDTH * 2;
			src.Height = textView.VisualLines.Count * proportion + RECTANGLE_BORDER * 2;
			Canvas.SetLeft(src, 0);
			Canvas.SetTop(src, firstLineNumber * proportion + SCROLL_VIEWER_BUTTON_SIZE - RECTANGLE_BORDER);

			canvas.Children.Add(src);

			// Show changes
			if (changes != null) {
				foreach (var change in changes) {
					var top = change.Key * proportion;
					var rectangle = new Rectangle();
					rectangle.Fill = ColorPalette.CHANGES;
					rectangle.Width = LINE_WIDTH - 2 * RECTANGLE_BORDER;
					rectangle.Height = LINE_HEIGHT;
					Canvas.SetLeft(rectangle, LINE_WIDTH + RECTANGLE_BORDER);
					Canvas.SetTop(rectangle, top + SCROLL_VIEWER_BUTTON_SIZE - 2 * RECTANGLE_BORDER);
					canvas.Children.Add(rectangle);
				}
			}

			// Show deletions
			if (host.SnapshotParent == null) return;
			var totalDeletionsLines = host.SnapshotParent.FileLinesCount;
			if (deletions != null) {
				foreach (var change in deletions) {
					var top = host.DiffManager.GetParentLineNumber(host.SnapshotParent, host.Snapshot, change.Key) * proportion;
					var rectangle = new Rectangle();
					rectangle.Fill = ColorPalette.DELETED;
					rectangle.Width = LINE_WIDTH - 2 * RECTANGLE_BORDER;
					rectangle.Height = LINE_HEIGHT;
					Canvas.SetLeft(rectangle, RECTANGLE_BORDER);
					Canvas.SetTop(rectangle, top + SCROLL_VIEWER_BUTTON_SIZE - 2 * RECTANGLE_BORDER);
					canvas.Children.Add(rectangle);
				}
			}
		}
	}
}
