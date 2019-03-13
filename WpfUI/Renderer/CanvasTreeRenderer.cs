using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TimeLapseView;
using TimeLapseView.Model;

namespace WpfUI.Renderer {
	public class SnapshotCanvasModel {
		public Ellipse Ellipse;
		public List<Shape> Paths = new List<Shape>();
		public Rectangle CommitBackground;
	}

	public class CanvasTreeRenderer {
		public ViewData ViewData;
		public Canvas Canvas;
		private Dictionary<string, SnapshotCanvasModel> UiElements;
		private Dictionary<string, Shape> UiChildParentPaths; // TODO: Investigate more advanced solution

		/// <summary>
		/// List of already rendered snapshots
		/// </summary>
		private List<SnapshotVM> RenderedSnapshots { get; set; }

		private const int SCALE_Y = 12;
		private const int SCALE_X = 12;
		private const int CIRCLE = 20;
		private const int SELECTED_LINE_WIDTH = 2;
		private const int NOT_SELECTED_LINE_WIDTH = 1;

		private readonly SolidColorBrush BlackBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10));
		private readonly SolidColorBrush BlueBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x82, 0xc8));
		private readonly SolidColorBrush GreenBrush = new SolidColorBrush(Color.FromRgb(0x3c, 0xb4, 0x4b));

		private readonly SolidColorBrush TransparentCircleBrush = new SolidColorBrush(Color.FromArgb(0x00, 0xff, 0xff, 0xff));
		private readonly SolidColorBrush TransparentGreenBrush1 = new SolidColorBrush(Color.FromArgb(0x3c, 0x3c, 0xb4, 0x4b));
		private readonly SolidColorBrush TransparentGreenBrush2 = new SolidColorBrush(Color.FromArgb(0x2c, 0x3c, 0xb4, 0x4b));

		private readonly SolidColorBrush SolidCircleBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));

		public CanvasTreeRenderer(ViewData viewData, Canvas canvas) {
			ViewData = viewData;
			Canvas = canvas;
			//CanvasScrollViewer = canvasScrollViewer;
			UiElements = new Dictionary<string, SnapshotCanvasModel>();
			UiChildParentPaths = new Dictionary<string, Shape>();
			RenderedSnapshots = ViewData.Snapshots.ToList();

			foreach (var snapshot in ViewData.Snapshots) {
				UiElements[snapshot.Sha] = new SnapshotCanvasModel();
			}
		}

		public void BuildTree() {
			var rnd = new Random();
			var reservedArea = GetLabelsToothOffset();

			foreach (var snapshot in ViewData.Snapshots) {
				var diameter = CIRCLE;
				if (!snapshot.IsImportantCommit) diameter -= CIRCLE / 4;
				var radius = diameter / 2;
				var x = (SCALE_X - radius) + 2 * SCALE_X * snapshot.TreeOffset;
				var y = (SCALE_Y - radius) + 2 * SCALE_Y * snapshot.Index;

				var rectangle = new Rectangle();
				rectangle.Fill = TransparentCircleBrush;
				rectangle.Width = 999999;
				rectangle.Height = SCALE_Y * 2;
				rectangle.Tag = snapshot.Sha;
				LinkCommitHoverEvents(rectangle);
				rectangle.MouseLeftButtonDown += FrameworkElement_MouseLeftButtonDown;
				Canvas.SetLeft(rectangle, x * 0);
				Canvas.SetTop(rectangle, 2 * SCALE_Y * snapshot.Index);

				UiElements[snapshot.Sha].CommitBackground = rectangle;
				Canvas.Children.Add(rectangle);
				Canvas.SetZIndex(rectangle, -2);
				
				foreach (var p in snapshot.Parents.ToList()) { // TODO: Investigate .ToList();
					var parent = ViewData.ShaDictionary[p];
					if (!parent.IsCommitVisible) continue;

					var x1 = SCALE_X + 2 * SCALE_X * snapshot.TreeOffset;
					var y1 = SCALE_Y + 2 * SCALE_Y * snapshot.Index;
					
					var x2 = SCALE_X + 2 * SCALE_X * parent.TreeOffset;
					var y2 = SCALE_Y + 2 * SCALE_Y * parent.Index;

					var x3 = x2;
					var y3 = y1;
					var x4 = x1;
					var y4 = y2;

					if (IsCommitsInOneLine(snapshot, parent)) {
						var line = new Line();
						line.Stroke = ColorPalette.GetBaseBrush(snapshot.TreeOffset);
						line.X1 = x1;
						line.Y1 = y1;
						line.X2 = x2;
						line.Y2 = y2;
						line.StrokeThickness = 1;

						Canvas.Children.Add(line);
						UiElements[parent.Sha].Paths.Add(line);
						UiElements[snapshot.Sha].Paths.Add(line);
						UiChildParentPaths[snapshot.Sha + "|" + parent.Sha] = line;
					} else {
						PathFigure figure = new PathFigure();

						// Normalization to (x1, y1) as zero point
						x2 -= x1; //       1
						x3 -= x1; //       |
						x4 -= x1; // 2-----4

						y2 -= y1; // 1-----3
						y3 -= y1; //       |
						y4 -= y1; //       2

						// TODO: Calculate x3 y3 in other place near center???

						if (IsSimpleDownLine(snapshot, parent)) {
							BezierSegment bezier = new BezierSegment() {
								// Point0                   //         3
								Point1 = new Point(x4, y4), //         |
								Point2 = new Point(x4, y4), //         |
								Point3 = new Point(x2, y2), // 0 _____1,2
								IsStroked = true
							};

							figure.Segments.Add(bezier);
						} else if(IsSimpleUpLine(snapshot, parent)) {
							BezierSegment bezier = new BezierSegment() {
								// Point0                   // 0______1,2
								Point1 = new Point(x3, y3), //         |
								Point2 = new Point(x3, y3), //         |
								Point3 = new Point(x2, y2), //         3
								IsStroked = true
							};

							figure.Segments.Add(bezier);
						} else {
							var n = SCALE_X / 2 + rnd.Next(SCALE_X / 2);
							var left = x1 < x2 ? n : -n;
							
							x3 -= left;
							x2 -= left;

							// Main line
							BezierSegment bezier = new BezierSegment() {
								Point1 = new Point(x3, y3),
								Point2 = new Point(x3, y3),
								Point3 = new Point(x2, y2 - SCALE_Y / 2),
								IsStroked = true
							};

							// Helper line
							BezierSegment s1 = new BezierSegment() {
								Point1 = new Point(x2, y2),
								Point2 = new Point(x2, y2),
								Point3 = new Point(x2 + left, y2),
								IsStroked = true
							};

							figure.Segments.Add(bezier);
							figure.Segments.Add(s1);
						}

						Path path = new Path();
						path.Stroke = ColorPalette.GetBaseBrush(parent.TreeOffset);
						path.Data = new PathGeometry(new PathFigure[] { figure });
						path.Opacity = 1;

						Canvas.Children.Add(path);
						UiElements[parent.Sha].Paths.Add(path);
						UiElements[snapshot.Sha].Paths.Add(path);
						UiChildParentPaths[snapshot.Sha + "|" + parent.Sha] = path;

						Canvas.SetLeft(path, x1);
						Canvas.SetTop(path,  y1);
					}
				}

				BitmapImage bmpImage = new BitmapImage();
				bmpImage.BeginInit();
				bmpImage.UriSource = new Uri(snapshot.AvatarUrl + "&s=" + diameter, UriKind.RelativeOrAbsolute);
				bmpImage.EndInit();

				// User icon
				Image clippedImage = new Image();
				clippedImage.Source = bmpImage;
				EllipseGeometry clipGeometry = new EllipseGeometry(new Point(radius, radius), radius, radius);
				clippedImage.Clip = clipGeometry;

				Canvas.Children.Add(clippedImage);
				Canvas.SetLeft(clippedImage, x);
				Canvas.SetTop(clippedImage, y);

				// User Image border
				var ellipse = new Ellipse();
				ellipse.Fill = TransparentCircleBrush;
				ellipse.Height = diameter;
				ellipse.Width = diameter;
				ellipse.StrokeThickness = NOT_SELECTED_LINE_WIDTH;
				ellipse.Stroke = GreenBrush;
				ellipse.ToolTip = snapshot.Tooltip;
				ellipse.MouseEnter += Ellipse_OnMouseEnter;
				ellipse.MouseLeave += Ellipse_OnMouseLeave;
				ellipse.MouseLeftButtonDown += Ellipse_MouseLeftButtonDown;
				ellipse.Tag = snapshot.Sha;
				LinkArrowHandEvents(ellipse);
				LinkCommitHoverEvents(ellipse);

				UiElements[snapshot.Sha].Ellipse = ellipse;
				Canvas.Children.Add(ellipse);
				Canvas.SetLeft(ellipse, x);
				Canvas.SetTop(ellipse, y);

				// Circle before label
				var branchColorCircle = new Ellipse();
				branchColorCircle.Fill = ColorPalette.GetBaseBrush(snapshot.TreeOffset);
				branchColorCircle.Height = 8;
				branchColorCircle.Width = 8;
				branchColorCircle.Stroke = ColorPalette.GetBaseBrush(snapshot.TreeOffset);
				branchColorCircle.ToolTip = snapshot.Tooltip;
				LinkCommitHoverEvents(branchColorCircle);
				LinkArrowHandEvents(branchColorCircle);
				Canvas.SetLeft(branchColorCircle, 2 * SCALE_X * (reservedArea[snapshot.Index] + 1) + SCALE_X / 4);
				Canvas.SetTop(branchColorCircle, y+5);
				Canvas.Children.Add(branchColorCircle);

				// Commit description Label
				TextBlock textBlock = new TextBlock();
				textBlock.Text = /*snapshot.DateString + " " + */snapshot.DescriptionShort;
				textBlock.Foreground = BlackBrush;
				textBlock.Tag = snapshot.Sha;
				textBlock.ToolTip = snapshot.Tooltip;
				textBlock.MouseLeftButtonDown += FrameworkElement_MouseLeftButtonDown;
				textBlock.MouseRightButtonDown += TextBlock_MouseRightButtonDown;
				LinkCommitHoverEvents(textBlock);
				LinkArrowHandEvents(textBlock);
				Canvas.SetLeft(textBlock, 2 * SCALE_X * (reservedArea[snapshot.Index] + 1) + SCALE_X);
				Canvas.SetTop(textBlock, y);
				Canvas.Children.Add(textBlock);
			}

			Canvas.Width = RenderedSnapshots.Max(e => e.TreeOffset)*SCALE_X+SCALE_X;
			Canvas.Height = RenderedSnapshots.Count* SCALE_Y * 2 + SCALE_Y;
		}

		#region Events

		/// <summary>
		/// Link events for changing cursor (pointer/arrow)
		/// </summary>
		private void LinkArrowHandEvents(FrameworkElement element) {
			element.MouseEnter += MouseEnterHand;
			element.MouseLeave += MouseLeaveArrow;
		}

		private void MouseLeaveArrow(object sender, MouseEventArgs e) {
			Mouse.OverrideCursor = null;
		}

		private void MouseEnterHand(object sender, MouseEventArgs e) {
			Mouse.OverrideCursor = Cursors.Hand;
		}

		private void FrameworkElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			// TODO: Copy / paste
			var sha = (string)(sender as FrameworkElement).Tag;
			if (string.IsNullOrEmpty(sha)) return;
			foreach (var path in UiElements[sha].Paths) {
				path.StrokeThickness = SELECTED_LINE_WIDTH;
			}

			ViewData.SelectSnapshot(ViewData.ShaDictionary[sha].Index);
		}

		/// <summary>
		/// Open tree item context menu
		/// </summary>
		private void TextBlock_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
			var cm = Canvas.FindName("cmTreeItem") as ContextMenu;
			cm.Tag = (string)(sender as TextBlock).Tag;
			cm.PlacementTarget = sender as TextBlock;
			cm.IsOpen = true;
		}

		/// <summary>
		/// Link commit hover events
		/// </summary>
		private void LinkCommitHoverEvents(FrameworkElement element) {
			element.MouseEnter += CommitEnter;
			element.MouseLeave += CommitLeave;
		}

		private void CommitLeave(object sender, MouseEventArgs e) {
			var sha = (string)(sender as FrameworkElement).Tag;
			if (string.IsNullOrEmpty(sha)) return;
			UiElements[sha].CommitBackground.Fill = TransparentCircleBrush;
			foreach (var path in UiElements[sha].Paths) {
				path.StrokeThickness = NOT_SELECTED_LINE_WIDTH;
			}
			Draw();
		}

		private void CommitEnter(object sender, MouseEventArgs e) {
			var sha = (string)(sender as FrameworkElement).Tag;
			if (string.IsNullOrEmpty(sha)) return;
			foreach (var path in UiElements[sha].Paths) {
				path.StrokeThickness = SELECTED_LINE_WIDTH;
			}
			Draw();
			UiElements[sha].CommitBackground.Fill = TransparentGreenBrush2;
		}

		/// <summary>
		/// Highlight paths for hovered snapshot
		/// </summary>
		private void Ellipse_OnMouseEnter(object sender, MouseEventArgs e) {
			var sha = (string)(sender as Ellipse).Tag;
			UiElements[sha].Ellipse.StrokeThickness = 2;
			foreach (var path in UiElements[sha].Paths) {
				path.StrokeThickness = SELECTED_LINE_WIDTH;
			}
			Draw();
		}

		/// <summary>
		/// Revert paths highlighting for hovered snapshot
		/// </summary>
		private void Ellipse_OnMouseLeave(object sender, MouseEventArgs e) {
			var sha = (string)(sender as Ellipse).Tag;
			UiElements[sha].Ellipse.StrokeThickness = NOT_SELECTED_LINE_WIDTH;
			foreach (var path in UiElements[sha].Paths) {
				path.StrokeThickness = NOT_SELECTED_LINE_WIDTH;
			}
			Draw();
		}

		/// <summary>
		/// Select snapshot
		/// </summary>
		private void Ellipse_MouseLeftButtonDown(object sender, MouseEventArgs e) {
			var sha = (string)(sender as Ellipse).Tag;
			UiElements[sha].Ellipse.StrokeThickness = 2;
			foreach (var path in UiElements[sha].Paths) {
				path.StrokeThickness = SELECTED_LINE_WIDTH;
			}

			ViewData.SelectSnapshot(ViewData.ShaDictionary[sha].Index);
		}

		#endregion

		#region Tree preparation

		/// <summary>
		/// Find position for each commit label
		/// </summary>
		public int[] GetLabelsToothOffset() {
			var reservedArea = new int[ViewData.Snapshots.Count];
			for (int i = 0; i < ViewData.Snapshots.Count; i++) {
				var snapshot = ViewData.Snapshots[i];
				if (reservedArea[i] < snapshot.TreeOffset) reservedArea[i] = snapshot.TreeOffset;

				foreach (var p in snapshot.Parents) {
					var parent = ViewData.ShaDictionary[p];
					var maxOfset = snapshot.TreeOffset > parent.TreeOffset ? snapshot.TreeOffset : parent.TreeOffset;
					for (int j = i; j <= parent.Index; j++) {
						if (reservedArea[j] < maxOfset) reservedArea[j] = maxOfset;
					}
				}
			}

			return reservedArea;
		}

		/// <summary>
		/// Return true if commits in the same line in the same branch and nothing in between
		/// *
		/// |
		/// |
		/// *
		/// </summary>
		private bool IsCommitsInOneLine(SnapshotVM a, SnapshotVM p) {
			if (p.TreeOffset != a.TreeOffset) return false;
			if (p.BranchLineId == a.BranchLineId) return true;

			for (int i = a.Index + 1; i < p.Index; i++) {
				if (RenderedSnapshots[i].IsCommitVisible && RenderedSnapshots[i].TreeOffset == p.TreeOffset) {
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Return true is we can draw simple up line
		///   ____ *
		///  /
		/// /
		/// |
		/// |
		/// *
		/// </summary>
		private bool IsSimpleUpLine(SnapshotVM s, SnapshotVM p) {
			if (!p.IsFirstInLine) return false;
			if (p.TreeOffset == s.TreeOffset) return false;

			var requiredEmptySpace = (p.Index - s.Index) * 0.6;
			if (RenderedSnapshots
					.Where(e => e.IsCommitVisible)
					.Where(e => p.Index - requiredEmptySpace < e.Index && e.Index < p.Index)
					.Any(e => e.TreeOffset == p.TreeOffset)
				) return false;

			return true;
		}

		/// <summary>
		/// Return true is we can draw simple down line
		///        *
		///        |
		///        |
		///        |
		///        /
		/// *____ /
		/// </summary>
		private bool IsSimpleDownLine(SnapshotVM s, SnapshotVM p) {
			if (!s.IsLastInLine) return false;
			if (p.TreeOffset == s.TreeOffset) return false;

			var requiredEmptySpace = (p.Index - s.Index) * 0.6;
			if (RenderedSnapshots
					.Where(e => e.IsCommitVisible)
					.Where(e => s.Index < e.Index && e.Index < s.Index + requiredEmptySpace)
					.Any(e => e.TreeOffset == s.TreeOffset)
				) return false;

			return true;
		}

		#endregion

		/// <summary>
		/// Update all elements highlighting
		/// </summary>
		public void Draw() {
			foreach (var snapshot in RenderedSnapshots) {
				if (string.IsNullOrEmpty(snapshot.Description)) continue;

				var isSelectedSnapshot = ViewData.Snapshot.Sha == snapshot.Sha || ViewData.SnapshotParent?.Sha == snapshot.Sha;
				UiElements[snapshot.Sha].Ellipse.StrokeThickness = isSelectedSnapshot ? SELECTED_LINE_WIDTH : NOT_SELECTED_LINE_WIDTH;
				UiElements[snapshot.Sha].CommitBackground.Fill = isSelectedSnapshot ? TransparentGreenBrush1 : TransparentCircleBrush;
			}

			if (ViewData.SnapshotParent == null) return;

			// Select path between two snapshots
			var c = ViewData.Snapshot.Sha;
			var p = ViewData.SnapshotParent.Sha;
			if (UiChildParentPaths.Keys.Contains(c + "|" + p)) {
				UiChildParentPaths[c + "|" + p].StrokeThickness = SELECTED_LINE_WIDTH;
			}
		}

		public void ClearHighlighting() {
			foreach (var snapshot in RenderedSnapshots) {
				UiElements[snapshot.Sha].Ellipse.StrokeThickness = NOT_SELECTED_LINE_WIDTH;
				foreach (var path in UiElements[snapshot.Sha].Paths) {
					path.StrokeThickness = NOT_SELECTED_LINE_WIDTH;
				}
			}
		}
	}
}
