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
		public MainWindow() {
			InitializeComponent();
		}

		ViewData View;

		FileHistoryManager manager;

		protected override void OnInitialized(EventArgs e) {
			base.OnInitialized(e);
			lblCommitMessageLabel.Text = "\nMessage ";
		}

		private void slHistoyValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			int index = (int) (slHistoy.Maximum - slHistoy.Value);
			View.SnapshotIndex = index;
			tbCode.Text = View.Snapshots[index].File;
			UpdateCommitDetails(View.Snapshots[index].Commit);
		}

		private void btnBrowseFile_Click(object sender, RoutedEventArgs e) {
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Multiselect = false;
			openFileDialog.RestoreDirectory = true;
			if (openFileDialog.ShowDialog() == true) {
				try {
					var filename = openFileDialog.FileNames.FirstOrDefault();
					lblFilePath.Content = filename;

					manager = new FileHistoryManager(filename);
					View = new ViewData(manager.GetCommitsHistory());
					slHistoy.Maximum = View.Snapshots.Count;
					slHistoy.Value = View.Snapshots.Count;
					slHistoy.Minimum = 1;
					tbCode.Text = View.Snapshots[0].File;
					lblCommitDetailsSection.Visibility = Visibility.Visible;
					SetBackgroundRendererMode(RendererMode.TimeLapse);
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

		private void tbCode_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			try {
				if (View.SelectedSnapshotIndex == View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].SequenceStart) {
					View.SelectedSnapshotIndex = -1;
					View.SelectedLine = -1;
					View.SelectedLineLID = -1;
					int index = (int)(slHistoy.Maximum - slHistoy.Value);

					UpdateCommitDetails(View.Snapshots[index].Commit);

					slHistoy.IsSelectionRangeEnabled = false;
				} else {
					View.SelectedSnapshotIndex = View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].SequenceStart;
					View.SelectedLineLID = View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].LID;
					View.SelectedLine = tbCode.TextArea.Caret.Line - 1;

					UpdateCommitDetails(View.Snapshots[View.SelectedSnapshotIndex].Commit);

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
		}

		/// <summary>
		/// Show information about commit
		/// </summary>
		/// <param name="commit">Selected commit</param>
		private void UpdateCommitDetails(Commit commit) {
			lblCommitSha.Text     = commit.Sha;
			lblCommitAuthor.Text  = commit.Author;
			lblCommitDate.Text    = commit.Date.ToString();
			lblCommitMessageText.Text = commit.Description;
			// TODO: Multiline description not working
		}
	}
}
