using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using TimeLapseView;

namespace WpfUI.Renderer {
	public class LineDetailsRenderer : BaseBackgroundRenderer, IBackgroundRenderer {
		private Canvas canvas;

		private readonly SolidColorBrush BlackBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10));

		public LineDetailsRenderer(ViewData host, Canvas canvas) {
			this.canvas = canvas;
			this.host = host;
		}

		public KnownLayer Layer {
			get { return KnownLayer.Background; }
		}

		public void Draw(TextView textView, DrawingContext drawingContext) {
			canvas.Children.Clear();

			for (var i = 0; i < textView.VisualLines.Count; i++) {
				var linenum = textView.VisualLines[i].FirstDocumentLine.LineNumber - 1;
				if (linenum >= host.Snapshot.FileDetails.Count) continue;

				var snapshot = host.Snapshots[host.GetLineBirth(linenum)];
				var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, textView.VisualLines[i], 0, 1000).First();

				TextBlock textBlock = new TextBlock();
				// TODO: Use windows DateTime Format settings
				textBlock.Text = snapshot.Commit.Date.ToString("dd.MM.yyyy") + " " + Truncate(snapshot.Commit.Author, 10);
				textBlock.ToolTip = snapshot.Commit.Date.ToString("dd.MM.yyyy hh:mm") + " " + snapshot.Commit.Author + "\n" + snapshot.Commit.Description;
				textBlock.Foreground = BlackBrush;
				textBlock.Background = GetLineBackgroundBrush(linenum);
				textBlock.Width = canvas.Width;
				textBlock.FontFamily = new FontFamily("Consolas");

				Canvas.SetLeft(textBlock, 0);
				Canvas.SetTop(textBlock, rc.Top);
				canvas.Children.Add(textBlock);
			}
		}

		/// <summary>
		/// Truncate string length
		/// </summary>
		private string Truncate(string value, int maxLength) {
			if (string.IsNullOrEmpty(value)) return value;
			return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "…";
		}
	}
}
