using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace WpfUI.Renderer {
	public static class ColorPalette {
		private static readonly List<Color> baseColors = new List<Color>() {
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

		private static readonly List<SolidColorBrush> BaseBrushes = new List<SolidColorBrush>();
		private static readonly List<SolidColorBrush> BackgroundBrushes = new List<SolidColorBrush>();
		private static readonly List<SolidColorBrush> HoveredBackgroundBrushes = new List<SolidColorBrush>();

		public static SolidColorBrush DELETED = new SolidColorBrush(Color.FromRgb(255, 153, 153));
		public static SolidColorBrush CHANGES = new SolidColorBrush(Color.FromRgb(244, 167, 33));

		static ColorPalette() {
			foreach (var color in baseColors) {
				BaseBrushes.Add(new SolidColorBrush(color));
				BackgroundBrushes.Add(new SolidColorBrush(ChangeBrightness(color, 0.9)));
				HoveredBackgroundBrushes.Add(new SolidColorBrush(ChangeBrightness(color, 0.8)));
			}
		}

		/// <summary>
		/// Return base brush associated with index
		/// </summary>
		/// <param name="i"></param>
		/// <returns></returns>
		public static SolidColorBrush GetBaseBrush(int i) {
			return BaseBrushes[i % BaseBrushes.Count];
		}

		/// <summary>
		/// Return background brush associated with index
		/// </summary>
		/// <param name="i"></param>
		/// <returns></returns>
		public static SolidColorBrush GetBackgroundBrush(int i) {
			return BackgroundBrushes[i % BackgroundBrushes.Count];
		}

		/// <summary>
		/// Return hovered background brush associated with index
		/// </summary>
		/// <param name="i"></param>
		/// <returns></returns>
		public static SolidColorBrush GetHoveredBackgroundBrush(int i) {
			return HoveredBackgroundBrushes[i % HoveredBackgroundBrushes.Count];
		}

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
	}
}
