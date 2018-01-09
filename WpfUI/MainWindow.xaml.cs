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
using Microsoft.Win32;
using WpfUI.Renderer;

namespace WpfUI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private const string APP_TITLE = "Git Time Lapse View";

		public MainWindow() {
			InitializeComponent();
		}

		ViewData View;

		FileHistoryManager manager;

		class MovieData {
			public string Title { get; set; }
			public string ImageData { get; set; }
		}

		protected override void OnInitialized(EventArgs e) {
			base.OnInitialized(e);
			// TODO: Spaces.......
			lblCommitMessageLabel.Text = "\nMessage ";
			lblFilePathLabel.Text = "\nFile         ";
		}

		private void slHistoyValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			if (View == null) return;
			SelectedCommitIndexChanged((int)(slHistoy.Maximum - slHistoy.Value));
		}

		private void lvVerticalHistoryPanel_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			SelectedCommitIndexChanged(lvVerticalHistoryPanel.SelectedIndex);
		}

		/// <summary>
		/// On changed current view index event handler.
		/// </summary>
		/// <param name="index">new selected index</param>
		private void SelectedCommitIndexChanged(int index) {
			if (View == null || index < 0) return;

			View.SnapshotIndex = index;
			tbCode.Text = View.Snapshots[index].File;
			slHistoy.Value = slHistoy.Maximum - index;
			lvVerticalHistoryPanel.SelectedIndex = index;
			UpdateCommitDetails(View.Snapshots[index]);
		}

		private void btnBrowseFile_Click(object sender, RoutedEventArgs e) {
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Multiselect = false;
			openFileDialog.RestoreDirectory = true;
			if (openFileDialog.ShowDialog() == true) {
				try {
					var filename = openFileDialog.FileNames.FirstOrDefault();

					manager = new FileHistoryManager(filename);
					Title = APP_TITLE + ": " + manager.filePath;
					View = new ViewData(manager.GetCommitsHistory());
					slHistoy.Maximum = View.Snapshots.Count;
					slHistoy.Value = View.Snapshots.Count;
					slHistoy.Minimum = 1;
					tbCode.Text = View.Snapshots[0].File;
					lblCommitDetailsSection.Visibility = Visibility.Visible;
					SetBackgroundRendererMode(RendererMode.TimeLapse);

					// TODO: Implement Search by commits
					// TODO: Highlight code on hover
					lvVerticalHistoryPanel.ItemsSource = View.Snapshots;
				} catch (Exception ex) {
					MessageBox.Show("Oops! Something went wrong.");
				}
			}
		}

		private void btnTimeLapseViewMode_Click(object sender, RoutedEventArgs e) {
			SetBackgroundRendererMode(RendererMode.TimeLapse);
		}

		private void btnIncrementalViewMode_Click(object sender, RoutedEventArgs e) {
			SetBackgroundRendererMode(RendererMode.IncrementalDiff);
		}

		private void btnExit_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void tbCode_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			try {
				if (View.SelectedSnapshotIndex == View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].SequenceStart) {
					View.SelectedSnapshotIndex = -1;
					View.SelectedLine = -1;
					View.SelectedLineLID = -1;
					int index = (int)(slHistoy.Maximum - slHistoy.Value);

					UpdateCommitDetails(View.Snapshots[index]);

					slHistoy.IsSelectionRangeEnabled = false;
				} else {
					View.SelectedSnapshotIndex = View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].SequenceStart;
					View.SelectedLineLID = View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].LID;
					View.SelectedLine = tbCode.TextArea.Caret.Line - 1;

					UpdateCommitDetails(View.Snapshots[View.SelectedSnapshotIndex]);

					slHistoy.IsSelectionRangeEnabled = true;
					slHistoy.SelectionStart = slHistoy.Maximum - View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].SequenceStart;
					slHistoy.SelectionEnd = slHistoy.Maximum - View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].SequenceEnd;
				}

				tbCode.TextArea.TextView.Redraw();
			} catch (Exception ex) {

			}
		}

		/// <summary>
		/// Choose methout for code background highlighting
		/// </summary>
		/// <param name="mode">Rendering mode</param>
		private void SetBackgroundRendererMode(RendererMode mode) {
			tbCode.TextArea.TextView.BackgroundRenderers.Clear();
			switch (mode) {
				case RendererMode.TimeLapse:
					tbCode.TextArea.TextView.BackgroundRenderers.Add(new TimeLapseLineBackgroundRenderer(View));
					break;
				case RendererMode.IncrementalDiff:
					tbCode.TextArea.TextView.BackgroundRenderers.Add(new IncrementalDiffLineBackgroundRenderer(View));
					break;
			}
			
			tbCode.TextArea.TextView.BackgroundRenderers.Add(new SelectedLineBackgroundRenderer(View));
			tbCode.TextArea.TextView.Redraw();

			// Set Menu Check Boxes
			menuTimeLapseViewMode.IsChecked = false;
			menuIncrementalViewMode.IsChecked = false;
			switch (mode) {
				case RendererMode.TimeLapse:
					menuTimeLapseViewMode.IsChecked = true;
					break;
				case RendererMode.IncrementalDiff:
					menuIncrementalViewMode.IsChecked = true;
					break;
			}
		}

		/// <summary>
		/// Show information about commit
		/// </summary>
		/// <param name="commit">Selected commit</param>
		private void UpdateCommitDetails(Snapshot snapshot) {
			var commit                = snapshot.Commit;
			lblCommitSha.Text         = commit.Sha;
			lblCommitAuthor.Text = commit.Author;
			lblCommitDate.Text        = commit.Date.ToString();
			lblCommitMessageText.Text = commit.Description;
			lblFilePath.Text          = snapshot.FilePath;
			// TODO: Multiline description not working
		}

		/// <summary>
		/// Go to the next file diff
		/// </summary>
		private void menuNextDiff_Click(object sender, RoutedEventArgs e) {
			var lines = View.Snapshot.Lines;
			var i = tbCode.TextArea.Caret.Line;
			if (i == lines.Count) return;

			// Skip current diff
			if (lines[i].State != LineState.Unchanged) while (i < lines.Count && lines[i].State != LineState.Unchanged) i++;
			// Find next diff
			while (i < lines.Count && lines[i].State == LineState.Unchanged) i++;

			tbCode.TextArea.Caret.Line = i;
			tbCode.ScrollTo(i, 0);
		}

		/// <summary>
		/// Go to the previous file diff
		/// </summary>
		private void menuPrevDiff_Click(object sender, RoutedEventArgs e) {
			var lines = View.Snapshot.Lines;
			var i = tbCode.TextArea.Caret.Line;
			if (i == 0) return;

			// Skip current diff
			if (lines[i].State != LineState.Unchanged) while (i > 0 && lines[i].State != LineState.Unchanged) i--;
			// Find next diff
			while (i > 0 && lines[i].State == LineState.Unchanged) i--;

			tbCode.TextArea.Caret.Line = i;
			tbCode.ScrollTo(i, 0);
		}
	}
}
