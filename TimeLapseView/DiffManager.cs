using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeLapseView.Model;

namespace TimeLapseView {
	public class DiffManager {
		// Parent => Child => Line => State
		private Dictionary<int, Dictionary<int, Dictionary<int, LineState>>> DiffsDeleted { get; set; }
		private Dictionary<int, Dictionary<int, Dictionary<int, LineState>>> DiffsChanged { get; set; }

		public DiffManager() {
			DiffsDeleted = new Dictionary<int, Dictionary<int, Dictionary<int, LineState>>>();
			DiffsChanged = new Dictionary<int, Dictionary<int, Dictionary<int, LineState>>>();
		}

		/// <summary>
		/// Answer is line was deleted
		/// </summary>
		public bool IsDeleted(SnapshotVM currrent, SnapshotVM parent, int line) {
			// return false if it's last commit
			if (parent == null) return false;
			// Check is child commit analysed
			if (!DiffsDeleted.Keys.Contains(currrent.Index)) Compare(currrent, parent);
			// Check is parent commit analysed
			if (!DiffsDeleted[currrent.Index].Keys.Contains(parent.Index)) Compare(currrent, parent);

			return DiffsDeleted[currrent.Index][parent.Index].Keys.Contains(line);
		}

		/// <summary>
		/// Return type of changes (if any)
		/// </summary>
		public LineState GetChangesType(SnapshotVM currrent, SnapshotVM parent, int line) {
			// return false if it's last commit
			if (parent == null) return LineState.Unknown;
			// Check is child commit analysed
			if (!DiffsChanged.Keys.Contains(currrent.Index)) Compare(currrent, parent);
			// Check is parent commit analysed
			if (!DiffsChanged[currrent.Index].Keys.Contains(parent.Index)) Compare(currrent, parent);
			// Check line mentions 
			if (!DiffsChanged[currrent.Index][parent.Index].Keys.Contains(line)) return LineState.Unchanged;

			return DiffsChanged[currrent.Index][parent.Index][line];
		}

		/// <summary>
		/// Compare changes between snapshots and add to cache
		/// </summary>
		private void Compare(SnapshotVM currrent, SnapshotVM parent) {
			if (parent == null) return;
			DiffsDeleted[currrent.Index] = new Dictionary<int, Dictionary<int, LineState>>();
			DiffsDeleted[currrent.Index][parent.Index] = new Dictionary<int, LineState>();
			DiffsChanged[currrent.Index] = new Dictionary<int, Dictionary<int, LineState>>();
			DiffsChanged[currrent.Index][parent.Index] = new Dictionary<int, LineState>();

			var fileComparer = new InlineDiffBuilder(new Differ());
			var diff = fileComparer.BuildDiffModel(parent.File, currrent.File);
			int updatedLines = -1;
			int deletedLines = -1;

			foreach (var line in diff.Lines) {
				updatedLines++;
				deletedLines++;

				switch (line.Type) {
					case ChangeType.Modified:
						DiffsChanged[currrent.Index][parent.Index][updatedLines] = LineState.Modified;
						break;
					case ChangeType.Inserted:
						DiffsChanged[currrent.Index][parent.Index][updatedLines] = LineState.Inserted;
						deletedLines--;
						break;
					case ChangeType.Deleted:
						DiffsDeleted[currrent.Index][parent.Index][deletedLines] = LineState.Deleted;
						updatedLines--;
						break;
					default:
						break;
				}
			}
		}
	}
}
