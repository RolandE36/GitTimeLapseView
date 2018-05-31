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
	public class CanvasTreeRenderer {
		public ViewData ViewData;
		public Canvas Canvas;

		private const int SCALE_Y = 10;
		private const int SCALE_X = 30;

		private readonly SolidColorBrush BlackBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10));
		private readonly SolidColorBrush BlueBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x82, 0xc8));
		private readonly SolidColorBrush GreenBrush = new SolidColorBrush(Color.FromRgb(0x3c, 0xb4, 0x4b));

		private readonly List<SolidColorBrush> Brushes = new List<SolidColorBrush>() {
			new SolidColorBrush(Color.FromRgb(0xe6, 0x19, 0x4b)),
			new SolidColorBrush(Color.FromRgb(0x3c, 0xb4, 0x4b)),
			new SolidColorBrush(Color.FromRgb(0xff, 0xe1, 0x19)),
			new SolidColorBrush(Color.FromRgb(0x00, 0x82, 0xc8)),
			new SolidColorBrush(Color.FromRgb(0xf5, 0x82, 0x31)),
			new SolidColorBrush(Color.FromRgb(0x91, 0x1e, 0xb4)),
			new SolidColorBrush(Color.FromRgb(0x46, 0xf0, 0xf0)),
			new SolidColorBrush(Color.FromRgb(0xf0, 0x32, 0xe6)),
			new SolidColorBrush(Color.FromRgb(0xd2, 0xf5, 0x3c)),
			new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x80)),
			new SolidColorBrush(Color.FromRgb(0xaa, 0x6e, 0x28)),
			new SolidColorBrush(Color.FromRgb(0x80, 0x00, 0x00)),
			new SolidColorBrush(Color.FromRgb(0xaa, 0xff, 0xc3)),
			new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x00)),
			new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x80)),
			new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
		};

		public CanvasTreeRenderer(ViewData viewData, Canvas canvas) {
			ViewData = viewData;
			Canvas = canvas;
		}

		public List<Ellipse> Ellipses = new List<Ellipse>();

		public void BuildTree() {
			var rnd = new Random();

			foreach (var snapshot in ViewData.Snapshots) {
				snapshot.ViewIndex = snapshot.Index;
			}
			
			// Calculate Y coordinates for visible commits
			double index = 0;
			bool previousSmall = false;
			foreach (var snapshot in ViewData.Snapshots) {
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

			foreach (var snapshot in ViewData.Snapshots) {
				if (!snapshot.IsCommitVisible) continue;

				foreach (var p in snapshot.Commit.Parents) {
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
						line.Stroke = Brushes[snapshot.BranchLineId % Brushes.Count];
						line.X1 = x1;
						line.Y1 = y1;
						line.X2 = x2;
						line.Y2 = y2;
						line.StrokeThickness = 1;

						Canvas.Children.Add(line);

						parent.UiElements.Add(line);
						snapshot.UiElements.Add(line);
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
						path.Stroke = Brushes[parent.BranchLineId % Brushes.Count];
						path.Data = new PathGeometry(new PathFigure[] { figure });
						path.Opacity = 1;

						Canvas.Children.Add(path);

						parent.UiElements.Add(path);
						snapshot.UiElements.Add(path);

						Canvas.SetLeft(path, x1);
						Canvas.SetTop(path,  y1);
					}
				}

				// Circle (Commit)
				var color = !snapshot.IsCommitRelatedToFile ? BlueBrush : GreenBrush;

				var diameter = SCALE_Y;
				if (!snapshot.IsImportantCommit) diameter = SCALE_Y / 2;

				var ellipse = new Ellipse();
				ellipse.Fill = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
				ellipse.Height = diameter;
				ellipse.Width = diameter;
				ellipse.StrokeThickness = 1;
				ellipse.Stroke = color;
				ellipse.ToolTip = snapshot.Commit.Description;
				ellipse.MouseEnter += Ellipse_OnMouseEnter;
				ellipse.MouseLeave += Ellipse_OnMouseLeave;
				ellipse.MouseLeftButtonDown += Ellipse_MouseLeftButtonDown;
				ellipse.Tag = snapshot.Index;
				snapshot.UiCircle = ellipse;

				var x = (SCALE_X - diameter/2) + 2* SCALE_X * snapshot.TreeOffset;
				var y = (SCALE_Y - diameter/2) + 2* SCALE_Y * snapshot.ViewIndex;

				Ellipses.Add(ellipse);
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
				if (!snapshot.IsImportantCommit) continue;

				TextBlock textBlock = new TextBlock();
				textBlock.Text = snapshot.Commit.DateString + " " + snapshot.Commit.DescriptionShort;
				textBlock.Foreground = BlackBrush;
				Canvas.SetLeft(textBlock, SCALE_X * 7);
				Canvas.SetTop(textBlock, y);
				Canvas.Children.Add(textBlock);
			}

			Canvas.Width = ViewData.Snapshots.Max(e => e.TreeOffset)*SCALE_X+SCALE_X;
			Canvas.Height = ViewData.Snapshots.Count* SCALE_Y * 2 + SCALE_Y;
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

		private void Ellipse_OnMouseEnter(object sender, MouseEventArgs e) {
			(sender as Ellipse).StrokeThickness = 2;
			
			var index = (int)(sender as Ellipse).Tag;
			var s = ViewData.Snapshots.First(f => f.Index == index);
			foreach (var element in s.UiElements) {
				(element as Shape).StrokeThickness = 2;
			}
		}

		private void Ellipse_OnMouseLeave(object sender, MouseEventArgs e) {
			(sender as Ellipse).StrokeThickness = 1;

			var index = (int)(sender as Ellipse).Tag;
			ViewData.SelectedSnapshotIndex = index;
			// TODO: em.... nothing will be changed....
		}

		private void Ellipse_MouseLeftButtonDown(object sender, MouseEventArgs e) {
			(sender as Ellipse).StrokeThickness = 1;

			var index = (int)(sender as Ellipse).Tag;
			var s = ViewData.Snapshots.First(f => f.Index == index);
			foreach (var element in s.UiElements) {
				(element as Shape).StrokeThickness = 1;
			}

			ViewData.SetViewIndex(index);
		}

		public void DrawRelated(HashSet<string> items, int size) {
			// TODO: Rewrite this. We shouldn't have additional dictionary
			var dictionary = new Dictionary<string, Snapshot>();
			foreach (var snapshot in ViewData.Snapshots) {
				dictionary[snapshot.Sha] = snapshot;
			}

			foreach (var item in items) {
				(dictionary[item].UiCircle as Ellipse).StrokeThickness = size;
			}
		}


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

			for (int i = a.Index + 1; i < p.Index; i++) {
				if (ViewData.Snapshots[i].IsCommitVisible && ViewData.Snapshots[i].TreeOffset == p.TreeOffset) {
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
			if (ViewData
					.Snapshots
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
			if (ViewData
					.Snapshots
					.Where(e => e.IsCommitVisible)
					.Where(e => s.ViewIndex < e.ViewIndex && e.ViewIndex < s.ViewIndex + requiredEmptySpace)
					.Any(e => e.TreeOffset == s.TreeOffset)
				) return false;

			return true;
		}

		#endregion
	}
}
