using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using TimeLapseView;
using TimeLapseView.Model;

namespace WpfUI.Renderer {
	public abstract class BaseBackgroundRenderer {
		protected ViewData host;

		/// <summary>
		/// Get line background color based on line birth date (index)
		/// </summary>
		protected Brush GetLineBackgroundBrush(SnapshotVM snapshot, int linenum) {
			var brush = default(Brush);
			var lineSnapshotsNumber = host.Snapshots.Count - host.GetLineBirth(snapshot, linenum);
			if (lineSnapshotsNumber < 0) lineSnapshotsNumber = 0; // In case if host.Snapshot not yet updated with new commits.
			var lineLifeTimePercent = (lineSnapshotsNumber * 100.0 / (host.Snapshots.Count - host.Snapshot.Index)) / 100;
			if (lineLifeTimePercent > 1) lineLifeTimePercent = 1;
			if (host.Snapshots.Count == 1) lineLifeTimePercent = 0;
			var byteColor = Convert.ToByte(255 - (5 + 249 * lineLifeTimePercent));

			brush = new SolidColorBrush(Color.FromRgb(byteColor, 0xff, byteColor));
			return brush;
		}
	}
}
