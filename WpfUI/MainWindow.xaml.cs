using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TimeLapseView;
using ICSharpCode.AvalonEdit.Document;

namespace WpfUI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		public MainWindow() {
			InitializeComponent();
		}

		FileHistoryManager manager;

		protected override void OnInitialized(EventArgs e) {
			base.OnInitialized(e);

			manager = new FileHistoryManager(@"C:\git\ok-booking\OkBooking\BAL\ExchangeManager.cs");
			manager.GetCommitsHistory();
			slHistoy.Maximum = manager.Snapshots.Count;
			slHistoy.Value = manager.Snapshots.Count;
			slHistoy.Minimum = 1;
			tbCode.Text = manager.Snapshots[0].File;
		}

		private void slHistoyValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			int index = (int) (slHistoy.Maximum - slHistoy.Value);

			var ttt = new DiffLineBackgroundRenderer(manager.Snapshots[index]);
			tbCode.TextArea.TextView.BackgroundRenderers.Clear();// TODO:
			tbCode.TextArea.TextView.BackgroundRenderers.Add(ttt);
			tbCode.Text = manager.Snapshots[index].File;
			lblDetails.Content = manager.Snapshots[index].Commit.Description;
		}
	}



	public class DiffLineBackgroundRenderer : IBackgroundRenderer {
		static Pen pen;

		static SolidColorBrush removedBackground;
		static SolidColorBrush addedBackground;
		static SolidColorBrush headerBackground;
		static SolidColorBrush emptyBackground;

		Snapshot host;

		static DiffLineBackgroundRenderer() {
			removedBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xdd, 0xdd)); removedBackground.Freeze();
			addedBackground = new SolidColorBrush(Color.FromRgb(0xdd, 0xff, 0xdd)); addedBackground.Freeze();
			headerBackground = new SolidColorBrush(Color.FromRgb(0xf8, 0xf8, 0xff)); headerBackground.Freeze();
			emptyBackground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff)); emptyBackground.Freeze();

			var blackBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0)); blackBrush.Freeze();
			pen = new Pen(blackBrush, 0.0);
		}

		public DiffLineBackgroundRenderer(Snapshot host) {
			this.host = host;
		}

		public KnownLayer Layer {
			get { return KnownLayer.Background; }
		}

		public void Draw(TextView textView, DrawingContext drawingContext) {
			foreach (var v in textView.VisualLines) {
				var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, v, 0, 1000).First();
				// NB: This lookup to fetch the doc line number isn't great, we could
				// probably do it once then just increment.
				var linenum = v.FirstDocumentLine.LineNumber - 1;
				if (linenum >= host.Lines.Count) continue;

				var brush = default(Brush);
				
				switch (host.Lines[linenum].State) {
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
