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
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit;

namespace WpfUI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		private const string APP_TITLE = "Git Time Lapse View";
		private const string TAG_PREFIX = "cb_";

		private FileHistoryManager manager;
		private CanvasTreeRenderer treeRenderer;
		private ViewData View;

		private SearchWindow searchWindow;
		private GoToLineWindow goToLineWindow;
		private TextEditor lastFocusedEditor;

		private bool isFirstRendering;
		private bool isApplicationShutdownRequired;
		private Thread scanningThread;

		public MainWindow() {
			InitializeComponent();
		}

		protected override void OnInitialized(EventArgs e) {
			base.OnInitialized(e);
			// TODO: Spaces.......
			lblCommitMessageLabel.Text = "\nMessage ";
			lblFilePathLabel.Text = "\nFile         ";

			CheckCommandLineParameters();

			splTree.MouseEnter += (s, v) => { Mouse.OverrideCursor = Cursors.SizeNS; };
			splTree.MouseLeave += (s, c) => { Mouse.OverrideCursor = Cursors.Arrow; };
			splChart.MouseEnter += (s, v) => { Mouse.OverrideCursor = Cursors.SizeWE; };
			splChart.MouseLeave += (s, c) => { Mouse.OverrideCursor = Cursors.Arrow; };
		}

		private void CheckCommandLineParameters() {
			string[] args = Environment.GetCommandLineArgs();
			if (args.Length <= 1) return;

			for (int i = 1; i < args.Length; i++) {
				var fi = new FileInfo(args[i]);
				if (!fi.Exists) continue;
				OpenFile(fi.FullName);
				return;
			}

			//MessageBox.Show("Invalid arguments.");
		}

		private void slHistoyValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			if (View == null || View.Snapshots == null || View.Snapshots.Count == 0) return;
			View.SelectSnapshot((int)(slHistoy.Maximum - slHistoy.Value));
		}

		private void btnBrowseFile_Click(object sender, RoutedEventArgs e) {
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Multiselect = false;
			openFileDialog.RestoreDirectory = true;
			if (openFileDialog.ShowDialog() == true) {
				var filename = openFileDialog.FileNames.FirstOrDefault();
				OpenFile(filename);
			}
		}

		/// <summary>
		/// Start file processing
		/// </summary>
		/// <param name="filename">File name</param>
		/// <param name="appShutdown">Is mandatory application shutdown call required</param>
		public async void OpenFile(string filename, bool appShutdown = true) {
			try {
				if (Directory.Exists(filename)) {
					MessageBox.Show("Can't analyse directory!");
					return;
				}

				isApplicationShutdownRequired = appShutdown;
				manager = new FileHistoryManager(filename);
				Title = APP_TITLE + ": " + manager.filePath;
				statusTbPausePlay.Source = new BitmapImage(new Uri("pack://application:,,/Resources/Stop_16x.png"));
				View = new ViewData();
				isFirstRendering = true;
				tiCodeCompare.Header = manager.filePath.Split('\\').Last();

				var typeConverter = new HighlightingDefinitionTypeConverter();
				var syntaxHighlighter = (IHighlightingDefinition)typeConverter.ConvertFrom(GetSyntax(filename));
				tbCodeA.SyntaxHighlighting = syntaxHighlighter;
				tbCodeB.SyntaxHighlighting = syntaxHighlighter;

				manager.OnSnapshotsHistoryUpdated = (snapshots) => {
					this.Dispatcher.BeginInvoke(new Action(() => {
						if (!snapshots.Any()) return;// TODO: < 2

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
							statusProgressBar.Value = View.SeekStatus.ItemsProcessed * 100.0 / View.SeekStatus.ItemsTotal;
							statusTbProgressBar.Text = View.SeekStatus.ItemsProcessed + "/" + View.SeekStatus.ItemsTotal;
						}));
					}

					this.Dispatcher.BeginInvoke(new Action(() => {
						statusTbProgressBar.Text = "Done";
					}));
				});
				scanningThread.Start();

				// TODO: Mediator patern????
				// TODO: View should exist without snapshots
				View.OnViewIndexChanged = async (index, csnapshot, psnapshot) => {
					await this.Dispatcher.BeginInvoke(new Action(() => {
						tbCodeA.Text = csnapshot.File;
						tbCodeB.Text = psnapshot?.File;

						slHistoy.Value = slHistoy.Maximum - index;
						UpdateCommitDetails(csnapshot);

						// Initialize parents DropDown
						cbParentBranchesB.Items.Clear();
						foreach (var p in csnapshot.Parents) {
							var item = new ComboBoxItem();
							var text = new TextBlock();

							var snapshot = View.ShaDictionary[p];
							text.Text = "● " + snapshot.DescriptionShort;

							var textEffect = new TextEffect();
							textEffect.PositionStart = 0;
							textEffect.PositionCount = 1;
							textEffect.Foreground = ColorPalette.GetBaseBrush(snapshot.TreeOffset);
							text.TextEffects.Add(textEffect);

							text.TextEffects.Add(textEffect);

							item.Content = text;
							item.Tag = TAG_PREFIX + snapshot.Sha;
							item.IsSelected = psnapshot.Sha == p;

							cbParentBranchesB.Items.Add(item);
						}
					}));

					await CompareCommitsTree();
				};

				View.OnSelectionChanged = () => {
					this.Dispatcher.BeginInvoke(new Action(() => {
						treeRenderer.ClearHighlighting();
						treeRenderer.Draw();

						tbCodeA.Text = View.Snapshot.File;
						tbCodeB.Text = View.SnapshotParent?.File;

						// TODO: BitmapImage cache
						BitmapImage bmpImage = new BitmapImage();
						bmpImage.BeginInit();
						bmpImage.UriSource = new Uri(View.Snapshot.AvatarUrl + "&s=" + 40, UriKind.RelativeOrAbsolute);
						bmpImage.EndInit();
						imgAuthor.Source = bmpImage;

						tbCodeA.TextArea.TextView.Redraw();
						tbCodeB.TextArea.TextView.Redraw();

						cbParentBranchesB.SelectedValue = TAG_PREFIX + View.SnapshotParent?.Sha;
					}));
				};

				// TODO: Implement Search by commits
				// TODO: Highlight code on hover
			} catch (OutOfMemoryException ex) {
				// TODO: "Try to change end date." - add abilty to choose end dates or commits count.
				MessageBox.Show("File history too large.");
			} catch (Exception ex) {
				File.AppendAllText(string.Format("ERROR_{0}_.txt", DateTime.Now.ToString()), ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
				MessageBox.Show("Oops! Something went wrong.");
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

		/// <summary>
		/// Remember last active text editor
		/// </summary>
		private void tbCode_LostFocus(object sender, RoutedEventArgs e) {
			lastFocusedEditor = (TextEditor) sender;
		}

		/// <summary>
		/// Return current active text editor
		/// </summary>
		public TextEditor GetActiveTextEditor() {
			if (tcSources.SelectedContent is TextEditor) return (TextEditor) tcSources.SelectedContent;
			if (tcSources.SelectedIndex == 0 && lastFocusedEditor != null) return lastFocusedEditor;
			if (tcSources.SelectedIndex == 0) return tbCodeA;
			return null;
		}

		private void MainWindow_KeyDown(object sender, KeyEventArgs e) {
			switch (e.Key) {
				case Key.Down: View.MoveToNextSnapshot(); break;
				case Key.Up: View.MoveToPrevSnapshot(); break;
				case Key.Left: View.MoveToLeftSnapshot(); break;
				case Key.Right: View.MoveToRightSnapshot(); break;
			}

			var editor = GetActiveTextEditor();
			if (editor == null) return;

			// Open search window
			if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
				if (searchWindow == null) searchWindow = new SearchWindow(this);
				if (!searchWindow.IsLoaded) searchWindow = new SearchWindow(this);
				if (!searchWindow.IsVisible) searchWindow.Show();
				if (!searchWindow.IsActive) searchWindow.Activate();
			}

			// Open GoToLine window
			if (e.Key == Key.G && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
				if (goToLineWindow == null) goToLineWindow = new GoToLineWindow(this);
				if (!goToLineWindow.IsLoaded) goToLineWindow = new GoToLineWindow(this);
				if (!goToLineWindow.IsVisible) goToLineWindow.Show();
				if (!goToLineWindow.IsActive) goToLineWindow.Activate();
			}

			// Diff navigation
			if (e.Key == Key.F8 && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
				goToPrevDiff();
			} else if (e.Key == Key.F8) {
				goToNextDiff();
			}

			e.Handled = true;
		}

		private void MenuItem_CompareCommitClick(object sender, RoutedEventArgs e) {
			var mi = sender as MenuItem;
			var sha = (string)(mi.Parent as ContextMenu).Tag;
			View.ChangeParentSnapshot(sha, false);
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

			tbCodeA.TextArea.TextView.BackgroundRenderers.Add(new ScrollRenderer(View, canvasScroll));
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
		}

		private void menuNextDiff_Click(object sender, RoutedEventArgs e) {
			goToNextDiff();
		}

		private void tbCodeA_ScrollChanged(object sender, ScrollChangedEventArgs e) {
			syncWindowB();
		}

		/// <summary>
		/// Go to the next file diff
		/// </summary>
		private void goToNextDiff() {
			if (View == null) return;
			var i = tbCodeA.TextArea.Caret.Line;
			if (View.DiffManager.TryGetNextDiff(View.Snapshot, View.SnapshotParent, tbCodeA.TextArea.Caret.Line, ref i)) {
				tbCodeA.ScrollTo(i, 0);
				tbCodeA.TextArea.Caret.Line = i;
				tbCodeA.TextArea.Caret.Column = 1;
				tbCodeA.TextArea.Focus();

				Task.Delay(100).ContinueWith(_ => {
						syncWindowB();
					}
				);
			}
		}

		private void menuPrevDiff_Click(object sender, RoutedEventArgs e) {
			goToPrevDiff();
		}

		/// <summary>
		/// Go to the previous file diff
		/// </summary>
		private void goToPrevDiff() {
			if (View == null) return;
			var i = tbCodeA.TextArea.Caret.Line;
			if (View.DiffManager.TryGetPrevtDiff(View.Snapshot, View.SnapshotParent, tbCodeA.TextArea.Caret.Line, ref i)) {
				tbCodeA.ScrollTo(i, 0);
				tbCodeA.TextArea.Caret.Line = i;
				tbCodeA.TextArea.Caret.Column = 1;
				tbCodeA.TextArea.Focus();

				Task.Delay(100).ContinueWith(_ => {
						syncWindowB();
					}
				);
			}
		}

		/// <summary>
		/// On parents DropDown changed
		/// </summary>
		private void cbParentBranchesB_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (cbParentBranchesB.SelectedItem == null) return;
			View.ChangeParentSnapshot(((ComboBoxItem)cbParentBranchesB.SelectedItem).Tag.ToString().Replace(TAG_PREFIX, ""));
		}

		private void syncWindowB() {
			if (View == null) return;
			if (!tbCodeA.TextArea.TextView.VisualLinesValid) return;
			if (!tbCodeB.TextArea.TextView.VisualLinesValid) return;
			var line = tbCodeA.TextArea.TextView.VisualLines[0].FirstDocumentLine.LineNumber;
			
			this.Dispatcher.BeginInvoke(new Action(() => {
				var parentLine = View.DiffManager.GetParentLineNumber(View.Snapshot, View.SnapshotParent, line);
				var offset = tbCodeA.TextArea.TextView.VisualLines[0].Height * parentLine;
				tbCodeB.ScrollToVerticalOffset(offset);
			}));
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			if (scanningThread != null && scanningThread.IsAlive) {
				if (View.SeekStatus.PauseProcessing) scanningThread.Resume();
				scanningThread.Abort();
			}
			
			if (isApplicationShutdownRequired) Application.Current.Shutdown();
		}

		#region Syntax

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
		/// Change document syntax highlighting
		/// </summary>
		private void MenuItemChangeSyntax_Click(object sender, RoutedEventArgs e) {
			var editor = GetActiveTextEditor();
			if (editor == null) return;
			var typeConverter = new HighlightingDefinitionTypeConverter();
			var syntaxHighlighter = (IHighlightingDefinition)typeConverter.ConvertFrom(((MenuItem)sender).Header);
			editor.SyntaxHighlighting = syntaxHighlighter;
		}

		#endregion

		/// <summary>
		/// Suspend/Resume scanning thread and update UI controls 
		/// </summary>
		private void statusTbPausePlay_MouseUp(object sender, MouseButtonEventArgs e) {
			if (scanningThread == null || View == null) return;
			if (View.SeekStatus.ItemsProcessed == View.SeekStatus.ItemsTotal) return;

			View.SeekStatus.PauseProcessing = !View.SeekStatus.PauseProcessing;
			statusTbPausePlay.Source = View.SeekStatus.PauseProcessing ? 
				new BitmapImage(new Uri("pack://application:,,/Resources/Run_16x.png")) :
				new BitmapImage(new Uri("pack://application:,,/Resources/Stop_16x.png"));
			if (View.SeekStatus.PauseProcessing) {
				statusTbPausePlay.Source = new BitmapImage(new Uri("pack://application:,,/Resources/Run_16x.png"));
				scanningThread.Suspend();
			} else {
				statusTbPausePlay.Source = new BitmapImage(new Uri("pack://application:,,/Resources/Stop_16x.png"));
				scanningThread.Resume();
			}
		}

		#region TreeView

		private async Task CompareCommitsTree() {
			if (View == null) return;
			if (!tiTree.IsSelected) return;

			twTreeDiffs.Items.Clear();
			twTreeDiffs.Items.Add("Processing...");

			await Task.Run(() => {
				// Get changess
				var patch = manager.CompareCommitTree(View.SnapshotParent?.Sha, View.Snapshot?.Sha);
				this.Dispatcher.BeginInvoke(new Action(() => {
					twTreeDiffs.Items.Clear();
					if (patch == null) return;
					twTreeDiffs.Items.Clear();

					// Populate tree
					foreach (var item in patch) {
						var tItems = twTreeDiffs.Items;

						// Split path in to parts
						var parts = item.Path.Split('\\');
						var pathPart = "";
						for (var j = 0; j < parts.Count(); j++) {
							if (!string.IsNullOrEmpty(pathPart)) pathPart += "\\";
							pathPart += parts[j];
							// Check is part already exist
							var isPartAlreadyAdded = false;
							for (int i = 0; i < tItems.Count; i++) {
								if (((FileChanges)((TreeViewItem)tItems[i]).Tag).Path == pathPart) {
									tItems = ((TreeViewItem)tItems[i]).Items;
									isPartAlreadyAdded = true;
									break;
								}
							}

							if (isPartAlreadyAdded) continue;

							var isLastItem = j == parts.Count() - 1;
							var child = GetTreeViewItem(item, pathPart, parts[j], isLastItem);
							tItems.Add(child);
							tItems = child.Items;
						}
					}
				}));

				// TODO: Add deleted item
			});
		}

		private TreeViewItem GetTreeViewItem(LibGit2Sharp.PatchEntryChanges item, string path, string part, bool isLastPart) {
			// Create new TreeViewItem
			var child = new TreeViewItem();

			child.Tag = new FileChanges() {
				Path = path,
				OldPath = item.OldPath,
				IsRenamed = item.Status == LibGit2Sharp.ChangeKind.Renamed || item.Status == LibGit2Sharp.ChangeKind.TypeChanged
			};

			child.IsExpanded = true;
			child.MouseRightButtonDown += FilesTreeItem_MouseRightButtonDown;

			// Create image
			var icon = "Folder_16x.png";
			if (isLastPart) {
				switch (item.Status) {
					case LibGit2Sharp.ChangeKind.Deleted:
						icon = "FileError_16x.png";
						break;
					case LibGit2Sharp.ChangeKind.Modified:
						icon = "EditPage_16x.png";
						break;
					case LibGit2Sharp.ChangeKind.Renamed:
					case LibGit2Sharp.ChangeKind.TypeChanged:
						icon = "Rename_hidden_16x.png";
						break;
					case LibGit2Sharp.ChangeKind.Added:
					case LibGit2Sharp.ChangeKind.Unmodified:
					case LibGit2Sharp.ChangeKind.Copied:
						icon = "AddFile_16x.png";
						break;
					case LibGit2Sharp.ChangeKind.Ignored:
					case LibGit2Sharp.ChangeKind.Untracked:
					case LibGit2Sharp.ChangeKind.Unreadable:
					case LibGit2Sharp.ChangeKind.Conflicted:
						icon = "FileWarning_16x.png";
						break;
					default:
						icon = "FileWarning_16x.png";
						break;
				}
			}

			Image image = new Image();
			image.Source = new BitmapImage(new Uri("pack://application:,,/Resources/" + icon));
			image.Width = 16;
			image.Height = 16;

			// Create label
			Label lblFile = new Label();
			lblFile.Padding = new Thickness(2);
			lblFile.Content = part;

			// Populate StackPanel
			StackPanel stack = new StackPanel();
			stack.Orientation = Orientation.Horizontal;
			stack.Children.Add(image);
			stack.Children.Add(lblFile);

			if (item.Path == path) {
				// +?
				if (item.LinesAdded != 0) {
					Label lblAdded = new Label();
					lblAdded.Padding = new Thickness(2);
					lblAdded.Content = " +" + item.LinesAdded;
					lblAdded.Foreground = ColorPalette.ADDED;
					stack.Children.Add(lblAdded);
				}

				// -?
				if (item.LinesDeleted != 0) {
					Label lblDeleted = new Label();
					lblDeleted.Padding = new Thickness(2);
					lblDeleted.Content = " -" + item.LinesDeleted;
					lblDeleted.Foreground = ColorPalette.DELETED;
					stack.Children.Add(lblDeleted);
				}
			}
			child.Header = stack;

			// Add ToolTip
			if (isLastPart) {
				child.ToolTip += item.Path;
				child.ToolTip += " +" + item.LinesAdded + " -" + item.LinesDeleted;
				child.ToolTip += "\n" + item.Status.ToString();
				if (item.Status == LibGit2Sharp.ChangeKind.Renamed || item.Status == LibGit2Sharp.ChangeKind.TypeChanged || item.Status == LibGit2Sharp.ChangeKind.Copied) {
					child.ToolTip += ": " + item.OldPath;
				}
				child.ToolTip += "\n" + View.Snapshot.Tooltip;

				BindArrowEvents(child);

				// Create new tab with file content
				child.MouseDoubleClick += (s, e) => {
					var file = ((FileChanges)(s as TreeViewItem).Tag).Path;
					var aedit = new ICSharpCode.AvalonEdit.TextEditor();
					aedit.Text = manager.GetFile(View.Snapshot.Sha, file);
					aedit.FontFamily = tbCodeA.FontFamily;
					aedit.FontSize = tbCodeA.FontSize;
					aedit.ShowLineNumbers = true;
					aedit.IsReadOnly = true;
					var typeConverter = new HighlightingDefinitionTypeConverter();
					var syntaxHighlighter = (IHighlightingDefinition)typeConverter.ConvertFrom(GetSyntax(file));
					aedit.SyntaxHighlighting = syntaxHighlighter;

					var lblHeader = new Label();
					lblHeader.Padding = new Thickness(2);
					lblHeader.Content = file.Split('\\').Last();

					// TODO: <Image Source="PathToFile\close.png" Width="20" Height="20" MouseDown="Image_MouseDown"/>
					var lblClose = new Label();
					lblClose.Padding = new Thickness(2);
					lblClose.Content = " X";
					lblClose.MouseLeftButtonUp += (ss, ee) => { tcSources.Items.RemoveAt(tcSources.SelectedIndex); };
					BindArrowEvents(lblClose);

					var sp = new StackPanel();
					sp.Orientation = Orientation.Horizontal;
					sp.Children.Add(lblHeader);
					sp.Children.Add(lblClose);
					sp.ToolTip = child.ToolTip;

					var ti = new TabItem();
					ti.Header = sp;
					ti.IsSelected = true;
					ti.Content = aedit;
					tcSources.Items.Add(ti);
				};
			}

			return child;
		}

		/// <summary>
		/// Open tree item context menu
		/// </summary>
		private void FilesTreeItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
			var cm = twTreeDiffs.FindName("cmTree") as ContextMenu;
			var treeViewItem = sender as TreeViewItem;
			treeViewItem.IsSelected = true;
			var fileChanges = (FileChanges)treeViewItem.Tag;

			cm.PlacementTarget = sender as TextBlock;
			cmiTreeRenamed.IsEnabled = fileChanges.IsRenamed;
			cmiTreeRenamed.Tag = fileChanges.OldPath;
			cmiTreeCurrent.Tag = fileChanges.Path;

			cm.IsOpen = true;
			e.Handled = true;
		}

		private void TreeMenuItem_Click(object sender, RoutedEventArgs e) {
			var mi = sender as MenuItem;
			var path = (string)mi.Tag;
			var window = new MainWindow();
			window.OpenFile(manager.repositoryPath + "\\" + path, false);
			window.Show();
		}

		private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			await CompareCommitsTree();
		}

		private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) {
			twTreeDiffs.VerticalAlignment = VerticalAlignment.Stretch;
			twTreeDiffs.Height = Double.NaN;
		}

		private void BindArrowEvents(Control control) {
			control.MouseEnter += (s, e) => { Mouse.OverrideCursor = Cursors.Hand; };
			control.MouseLeave += (s, e) => { Mouse.OverrideCursor = Cursors.Arrow; };
		}

		#endregion
	}
}
