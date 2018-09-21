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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TimeLapseView;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Win32;
using WpfUI.Renderer;
using System.Threading;
using System.IO;

namespace WpfUI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private const string APP_TITLE = "Git Time Lapse View";

		private FileHistoryManager manager;
		private CanvasTreeRenderer treeRenderer;
		private ViewData View;

		private int page = 0; // TODO: Move to viewdata
		private bool isFirstRendering;
		private Thread scanningThread;
		private bool isScanningDone = false; // TODO: Move to viewdata

		public MainWindow() {
			InitializeComponent();
		}

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
			if (View == null || View.Snapshots == null || View.Snapshots.Count == 0) return;
			View.SelectSnapshot((int)(slHistoy.Maximum - slHistoy.Value));
		}

		private void lvVerticalHistoryPanel_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			View.SelectSnapshot(lvVerticalHistoryPanel.SelectedIndex);
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
					View = new ViewData();
					isFirstRendering = true;
					page = 0;
					isScanningDone = false;

					var typeConverter = new HighlightingDefinitionTypeConverter();
					var syntaxHighlighter = (IHighlightingDefinition)typeConverter.ConvertFrom(GetSyntax(filename));
					tbCode.SyntaxHighlighting = syntaxHighlighter;

					manager.OnSnapshotsHistoryUpdated = (snapshots) => {
						this.Dispatcher.BeginInvoke(new Action(() => {
							if (snapshots.Count() == 0) return;

							//View = new ViewData();
							var addedSnaphots = View.Snapshots == null ? 0 : snapshots.Count - View.Snapshots.Count;
							View.Snapshots = snapshots;
							slHistoy.Maximum = View.Snapshots.Count;
							if (isFirstRendering) {
								isFirstRendering = false;
								slHistoy.Value = View.Snapshots.Count;
								tbCode.Text = View.Snapshot.File;

								SetBackgroundRendererMode(RendererMode.TimeLapse);
								lblCommitDetailsSection.Visibility = Visibility.Visible;
							} else {
								slHistoy.Value += addedSnaphots;
							}

							Canvas1.Children.Clear();
							var crt = new CanvasTreeRenderer(View, snapshots, Canvas1);

							crt.BuildTree();
							crt.Draw();

							treeRenderer = crt;
						}));
					};


					if (scanningThread != null && scanningThread.IsAlive) scanningThread.Abort();
					scanningThread = new Thread(() => {
						while (!isScanningDone) {
							manager.GetCommitsHistory(page, ref isScanningDone);
							page++;
						}
						MessageBox.Show("Done. Page = " + page);
					});
					scanningThread.Start();



					// TODO: Mediator patern????
					// TODO: View should exist without snapshots
					View.OnViewIndexChanged = (index, snapshot) => {
						this.Dispatcher.BeginInvoke(new Action(() => {
							tbCode.Text = snapshot.File;
							slHistoy.Value = slHistoy.Maximum - index;
							lvVerticalHistoryPanel.SelectedIndex = index;
							UpdateCommitDetails(snapshot);
						}));
					};

					View.OnSelectionChanged = () => {
						this.Dispatcher.BeginInvoke(new Action(() => {
							treeRenderer.ClearHighlighting();
							treeRenderer.Draw();
						}));
					};

					// TODO: Implement Search by commits
					// TODO: Highlight code on hover
					lvVerticalHistoryPanel.ItemsSource = View.Snapshots;
				} catch (OutOfMemoryException ex) {
					// TODO: "Try to change end date." - add abilty to choose end dates or commits count.
					MessageBox.Show("File history too large.");
				} catch (Exception ex) {
					MessageBox.Show("Oops! Something went wrong.");
				}
			}
		}

		/// <summary>
		/// Prevent scrolling after keyboard events
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void CanvasScrollViewer_PreviewKeyDown(object sender, KeyEventArgs e) {
			e.Handled = true;
		}

		private void Canvas_MouseDown(object sender, MouseButtonEventArgs e) {
			Keyboard.ClearFocus();
			Canvas1.Focus();
		}

		private void MainWindow_KeyUp(object sender, KeyEventArgs e) {
			switch (e.Key) {
				case Key.Down: View.MoveToNextSnapshot(); break;
				case Key.Up: View.MoveToPrevSnapshot(); break;
				case Key.Left: View.MoveToLeftSnapshot(); break;
				case Key.Right: View.MoveToRightSnapshot(); break;
			}

			e.Handled = true;
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
				if (View.SelectedSnapshotIndex == View.Snapshot.GetLineBirth(tbCode.TextArea.Caret.Line - 1)) {

					View.ResetSnapshotsSelection();

					View.SelectedSnapshotIndex = -1;
					View.SelectedLine = -1;
					View.SelectedLineLID = -1;
					int index = (int)(slHistoy.Maximum - slHistoy.Value);

					UpdateCommitDetails(View.Snapshot);

					slHistoy.IsSelectionRangeEnabled = false;
				} else {
					View.SelectedSnapshotIndex = View.Snapshot.GetLineBirth(tbCode.TextArea.Caret.Line - 1);
					View.SelectedLineLID = View.Snapshot.FileDetails[tbCode.TextArea.Caret.Line - 1].LID;
					View.SelectedLine = tbCode.TextArea.Caret.Line - 1;

					View.SelectSnapshots(CodeFile.LineBase[View.Snapshot.FileDetails.LineHistory[View.SelectedLine]]);
					UpdateCommitDetails(View.Snapshots[View.SelectedSnapshotIndex]);

					slHistoy.IsSelectionRangeEnabled = true;
					slHistoy.SelectionStart = slHistoy.Maximum - View.Snapshot.GetLineBirth(tbCode.TextArea.Caret.Line - 1);
					slHistoy.SelectionEnd = slHistoy.Maximum - View.Snapshot.GetLineDeath(tbCode.TextArea.Caret.Line - 1);
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
			lblCommitAuthor.Text      = commit.Author;
			lblCommitDate.Text        = commit.Date.ToString();
			lblCommitMessageText.Text = commit.Description;
			lblFilePath.Text          = snapshot.FilePath;
			// TODO: Multiline description not working
		}

		/// <summary>
		/// Go to the next file diff
		/// </summary>
		private void menuNextDiff_Click(object sender, RoutedEventArgs e) {
			var lines = View.Snapshot.FileDetails;
			var i = tbCode.TextArea.Caret.Line;
			if (i >= lines.Count) i = lines.Count - 1;

			while (i < lines.Count && lines[i].State != LineState.Unchanged) i++; // Skip current diff
			while (i < lines.Count && lines[i].State == LineState.Unchanged) i++; // Find next diff
			i++;

			tbCode.ScrollTo(i, 0);
			tbCode.TextArea.Caret.Line = i;
			tbCode.TextArea.Caret.Column = 1;
			tbCode.TextArea.Focus();
		}

		/// <summary>
		/// Go to the previous file diff
		/// </summary>
		private void menuPrevDiff_Click(object sender, RoutedEventArgs e) {
			var lines = View.Snapshot.FileDetails;
			var i = tbCode.TextArea.Caret.Line - 1;
			if (i <= 0) i = 0;

			while (i > 0 && lines[i].State != LineState.Unchanged) i--; // Skip current diff
			while (i > 0 && lines[i].State == LineState.Unchanged) i--; // Find next diff
			i++;

			tbCode.ScrollTo(i, 0);
			tbCode.TextArea.Caret.Line = i;
			tbCode.TextArea.Caret.Column = 1;
			tbCode.TextArea.Focus();
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			if (scanningThread != null && scanningThread.IsAlive) scanningThread.Abort();
		}

		private const string XML = "XML";
		private const string VB = "VB";
		private const string TEX = "TeX";
		private const string TSQL = "TSQL";
		private const string PYTHON = "Python";
		private const string POWERSHELL = "PowerShell";
		private const string PATCH = "Patch";
		private const string PHP = "PHP";
		private const string MARKDOWN = "MarkDown";
		private const string JAVASCRIPT = "JavaScript";
		private const string JAVA = "Java";
		private const string HTML = "HTML";
		private const string COCO = "Coco";
		private const string CSH = "C#";
		private const string CSS = "CSS";
		private const string CPP = "C++";
		private const string BOO = "Boo";
		private const string ASPXHTML = "ASP/XHTML";

		private string GetSyntax(string filename) {
			var file = new FileInfo(filename);
			switch (file.Extension.Replace(".", "")) {
				case "xml": case "xsl": case "xslt": case "xsd": case "manifest": case "config": case "addin": case "xshd": case "wxs": case "wxi": case "wxl": case "proj": case "csproj": case "vbproj": case "ilproj": case "booproj": case "build": case "xfrm": case "targets": case "xaml": case "xpt": case "xft": case "map": case "wsdl": case "disco": case "ps1xml": case "nuspec": return XML;
				case "vb": return VB;
				case "tex": return TEX;
				case "sql": return TSQL;
				case "py": case "pyw": return PYTHON;
				case "ps1": case "psm1": case "psd1": return POWERSHELL;
				case "patch": case "diff": return PATCH;
				case "php": return PHP;
				case "md": return MARKDOWN;
				case "js": return JAVASCRIPT;
				case "java": return JAVA;
				case "htm": case "html": return HTML;
				case "atg": return COCO;
				case "cs": return CSH;
				case "css": return CSS;
				case "c": case "h": case "cc": case "cpp": case "hpp": return CPP;
				case "boo": return BOO;
				case "asp": case "aspx": case "asax": case "asmx": case "ascx": case "master": return ASPXHTML;
			}

			return "";
		}
	}
}
