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
		}

		private void slHistoyValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			int index = (int) (slHistoy.Maximum - slHistoy.Value);

			View.SnapshotIndex = index;
			var r1 = new TimeLapseLineBackgroundRenderer(View, (int)slHistoy.Value+1, View.Snapshots.Count);
			var r2 = new SelectedLineBackgroundRenderer(View);
			
			tbCode.TextArea.TextView.BackgroundRenderers.Clear();// TODO: Move to form init
			tbCode.TextArea.TextView.BackgroundRenderers.Add(r1);
			tbCode.TextArea.TextView.BackgroundRenderers.Add(r2);
			tbCode.Text = View.Snapshots[index].File;
			lblDetails.Content = View.Snapshots[index].Commit.Description;
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
				} catch (Exception ex) {
					MessageBox.Show("Oops! Something went wrong.");
				}
			}
		}

		private void tbCode_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			try {
				if (View.SelectedSnapshotIndex == View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].SequenceStart) {
					View.SelectedSnapshotIndex = -1;
					View.SelectedLine = -1;
					View.SelectedLineLID = -1;
					int index = (int)(slHistoy.Maximum - slHistoy.Value);
					lblDetails.Content = View.Snapshots[index].Commit.Description;

					slHistoy.IsSelectionRangeEnabled = false;
				} else {
					View.SelectedSnapshotIndex = View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].SequenceStart;
					View.SelectedLineLID = View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].LID;
					View.SelectedLine = tbCode.TextArea.Caret.Line - 1;
					lblDetails.Content = View.Snapshots[View.SelectedSnapshotIndex].Commit.Description;

					slHistoy.IsSelectionRangeEnabled = true;
					slHistoy.SelectionStart = slHistoy.Maximum - View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].SequenceStart;
					slHistoy.SelectionEnd = slHistoy.Maximum - View.Snapshot.Lines[tbCode.TextArea.Caret.Line - 1].SequenceEnd;
				}

				tbCode.TextArea.TextView.Redraw();
			} catch (Exception ex) {

			}
		}
	}
}
