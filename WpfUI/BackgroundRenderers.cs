using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TimeLapseView;

namespace WpfUI {

	// TODO: Code cleanup
	
	public class TimeLapseLineBackgroundRenderer : IBackgroundRenderer {
		static Pen pen;

		private static SolidColorBrush selectedBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xd8, 0x1A));

		FileHistoryManager host;
		int revision;
		int revisionsCount;

		static TimeLapseLineBackgroundRenderer() {
			var blackBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0)); blackBrush.Freeze();
			pen = new Pen(blackBrush, 0.0);
		}

		public TimeLapseLineBackgroundRenderer(FileHistoryManager host, int currentRevisionNumber, int revisionsCount) {
			this.host = host;
			revision = currentRevisionNumber;
			this.revisionsCount = revisionsCount;
		}

		public KnownLayer Layer {
			get { return KnownLayer.Background; }
		}

		public void Draw(TextView textView, DrawingContext drawingContext) {

			foreach (var v in textView.VisualLines) {
				var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, v, 0, 1000).First();
				var linenum = v.FirstDocumentLine.LineNumber - 1;
				if (linenum >= host.CurrentSnapshot.Lines.Count) continue;

				var brush = default(Brush);

				if (host.SelectedSnapshotIndex != -1 &&
					// TODO: Select only one line ---- linenum == host.SelectedLine 
					host.CurrentSnapshot.Lines[linenum].SequenceStart == host.SelectedSnapshotIndex) {
					brush = selectedBackground;
				} else {
					// TODO: Move brush creation to separate class using percentage values (current, max, step)
					var lineSnapshotsNumber = revisionsCount - host.CurrentSnapshot.Lines[linenum].SequenceStart;
					var lineLifeTimePercent = (lineSnapshotsNumber * 100.0 / revision) / 100;
					var byteColor = Convert.ToByte(255 - (60 + 180 * lineLifeTimePercent));

					brush = new SolidColorBrush(Color.FromRgb(byteColor, 0xff, byteColor));
				}

				drawingContext.DrawRectangle(brush, pen, new Rect(0, rc.Top, textView.ActualWidth, rc.Height));
			}
		}
	}

	public class IncrementalDiffLineBackgroundRenderer : IBackgroundRenderer {
		static Pen pen;

		private static SolidColorBrush selectedBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xd8, 0x1A));

		static SolidColorBrush removedBackground;
		static SolidColorBrush addedBackground;
		static SolidColorBrush headerBackground;
		static SolidColorBrush emptyBackground;

		FileHistoryManager host;

		static IncrementalDiffLineBackgroundRenderer() {
			removedBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xdd, 0xdd)); removedBackground.Freeze();
			addedBackground = new SolidColorBrush(Color.FromRgb(0xdd, 0xff, 0xdd)); addedBackground.Freeze();
			headerBackground = new SolidColorBrush(Color.FromRgb(0xf8, 0xf8, 0xff)); headerBackground.Freeze();
			emptyBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff)); emptyBackground.Freeze();

			var blackBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0)); blackBrush.Freeze();
			pen = new Pen(blackBrush, 0.0);
		}

		public IncrementalDiffLineBackgroundRenderer(FileHistoryManager host) {
			this.host = host;
		}

		public KnownLayer Layer {
			get { return KnownLayer.Background; }
		}

		public void Draw(TextView textView, DrawingContext drawingContext) {
			foreach (var v in textView.VisualLines) {
				var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, v, 0, 1000).First();
				var linenum = v.FirstDocumentLine.LineNumber - 1;
				if (linenum >= host.CurrentSnapshot.Lines.Count) continue;

				var brush = default(Brush);

				switch (host.CurrentSnapshot.Lines[linenum].State) {
					case LineState.Modified:
					case LineState.Inserted:
						brush = addedBackground;
						break;
					case LineState.Unchanged:
					default:
						brush = emptyBackground;
						break;
				}

				drawingContext.DrawRectangle(brush, pen, new Rect(0, rc.Top, textView.ActualWidth, rc.Height));
			}
		}
	}
}
