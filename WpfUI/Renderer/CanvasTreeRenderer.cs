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

namespace WpfUI.Renderer {
	public class SnapshotCanvasModel {
		public Ellipse Ellipse;
		public List<Shape> Paths = new List<Shape>();
	}

	public class CanvasTreeRenderer {
		public ViewData ViewData;
		List<Snapshot> Snapshots;
		public Canvas Canvas;
		private Dictionary<string, SnapshotCanvasModel> UiElements;
		private Dictionary<string, Shape> UiChildParentPaths; // TODO: Investigate more advanced solution

		private const int SCALE_Y = 10;
		private const int SCALE_X = 10;
		private const int SELECTED_LINE_WIDTH = 3;
		private const int NOT_SELECTED_LINE_WIDTH = 1;

		private readonly SolidColorBrush BlackBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10));
		private readonly SolidColorBrush BlueBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x82, 0xc8));
		private readonly SolidColorBrush GreenBrush = new SolidColorBrush(Color.FromRgb(0x3c, 0xb4, 0x4b));

		private readonly List<Color> baseColors = new List<Color>() {
			Color.FromRgb(0xe6, 0x19, 0x4b),
			Color.FromRgb(0x3c, 0xb4, 0x4b),
			Color.FromRgb(0xff, 0xe1, 0x19),
			Color.FromRgb(0x00, 0x82, 0xc8),
			Color.FromRgb(0xf5, 0x82, 0x31),
			Color.FromRgb(0x91, 0x1e, 0xb4),
			Color.FromRgb(0x46, 0xf0, 0xf0),
			Color.FromRgb(0xf0, 0x32, 0xe6),
			Color.FromRgb(0xd2, 0xf5, 0x3c),
			Color.FromRgb(0x00, 0x80, 0x80),
			Color.FromRgb(0xaa, 0x6e, 0x28),
			Color.FromRgb(0x80, 0x00, 0x00),
			Color.FromRgb(0xaa, 0xff, 0xc3),
			Color.FromRgb(0x80, 0x80, 0x00),
			Color.FromRgb(0x00, 0x00, 0x80),
			Color.FromRgb(0x80, 0x80, 0x80)
		};

		private readonly List<SolidColorBrush> LinesBrushes = new List<SolidColorBrush>();
		private readonly List<SolidColorBrush> BackgroundBrushes = new List<SolidColorBrush>();
		private readonly List<SolidColorBrush> HoverBackgroundBrushes = new List<SolidColorBrush>();

		public CanvasTreeRenderer(ViewData viewData, List<Snapshot> snapshots, Canvas canvas) {
			ViewData = viewData;
			Snapshots = snapshots;
			Canvas = canvas;
			UiElements = new Dictionary<string, SnapshotCanvasModel>();
			UiChildParentPaths = new Dictionary<string, Shape>();

			foreach (var snapshot in Snapshots) {
				UiElements[snapshot.Sha] = new SnapshotCanvasModel();
			}

			foreach (var color in baseColors) {
				LinesBrushes.Add(new SolidColorBrush(color));
				BackgroundBrushes.Add(new SolidColorBrush(ChangeBrightness(color, 0.9)));
				HoverBackgroundBrushes.Add(new SolidColorBrush(ChangeBrightness(color, 0.8)));
			}
		}

		public void BuildTree() {
			var rnd = new Random();
			var textOffset = 0;

			foreach (var snapshot in Snapshots) {
				snapshot.ViewIndex = snapshot.VisibleIndex;
			}
			
			// Calculate Y coordinates for visible commits
			double index = 0;
			bool previousSmall = false;
			foreach (var snapshot in Snapshots) {
				if (!snapshot.IsCommitVisible) {
					index += 0;
					snapshot.ViewIndex = index;
				} else if (!snapshot.IsImportantCommit) {
					index += previousSmall ? 0.5 : 1;
					snapshot.ViewIndex = index;
					previousSmall = true;
				} else {
					index += 1;
					snapshot.ViewIndex = index;
					previousSmall = false;
				}
			}

			foreach (var snapshot in Snapshots) {
				var diameter = SCALE_Y;
				if (!snapshot.IsImportantCommit) diameter = SCALE_Y / 2;
				var x = (SCALE_X - diameter / 2) + 2 * SCALE_X * snapshot.TreeOffset;
				var y = (SCALE_Y - diameter / 2) + 2 * SCALE_Y * snapshot.ViewIndex;

				var rectangle = new Rectangle();
				rectangle.Fill = BackgroundBrushes[snapshot.BranchLineId % LinesBrushes.Count];
				rectangle.Width = 999999;
				rectangle.Height = SCALE_Y * 3;
				Canvas.SetLeft(rectangle, x * 0);
				Canvas.SetTop(rectangle, 2 * SCALE_Y * snapshot.ViewIndex);

				if (!snapshot.IsImportantCommit) {
					previousSmall = true;
				} else {
					previousSmall = false;
				}

				Canvas.Children.Add(rectangle);
				Canvas.SetZIndex(rectangle, -2);

				foreach (var p in snapshot.Parents.ToList()) { // TODO: Investigate .ToList();
					var parent = Snapshot.All[p];
					if (!parent.IsCommitVisible) continue;

					var x1 = SCALE_X + 2 * SCALE_X * snapshot.TreeOffset;
					var y1 = SCALE_Y + 2 * SCALE_Y * snapshot.ViewIndex;
					
					var x2 = SCALE_X + 2 * SCALE_X * parent.TreeOffset;
					var y2 = SCALE_Y + 2 * SCALE_Y * parent.ViewIndex;

					var x3 = x2;
					var y3 = y1;
					var x4 = x1;
					var y4 = y2;

					if (IsCommitsInOneLine(snapshot, parent)) {
						var line = new Line();
						line.Stroke = LinesBrushes[snapshot.BranchLineId % LinesBrushes.Count];
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
						path.Stroke = LinesBrushes[parent.BranchLineId % LinesBrushes.Count];
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

				// Circle (Commit)
				var color = !snapshot.IsCommitRelatedToFile ? BlueBrush : GreenBrush;

				var ellipse = new Ellipse();
				ellipse.Fill = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
				ellipse.Height = diameter;
				ellipse.Width = diameter;
				ellipse.StrokeThickness = NOT_SELECTED_LINE_WIDTH;
				ellipse.Stroke = color;
				ellipse.ToolTip = snapshot.Commit.Description;
				ellipse.MouseEnter += Ellipse_OnMouseEnter;
				ellipse.MouseLeave += Ellipse_OnMouseLeave;
				ellipse.MouseLeftButtonDown += Ellipse_MouseLeftButtonDown;
				ellipse.Tag = snapshot.Sha;

				UiElements[snapshot.Sha].Ellipse = ellipse;
				Canvas.Children.Add(ellipse);
				Canvas.SetLeft(ellipse, x);
				Canvas.SetTop(ellipse, y);
				/*
				BitmapImage bmpImage = new BitmapImage();
				bmpImage.BeginInit();
				bmpImage.UriSource = new Uri($@"https://www.gravatar.com/avatar/{CalculateMD5Hash(snapshot.Commit.Email)}?d=identicon&s={SCALE}", UriKind.RelativeOrAbsolute);
				bmpImage.EndInit();
 
				// Clipped Image
				Image clippedImage = new Image();
				clippedImage.Source = bmpImage;
				EllipseGeometry clipGeometry = new EllipseGeometry(new Point(SCALE/ 2, SCALE / 2), SCALE / 2, SCALE / 2);
				clippedImage.Clip = clipGeometry;
				
				Canvas.Children.Add(clippedImage);
				Canvas.SetLeft(clippedImage, x);
				Canvas.SetTop(clippedImage, y);*/

				//if (!snapshot.IsCommitRelatedToFile) continue;
				if (snapshot.Parents.Count > 0) {
					var maxOffset = snapshot.Parents.Max(e => Snapshot.All[e].TreeOffset);
					if (textOffset < maxOffset) textOffset = maxOffset;
				}
				if (textOffset < snapshot.TreeOffset) textOffset = snapshot.TreeOffset;

				if (!snapshot.IsImportantCommit) continue;

				TextBlock textBlock = new TextBlock();
				textBlock.Text = snapshot.Commit.DateString + " " + snapshot.Commit.DescriptionShort;
				textBlock.Foreground = BlackBrush;
				textBlock.Tag = snapshot.Sha;
				textBlock.MouseEnter += TextBlock_MouseEnter;
				textBlock.MouseLeave += TextBlock_MouseLeave;
				textBlock.MouseLeftButtonDown += TextBlock_MouseLeftButtonDown;
				Canvas.SetLeft(textBlock, 2 * SCALE_X * (textOffset + 1));
				Canvas.SetTop(textBlock, y - SCALE_Y / 2);
				Canvas.Children.Add(textBlock);
			}

			Canvas.Width = Snapshots.Max(e => e.TreeOffset)*SCALE_X+SCALE_X;
			Canvas.Height = Snapshots.Count* SCALE_Y * 2 + SCALE_Y;
		}

		private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			// TODO: Copy / paste
			var sha = (string)(sender as TextBlock).Tag;
			UiElements[sha].Ellipse.StrokeThickness = 2;
			foreach (var path in UiElements[sha].Paths) {
				path.StrokeThickness = SELECTED_LINE_WIDTH;
			}

			ViewData.SelectSnapshot(Snapshot.All[sha].VisibleIndex);
		}

		private void TextBlock_MouseLeave(object sender, MouseEventArgs e) {
			var tb = (sender as TextBlock);
			tb.FontWeight = FontWeights.Normal;

			var sha = (string)(sender as TextBlock).Tag;
			UiElements[sha].Ellipse.StrokeThickness = NOT_SELECTED_LINE_WIDTH;
			foreach (var path in UiElements[sha].Paths) {
				path.StrokeThickness = NOT_SELECTED_LINE_WIDTH;
			}
			Draw();
		}

		private void TextBlock_MouseEnter(object sender, MouseEventArgs e) {
			var tb = (sender as TextBlock);
			tb.FontWeight = FontWeights.SemiBold;

			var sha = (string)(sender as TextBlock).Tag;
			UiElements[sha].Ellipse.StrokeThickness = 2;
			foreach (var path in UiElements[sha].Paths) {
				path.StrokeThickness = SELECTED_LINE_WIDTH;
			}
			Draw();
		}

		/// <summary>
		/// Update all elements highlighting
		/// </summary>
		public void Draw() {
			foreach (var snapshot in Snapshots) {
				if (string.IsNullOrEmpty(snapshot.Commit.Description)) continue;

				UiElements[snapshot.Sha].Ellipse.StrokeThickness = snapshot.IsSelected ? SELECTED_LINE_WIDTH : NOT_SELECTED_LINE_WIDTH;
			}

			// Select path between two snapshots
			var selectedSnapshots = Snapshots.Where(e => e.IsSelected).OrderBy(e => e.VisibleIndex);
			if (selectedSnapshots.Count() == 2) {
				var c = selectedSnapshots.First().Commit.Sha;
				var p = selectedSnapshots.Last().Commit.Sha;
				if (UiChildParentPaths.Keys.Contains(c + "|" + p)) {
					UiChildParentPaths[c + "|" + p].StrokeThickness = SELECTED_LINE_WIDTH;
				}
			}
		}

		public void ClearHighlighting() {
			foreach (var snapshot in Snapshots) {
				UiElements[snapshot.Sha].Ellipse.StrokeThickness = NOT_SELECTED_LINE_WIDTH;
				foreach(var path in UiElements[snapshot.Sha].Paths) {
					path.StrokeThickness = NOT_SELECTED_LINE_WIDTH;
				}
			}
		}

		#region Mouse Events

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

			ViewData.SelectSnapshot(Snapshot.All[sha].VisibleIndex);
		}

		#endregion

		#region Tree preparation

		/// <summary>
		/// Return true if commits in the same line in the same branch and nothing in between
		/// *
		/// |
		/// |
		/// *
		/// </summary>
		private bool IsCommitsInOneLine(Snapshot a, Snapshot p) {
			if (p.TreeOffset != a.TreeOffset) return false;
			if (p.BranchLineId == a.BranchLineId) return true;

			for (int i = a.VisibleIndex + 1; i < p.VisibleIndex; i++) {
				if (Snapshots[i].IsCommitVisible && Snapshots[i].TreeOffset == p.TreeOffset) {
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
		private bool IsSimpleUpLine(Snapshot s, Snapshot p) {
			if (!p.IsFirstInLine) return false;
			if (p.TreeOffset == s.TreeOffset) return false;

			var requiredEmptySpace = (p.ViewIndex - s.ViewIndex) * 0.6;
			if (Snapshots
					.Where(e => e.IsCommitVisible)
					.Where(e => p.ViewIndex - requiredEmptySpace < e.ViewIndex && e.ViewIndex < p.ViewIndex)
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
		private bool IsSimpleDownLine(Snapshot s, Snapshot p) {
			if (!s.IsLastInLine) return false;
			if (p.TreeOffset == s.TreeOffset) return false;

			var requiredEmptySpace = (p.ViewIndex - s.ViewIndex) * 0.6;
			if (Snapshots
					.Where(e => e.IsCommitVisible)
					.Where(e => s.ViewIndex < e.ViewIndex && e.ViewIndex < s.ViewIndex + requiredEmptySpace)
					.Any(e => e.TreeOffset == s.TreeOffset)
				) return false;

			return true;
		}

		#endregion

		/// <summary>
		/// Change color brightness
		/// </summary>
		/// <param name="brightness">0..1</param>
		/// <returns>New color based on initial color and brightness</returns>
		public static Color ChangeBrightness(Color color, double brightness) {
			var r = (double)color.R;
			var g = (double)color.G;
			var b = (double)color.B;

			r = (255 - r) * brightness + r;
			g = (255 - g) * brightness + g;
			b = (255 - b) * brightness + b;

			return Color.FromArgb(color.A, (byte)r, (byte)g, (byte)b);
		}

		// TODO: Move to separate class
		public string CalculateMD5Hash(string input) {
			// step 1, calculate MD5 hash from input
			MD5 md5 = System.Security.Cryptography.MD5.Create();
			byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
			byte[] hash = md5.ComputeHash(inputBytes);

			// step 2, convert byte array to hex string
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < hash.Length; i++) {
				sb.Append(hash[i].ToString("X2"));
			}

			return sb.ToString().ToLower();
		}
	}
}
