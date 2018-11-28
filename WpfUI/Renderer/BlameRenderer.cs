using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TimeLapseView;

namespace WpfUI.Renderer {
	public class BlameRenderer : BaseBackgroundRenderer, IBackgroundRenderer {
		private Canvas canvas;

		private readonly SolidColorBrush BlackBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10));

		public BlameRenderer(ViewData host, Canvas canvas) {
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

				var snapshot = host.Snapshots[host.GetLineBirth(host.Snapshot, linenum)];
				var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, textView.VisualLines[i], 0, 1000).First();

				// Prevent drawing not fully visible lines
				if (rc.Top < -5) continue;
				if (textView.ActualHeight - rc.Top < 10) continue;

				TextBlock textBlock = new TextBlock();
				textBlock.Text = snapshot.Date.ToString(Constants.DATE_FORMAT) + " " + Truncate(snapshot.Author, 10);
				textBlock.ToolTip = snapshot.Tooltip;
				textBlock.Foreground = BlackBrush;
				textBlock.Background = GetLineBackgroundBrush(host.Snapshot, linenum);
				textBlock.Width = canvas.Width;
				textBlock.FontFamily = new FontFamily("Consolas");
				textBlock.Tag = snapshot.Sha;
				textBlock.MouseDown += TextBlock_MouseDown;

				Canvas.SetLeft(textBlock, 0);
				Canvas.SetTop(textBlock, rc.Top);
				canvas.Children.Add(textBlock);
			}
		}

		/// <summary>
		/// Select snapshot related to line
		/// </summary>
		private void TextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 2) {
				var sha = (string)(sender as TextBlock).Tag;
				host.SelectSnapshot(host.ShaDictionary[sha].Index);
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
