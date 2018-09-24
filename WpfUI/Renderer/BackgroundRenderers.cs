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

	// TODO: Code cleanup. Move to separate folder
	
	public class TimeLapseLineBackgroundRenderer : BaseBackgroundRenderer, IBackgroundRenderer {
		static Pen pen;

		private static SolidColorBrush selectedBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xd8, 0x1A));

		static TimeLapseLineBackgroundRenderer() {
			var blackBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0)); blackBrush.Freeze();
			pen = new Pen(blackBrush, 0.0);
		}

		public TimeLapseLineBackgroundRenderer(ViewData host) {
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

				drawingContext.DrawRectangle(GetLineBackgroundBrush(linenum), pen, new Rect(0, rc.Top, textView.ActualWidth, rc.Height));
			}
		}
	}
}
