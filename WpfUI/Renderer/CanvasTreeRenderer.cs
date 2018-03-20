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

		private const int SCALE = 10;

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

			// TODO: Rewrite this
			var dictionary = new Dictionary<string, Snapshot>();
			foreach (var snapshot in ViewData.Snapshots) {
				dictionary[snapshot.Sha] = snapshot;
			}

			foreach (var snapshot in ViewData.Snapshots) {
				snapshot.ViewIndex = snapshot.Index;
			}
			
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
			

			// TODO: Implement without merges

			foreach (var snapshot in ViewData.Snapshots) {

				if (!snapshot.IsCommitVisible) continue;

				// Line 
				/*
				// TODO: Very complicated solution
				var parentLineMergeGroup = snapshot.Commit.Parents.Where(e => dictionary[e].IsCommitVisible).GroupBy(e => dictionary[e].BranchLineId);
				foreach (var group in parentLineMergeGroup) {
					var maxIndex = group.Max(e => dictionary[e].Index);
					var parent = group.First(e => dictionary[e].Index == maxIndex);
					*/
				for (int j = snapshot.Commit.Parents.Count - 1; j >= 0; j--) {
					var parent = snapshot.Commit.Parents[j];
					if (!dictionary.ContainsKey(parent)) continue;
					var p = dictionary[parent];
					/*
					// TODO: Very complicated solution
					var childParentsInCurrentLine = p.Commit.Childs.Where(e => dictionary[e].IsCommitVisible && dictionary[e].BranchLineId == snapshot.BranchLineId);
					var maxChildIndex = childParentsInCurrentLine.Max(e => dictionary[e].Index);
					var child = childParentsInCurrentLine.First(e => dictionary[e].Index == maxChildIndex);
					if (snapshot.Sha != dictionary[child].Sha) continue;
					*/
					if (!p.IsCommitVisible) continue;

					// var x1 = 10;
					// var y1 = 50*i;
					// var x2 = 10;
					// var y2 = 50*p.Index;
					// var x3 = 10 + 50 + (y2 - y1) / 10;
					// var y3 = (int)((y2 - y1)/2 + y1);
					// var sameLine = snapshot.Index == p.Index - 1;

					var x1 = SCALE + 2 * SCALE * snapshot.TreeOffset;
					var y1 = SCALE + 2 * SCALE * snapshot.ViewIndex;
					
					var x2 = SCALE + 2 * SCALE * p.TreeOffset;
					var y2 = SCALE + 2 * SCALE * p.ViewIndex;

					if (snapshot.Commit.Parents.Count > 1) {
						//y1 += SCALE;
					}

					//var x3 = 10 + 50*snapshot.TreeOffset + 50 + (y2 - y1) / 10;
					//var y3 = (int)((y2 - y1)/2 + y1);
					var x3 = x2;
					var y3 = y1;

					// TODO: If first in line make it curves nice

					if (y3 > y2) {
						x3 = x1;
						y3 = y2;
					}

					var sameLine = p.BranchLineId == snapshot.BranchLineId;

					if (sameLine) {
						var line = new Line();
						line.Stroke = Brushes[snapshot.BranchLineId % Brushes.Count];
						line.X1 = x1;
						line.Y1 = y1;
						line.X2 = x2;
						line.Y2 = y2;
						line.StrokeThickness = 1;

						Canvas.Children.Add(line);
					} else {
						// Point0 should be 0x0
						x2 -= x1;
						x3 -= x1;
						y2 -= y1;
						y3 -= y1;

						BezierSegment bezier = new BezierSegment()
						{
							Point1 = new Point(x3, y3),
							Point2 = new Point(x3, y3),
							Point3 = new Point(x2, y2),
							IsStroked = true
						};

						PathFigure figure = new PathFigure();
						figure.Segments.Add(bezier);

						Path path = new Path();
						path.Stroke = Brushes[p.BranchLineId % Brushes.Count];
						path.Data = new PathGeometry(new PathFigure[] { figure });

						Canvas.Children.Add(path);

						Canvas.SetLeft(path, x1);
						Canvas.SetTop(path,  y1);
					}
				}

				// Circle (Commit)
				var color = !snapshot.IsCommitRelatedToFile ? BlueBrush : GreenBrush;

				var diameter = SCALE;
				//if (!snapshot.IsCommitRelatedToFile) diameter = SCALE / 2;
				if (!snapshot.IsImportantCommit) diameter = SCALE / 2;

				var ellipse = new Ellipse();
				ellipse.Fill = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
				ellipse.Height = diameter;
				ellipse.Width = diameter;
				ellipse.StrokeThickness = 1;
				ellipse.Stroke = color;
				ellipse.ToolTip = snapshot.Commit.Description;
				ellipse.MouseEnter += Ellipse_OnMouseEnter;
				ellipse.MouseLeave += Ellipse_OnMouseLeave;
				//ellipse.Visibility = snapshot.IsCommitRelatedToFile ? Visibility.Visible : Visibility.Hidden;

				
				var x = (SCALE-diameter/2) + 2*SCALE*snapshot.TreeOffset;
				var y = (SCALE-diameter/2) + 2*SCALE*snapshot.ViewIndex;

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
				Canvas.SetLeft(textBlock, 150);
				Canvas.SetTop(textBlock, y);
				Canvas.Children.Add(textBlock);
			}

			Canvas.Width = ViewData.Snapshots.Max(e => e.TreeOffset)*SCALE+SCALE;
			Canvas.Height = ViewData.Snapshots.Count*SCALE+SCALE;
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
		}

		private void Ellipse_OnMouseLeave(object sender, MouseEventArgs e) {
			(sender as Ellipse).StrokeThickness = 1;
		}
	}
}
