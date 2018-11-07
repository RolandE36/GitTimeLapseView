using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimeLapseView;
using Microsoft.Win32;
using WpfUI.Renderer;
using System.Threading;
using System.IO;
using TimeLapseView.Model;

namespace WpfUI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private const string APP_TITLE = "Git Time Lapse View";

		private FileHistoryManager manager;
		private CanvasTreeRenderer treeRenderer;
		private ViewData View;

		private bool isFirstRendering;
		private Thread scanningThread;

		public MainWindow() {
			InitializeComponent();
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
					statusTbPausePlay.Text = "⏸";
					View = new ViewData();
					isFirstRendering = true;

					var typeConverter = new HighlightingDefinitionTypeConverter();
					var syntaxHighlighter = (IHighlightingDefinition)typeConverter.ConvertFrom(GetSyntax(filename));
					tbCodeA.SyntaxHighlighting = tbCodeB.SyntaxHighlighting = syntaxHighlighter;

					manager.OnSnapshotsHistoryUpdated = (snapshots) => {
						this.Dispatcher.BeginInvoke(new Action(() => {
							if (!snapshots.Any()) return;

							var addedSnaphots = View.Snapshots == null ? 0 : snapshots.Count - View.Snapshots.Count;
							View.Snapshots = snapshots;
							slHistoy.Maximum = View.Snapshots.Count;
							if (isFirstRendering) {
								isFirstRendering = false;
								slHistoy.Value = View.Snapshots.Count;
								tbCodeA.Text = View.Snapshot.File;

								SetupBackgroundRenderers();
								lblCommitDetailsSection.Visibility = Visibility.Visible;
							} else {
								slHistoy.Value += addedSnaphots;
							}

							Canvas1.Children.Clear();
							var crt = new CanvasTreeRenderer(View, Canvas1);

							crt.BuildTree();
							crt.Draw();

							treeRenderer = crt;
						}));
					};


					if (scanningThread != null && scanningThread.IsAlive) scanningThread.Abort();
					scanningThread = new Thread(() => {
						while (!View.SeekStatus.IsSeekCompleted) {
							manager.GetCommitsHistory(View.SeekStatus);
							View.SeekStatus.ItemsPerPage *= 2;
							this.Dispatcher.BeginInvoke(new Action(() => {
								statusProgressBar.Value = (int)View.SeekStatus.ItemsProcessed * 100.0 / View.SeekStatus.ItemsTotal;
								statusTbProgressBar.Text = View.SeekStatus.ItemsProcessed + "/" + View.SeekStatus.ItemsTotal;
							}));
						}

						this.Dispatcher.BeginInvoke(new Action(() => {
							statusTbPausePlay.Text = "";
							statusTbProgressBar.Text = "Done";
						}));
					});
					scanningThread.Start();

					// TODO: Mediator patern????
					// TODO: View should exist without snapshots
					View.OnViewIndexChanged = (index, csnapshot, psnapshot) => {
						this.Dispatcher.BeginInvoke(new Action(() => {
							tbCodeA.Text = csnapshot.File;
							tbCodeB.Text = psnapshot?.File;

							slHistoy.Value = slHistoy.Maximum - index;
							lvVerticalHistoryPanel.SelectedIndex = index;
							UpdateCommitDetails(csnapshot);
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
					File.AppendAllText(string.Format("ERROR_{0}_.txt", DateTime.Now.ToString()), ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
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

		private void menuShowBlame_Click(object sender, RoutedEventArgs e) {
			canvasBlame.Visibility = menuBlameHighlight.IsChecked ? Visibility.Visible : Visibility.Hidden;
			colBlame.Width = new GridLength(menuBlameHighlight.IsChecked ? 150 : 0);
		}

		private void menuShowCompare_Click(object sender, RoutedEventArgs e) {
			colCompare.Width = new GridLength(0);

			if (menuShowCompare.IsChecked) {
				var width = (gridSources.ActualWidth - 150) / 2;
				colCompare.Width = new GridLength(width, GridUnitType.Star);
				colSource.Width = new GridLength(width, GridUnitType.Star);
			} else {
				colCompare.Width = new GridLength(0);
			}
		}

		private void btnExit_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		// <summary>
		/// Choose background highlighting
		/// </summary>
		private void SetupBackgroundRenderers() {
			if (View == null) return;
			tbCodeA.TextArea.TextView.BackgroundRenderers.Clear();
			tbCodeB.TextArea.TextView.BackgroundRenderers.Clear();

			if (menuBirthHighlight.IsChecked) {
				tbCodeA.TextArea.TextView.BackgroundRenderers.Add(new TimeLapseLineRenderer(View, false));
				tbCodeB.TextArea.TextView.BackgroundRenderers.Add(new TimeLapseLineRenderer(View, true));
			}

			if (menuChangesHighlight.IsChecked) {
				tbCodeA.TextArea.TextView.BackgroundRenderers.Add(new ChangesRenderer(View));
				tbCodeB.TextArea.TextView.BackgroundRenderers.Add(new DeletionsRenderer(View));
			}

			if (menuBlameHighlight.IsChecked) {
				tbCodeA.TextArea.TextView.BackgroundRenderers.Add(new BlameRenderer(View, canvasBlame));
			}
		}

		/// <summary>
		/// Background highlighting changed event
		/// </summary>
		private void btnViewModeChanged_Click(object sender, RoutedEventArgs e) {
			SetupBackgroundRenderers();
		}

		/// <summary>
		/// Show information about commit
		/// </summary>
		/// <param name="commit">Selected commit</param>
		private void UpdateCommitDetails(SnapshotVM snapshot) {
			lblCommitSha.Text         = snapshot.Sha;
			lblCommitAuthor.Text      = snapshot.Author;
			lblCommitDate.Text        = snapshot.Date.ToString();
			lblCommitMessageText.Text = snapshot.Description;
			lblFilePath.Text          = snapshot.FilePath;
			// TODO: Multiline description not working
		}

		/// <summary>
		/// Go to the next file diff
		/// </summary>
		private void menuNextDiff_Click(object sender, RoutedEventArgs e) {
			if (View == null) return;
			var lines = View.Snapshot.FileDetails;
			var i = tbCodeA.TextArea.Caret.Line;
			if (i >= lines.Count) i = lines.Count - 1;

			while (i < lines.Count && lines[i].State != LineState.Unchanged) i++; // Skip current diff
			while (i < lines.Count && lines[i].State == LineState.Unchanged) i++; // Find next diff
			i++;

			tbCodeA.ScrollTo(i, 0);
			tbCodeA.TextArea.Caret.Line = i;
			tbCodeA.TextArea.Caret.Column = 1;
			tbCodeA.TextArea.Focus();
		}

		/// <summary>
		/// Go to the previous file diff
		/// </summary>
		private void menuPrevDiff_Click(object sender, RoutedEventArgs e) {
			if (View == null) return;
			var lines = View.Snapshot.FileDetails;
			var i = tbCodeA.TextArea.Caret.Line - 1;
			if (i <= 0) i = 0;

			while (i > 0 && lines[i].State != LineState.Unchanged) i--; // Skip current diff
			while (i > 0 && lines[i].State == LineState.Unchanged) i--; // Find next diff
			i++;

			tbCodeA.ScrollTo(i, 0);
			tbCodeA.TextArea.Caret.Line = i;
			tbCodeA.TextArea.Caret.Column = 1;
			tbCodeA.TextArea.Focus();
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			if (scanningThread != null && scanningThread.IsAlive) {
				if (View.SeekStatus.PauseProcessing) scanningThread.Resume();
				scanningThread.Abort();
			}
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

		/// <summary>
		/// Suspend/Resume scanning thread and update UI controls 
		/// </summary>
		private void statusTbPausePlay_MouseUp(object sender, MouseButtonEventArgs e) {
			if (scanningThread == null || View == null) return;
			if (View.SeekStatus.ItemsProcessed == View.SeekStatus.ItemsTotal) return;

			View.SeekStatus.PauseProcessing = !View.SeekStatus.PauseProcessing;
			statusTbPausePlay.Text = View.SeekStatus.PauseProcessing ? "⏵" : "⏸";
			if (View.SeekStatus.PauseProcessing) {
				statusTbPausePlay.Text = "⏵";
				scanningThread.Suspend();
			} else {
				statusTbPausePlay.Text = "⏸";
				scanningThread.Resume();
			}
		}
	}
}
