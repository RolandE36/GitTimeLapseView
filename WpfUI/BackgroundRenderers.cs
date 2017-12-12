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

	// TODO: Code cleanup. Move to separate folder
	
	public class TimeLapseLineBackgroundRenderer : IBackgroundRenderer {
		static Pen pen;

		private static SolidColorBrush selectedBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xd8, 0x1A));

		ViewData host;
		int revision;
		int revisionsCount;

		static TimeLapseLineBackgroundRenderer() {
			var blackBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0)); blackBrush.Freeze();
			pen = new Pen(blackBrush, 0.0);
		}

		public TimeLapseLineBackgroundRenderer(ViewData host, int currentRevisionNumber, int revisionsCount) {
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
				if (linenum >= host.Snapshot.Lines.Count) continue;

				var brush = default(Brush);

				// TODO: Move brush creation to separate class using percentage values (current, max, step)
				var lineSnapshotsNumber = revisionsCount - host.Snapshot.Lines[linenum].SequenceStart;
				var lineLifeTimePercent = (lineSnapshotsNumber * 100.0 / revision) / 100;
				// TODO: Fix calcultions
				if (lineLifeTimePercent > 1) lineLifeTimePercent = 1;
				var byteColor = Convert.ToByte(255 - (40 + 150 * lineLifeTimePercent));

				brush = new SolidColorBrush(Color.FromRgb(byteColor, 0xff, byteColor));
				

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

		ViewData host;

		static IncrementalDiffLineBackgroundRenderer() {
			removedBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xdd, 0xdd)); removedBackground.Freeze();
			addedBackground = new SolidColorBrush(Color.FromRgb(0xdd, 0xff, 0xdd)); addedBackground.Freeze();
			headerBackground = new SolidColorBrush(Color.FromRgb(0xf8, 0xf8, 0xff)); headerBackground.Freeze();
			emptyBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff)); emptyBackground.Freeze();

			var blackBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0)); blackBrush.Freeze();
			pen = new Pen(blackBrush, 0.0);
		}

		public IncrementalDiffLineBackgroundRenderer(ViewData host) {
			this.host = host;
		}

		public KnownLayer Layer {
			get { return KnownLayer.Background; }
		}

		public void Draw(TextView textView, DrawingContext drawingContext) {
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
						brush = emptyBackground;
						break;
				}

				drawingContext.DrawRectangle(brush, pen, new Rect(0, rc.Top, textView.ActualWidth, rc.Height));
			}
		}
	}
}
