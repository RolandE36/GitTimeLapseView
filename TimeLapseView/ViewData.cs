using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView {
	public class ViewData {
		/// <summary>
		/// List of all snapshots related to file
		/// </summary>
		public readonly List<Snapshot> Snapshots;

		/// <summary>
		/// Currently viewed commit
		/// </summary>
		public Snapshot Snapshot {
			get {
				return Snapshots[SnapshotIndex];
			}
		}

		/// <summary>
		/// Index of current commit
		/// </summary>
		public int SnapshotIndex;

		public int SelectedSnapshotIndex;
		public int SelectedLine;
		public int SelectedLineLID;

		/// <summary>
		/// For storing history of viewing snapshots
		/// </summary>
		private Dictionary<string, string> preferredNextWay;

		#region Events

		/// <summary>
		/// On changed current view index
		/// </summary>
		/// <param name="index">new selected index</param>
		public Action<int, Snapshot> OnViewIndexChanged;

		/// <summary>
		/// On snapshot selection changed
		/// </summary>
		public Action OnSelectionChanged;

		#endregion

		public ViewData(List<Snapshot> snapshots) {
			Snapshots = snapshots;
			SelectedSnapshotIndex = -1;
			SelectedLine = -1;
			SelectedLineLID = -1;
			preferredNextWay = new Dictionary<string, string>();
		}


		/// <summary>
		/// Change selected sanpshot
		/// </summary>
		/// <param name="index">index in snapshots list</param>
		public void SelectSnapshot(int index) {
			if (index < 0 || Snapshots.Count == 0) return;
			UnselectSnapshots(false);

			SnapshotIndex = index;
			Snapshot.IsSelected = true;
			FindPreferredWay(Snapshot);

			OnViewIndexChanged?.Invoke(index, Snapshot);
			OnSelectionChanged?.Invoke();
		}

		/// <summary>
		/// Step down
		/// </summary>
		public void MoveToNextSnapshot() {
			var selected = Snapshot.All.Where(e => e.Value.IsSelected);
			if (selected.Count() != 2) return;

			var nextElement = selected.First(e => e.Value.Sha != Snapshot.Sha);

			UpdatePreferredWay(Snapshot.Sha, nextElement.Value.Sha);
			SelectSnapshot(nextElement.Value.Index);
		}

		/// <summary>
		/// Step up
		/// </summary
		public void MoveToPrevSnapshot() {
			var selected = Snapshot.All.Where(e => e.Value.IsSelected);
			if (selected.Count() > 2) return;

			if (Snapshot.Commit.Childs.Count > 0) {
				// Try to fin next snapshot from the same line
				var c = Snapshot.Commit.Childs.FirstOrDefault(e => Snapshot.All[e].TreeOffset == Snapshot.TreeOffset);
				// In other case take first snapshot
				if (c == null) c = Snapshot.Commit.Childs.First();
				// Do nothing if is't first snapshot
				if (c == null) return;

				UpdatePreferredWay(c, Snapshot.Sha);
				SelectSnapshot(Snapshot.All[c].Index);
			}
		}

		/// <summary>
		/// Step left
		/// </summary
		public void MoveToLeftSnapshot() {
			var selected = Snapshot.All.Where(e => e.Value.IsSelected);
			if (selected.Count() != 2) return;

			var nextElement = selected.First(e => e.Value.Sha != Snapshot.Sha);
			var orderedParents = Snapshot.Commit.Parents.OrderBy(e => Snapshot.All[e].TreeOffset).ToList();
			var pIndex = orderedParents.IndexOf(nextElement.Key);

			if (pIndex > 0) {
				var newNextElement = Snapshot.All[orderedParents[pIndex - 1]];

				UpdatePreferredWay(Snapshot.Sha, newNextElement.Sha);

				UnselectSnapshots(false);
				Snapshot.IsSelected = true;
				newNextElement.IsSelected = true;
				OnSelectionChanged?.Invoke();
			}
		}

		/// <summary>
		/// Step right
		/// </summary
		public void MoveToRightSnapshot() {
			var selected = Snapshot.All.Where(e => e.Value.IsSelected);
			if (selected.Count() != 2) return;

			var nextElement = selected.First(e => e.Value.Sha != Snapshot.Sha);
			var orderedParents = Snapshot.Commit.Parents.OrderBy(e => Snapshot.All[e].TreeOffset).ToList();
			var pIndex = orderedParents.IndexOf(nextElement.Key);

			if (pIndex < orderedParents.Count-1) {
				var newNextElement = Snapshot.All[orderedParents[pIndex + 1]];

				UpdatePreferredWay(Snapshot.Sha, newNextElement.Sha);

				UnselectSnapshots(false);
				Snapshot.IsSelected = true;
				newNextElement.IsSelected = true;
				OnSelectionChanged?.Invoke();
			}
		}

		/// <summary>
		/// Remember user choices
		/// </summary>
		private void UpdatePreferredWay(string from, string to) {
			preferredNextWay[from] = to;
		}

		/// <summary>
		/// Select next snapshot according to viewed history
		/// </summary>
		private void FindPreferredWay(Snapshot snapshot) {
			if (Snapshot.Commit.Parents.Count > 0) {
				string p = "";
				// Try to find next snapshot from history
				if (preferredNextWay.Keys.Contains(snapshot.Sha)) p = preferredNextWay[snapshot.Sha];
				// Try to fin next snapshot from the same line
				if (string.IsNullOrEmpty(p)) p = Snapshot.Commit.Parents.FirstOrDefault(e => Snapshot.All[e].TreeOffset == Snapshot.TreeOffset);
				// In other case take first snapshot
				if (string.IsNullOrEmpty(p)) p = Snapshot.Commit.Parents.First();
				Snapshot.All[p].IsSelected = true;
			}
		}

		/// <summary>
		/// Unselected all snapshots
		/// </summary>
		public void UnselectSnapshots(bool redraw = true) {
			foreach (var snapshot in Snapshot.All) {
				snapshot.Value.IsSelected = false;
			}

			if (redraw) OnSelectionChanged?.Invoke();
		}

		/// <summary>
		/// Select provided snapshots
		/// </summary>
		public void SelectSnapshots(HashSet<string> items) {
			UnselectSnapshots(false);
			foreach (var sha in items) {
				Snapshot.All[sha].IsSelected = true;
			}

			OnSelectionChanged?.Invoke();
		}
	}
}
