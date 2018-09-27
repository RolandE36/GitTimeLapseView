using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeLapseView.Model;

namespace TimeLapseView {
	public class ViewData {
		/// <summary>
		/// List of all snapshots related to file
		/// </summary>
		public List<SnapshotVM> Snapshots {
			get { return snapshots; }
			set {
				ShaDictionary.Clear();
				IdDictionary.Clear();
				foreach (var val in value) {
					ShaDictionary[val.Sha] = val;
					IdDictionary[val.Id] = val;
				}
				snapshots = value;
			}
		}
		private List<SnapshotVM> snapshots;

		/// <summary>
		/// Key based access to available snapshots
		/// </summary>
		public Dictionary<string, SnapshotVM> ShaDictionary;
		public Dictionary<int, SnapshotVM> IdDictionary;

		/// <summary>
		/// Currently viewed commit
		/// </summary>
		public SnapshotVM Snapshot {
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

		public CommitsAnalyzingStatus SeekStatus { get; set; }

		/// <summary>
		/// For storing history of viewed snapshots
		/// </summary>
		private Dictionary<string, string> preferredDowntWay;
		private Dictionary<string, string> preferredUpWay;

		#region Events

		/// <summary>
		/// On changed current view index
		/// </summary>
		/// <param name="index">new selected index</param>
		public Action<int, SnapshotVM> OnViewIndexChanged;

		/// <summary>
		/// On snapshot selection changed
		/// </summary>
		public Action OnSelectionChanged;

		#endregion

		public ViewData() {
			SelectedSnapshotIndex = -1;
			SelectedLine = -1;
			SelectedLineLID = -1;
			preferredDowntWay = new Dictionary<string, string>();
			preferredUpWay = new Dictionary<string, string>();
			ShaDictionary = new Dictionary<string, SnapshotVM>();
			IdDictionary = new Dictionary<int, SnapshotVM>();
			SeekStatus = new CommitsAnalyzingStatus() { ItemsPerPage = 100 };
		}

		/// <summary>
		/// Index of the first commit with provided line nuber
		/// </summary>
		public int GetLineBirth(int lineID) {
			var selectedLineId = Snapshot.FileDetails.LineHistory[lineID];

			if (selectedLineId == 0) return 0;

			// TODO: Investigate how to avoid .ToList() 
			// TODO: Investigate how to avoid All.ContainsKey(e) for preventing KeyNotFoundException
			var selectedLineCommits = CodeFile.LineBase[selectedLineId].ToList().Where(e => IdDictionary.ContainsKey(e));
			return selectedLineCommits.Max(e => IdDictionary[e].VisibleIndex);
		}

		/// <summary>
		/// Index of the last commit with provided line nuber
		/// </summary>
		public int GetLineDeath(int lineID) {
			var selectedLineId = Snapshot.FileDetails.LineHistory[lineID];

			// TODO: Investigate how to avoid .ToList() 
			// TODO: Investigate how to avoid All.ContainsKey(e) for preventing KeyNotFoundException
			var selectedLineCommits = CodeFile.LineBase[selectedLineId].ToList().Where(e => IdDictionary.ContainsKey(e));
			return selectedLineCommits.Min(e => IdDictionary[e].VisibleIndex);
		}

		#region Snapshots navigation

		/// <summary>
		/// Step down
		/// </summary>
		public void MoveToNextSnapshot() {
			var selected = ShaDictionary.Where(e => e.Value.IsSelected);
			if (selected.Count() != 2) return;

			var nextElement = selected.First(e => e.Value.Sha != Snapshot.Sha);

			UpdatePreferredWay(Snapshot.Sha, nextElement.Value.Sha);
			SelectSnapshot(nextElement.Value.VisibleIndex);
		}

		/// <summary>
		/// Step up
		/// </summary
		public void MoveToPrevSnapshot() {
			var selected = ShaDictionary.Where(e => e.Value.IsSelected);
			if (selected.Count() > 2) return;

			var c = FindPreferredUpWay(Snapshot);
			if (c == null) return;

			UpdatePreferredWay(c.Sha, Snapshot.Sha);
			SelectSnapshot(c.VisibleIndex);
		}

		/// <summary>
		/// Step left
		/// </summary
		public void MoveToLeftSnapshot() {
			var selected = ShaDictionary.Where(e => e.Value.IsSelected);
			if (selected.Count() != 2) return;

			var nextElement = selected.First(e => e.Value.Sha != Snapshot.Sha);
			var orderedParents = Snapshot.Parents.OrderBy(e => ShaDictionary[e].TreeOffset).ToList();
			var pIndex = orderedParents.IndexOf(nextElement.Key);

			if (pIndex > 0) {
				var newNextElement = ShaDictionary[orderedParents[pIndex - 1]];

				UpdatePreferredWay(Snapshot.Sha, newNextElement.Sha);

				ResetSnapshotsSelection(false);
				Snapshot.IsSelected = true;
				newNextElement.IsSelected = true;
				OnSelectionChanged?.Invoke();
			}
		}

		/// <summary>
		/// Step right
		/// </summary
		public void MoveToRightSnapshot() {
			var selected = ShaDictionary.Where(e => e.Value.IsSelected);
			if (selected.Count() != 2) return;

			var nextElement = selected.First(e => e.Value.Sha != Snapshot.Sha);
			var orderedParents = Snapshot.Parents.OrderBy(e => ShaDictionary[e].TreeOffset).ToList();
			var pIndex = orderedParents.IndexOf(nextElement.Key);

			if (pIndex < orderedParents.Count-1) {
				var newNextElement = ShaDictionary[orderedParents[pIndex + 1]];

				UpdatePreferredWay(Snapshot.Sha, newNextElement.Sha);

				ResetSnapshotsSelection(false);
				Snapshot.IsSelected = true;
				newNextElement.IsSelected = true;
				OnSelectionChanged?.Invoke();
			}
		}

		/// <summary>
		/// Remember user choices
		/// </summary>
		private void UpdatePreferredWay(string from, string to) {
			preferredDowntWay[from] = to;
			preferredUpWay[to] = from;
		}

		/// <summary>
		/// Select next snapshot according to viewed history
		/// </summary>
		private void FindPreferredDownWay(SnapshotVM snapshot) {
			if (Snapshot.Parents.Count > 0) {
				string p = "";
				// Try to find next snapshot from history
				if (preferredDowntWay.Keys.Contains(snapshot.Sha)) p = preferredDowntWay[snapshot.Sha];
				// Try to fin next snapshot from the same line
				if (string.IsNullOrEmpty(p)) p = Snapshot.Parents.FirstOrDefault(e => ShaDictionary[e].TreeOffset == Snapshot.TreeOffset);
				// In other case take first snapshot
				if (string.IsNullOrEmpty(p)) p = Snapshot.Parents.First();
				ShaDictionary[p].IsSelected = true;
			}
		}

		/// <summary>
		/// Select previous snapshot according to viewed history
		/// </summary>
		private SnapshotVM FindPreferredUpWay(SnapshotVM snapshot) {
			if (Snapshot.Childs.Count > 0) {
				string c = "";
				// Try to find next snapshot from history
				if (preferredUpWay.Keys.Contains(snapshot.Sha)) c = preferredUpWay[snapshot.Sha];
				// Try to fin next snapshot from the same line
				if (string.IsNullOrEmpty(c)) c = Snapshot.Childs.FirstOrDefault(e => ShaDictionary[e].TreeOffset == Snapshot.TreeOffset);
				// In other case take first snapshot
				if (string.IsNullOrEmpty(c)) c = Snapshot.Childs.First();
				ShaDictionary[c].IsSelected = true;

				return ShaDictionary[c];
			}

			return null;
		}

		#endregion

		/// <summary>
		/// Reset selection
		/// </summary>
		public void ResetSnapshotsSelection(bool redraw = true) {
			foreach (var snapshot in Snapshots) {
				snapshot.IsSelected = false;
			}

			Snapshot.IsSelected = true;
			FindPreferredDownWay(Snapshot);

			if (redraw) OnSelectionChanged?.Invoke();
		}

		/// <summary>
		/// Select provided snapshots
		/// </summary>
		public void SelectSnapshots(HashSet<int> items) {
			ResetSnapshotsSelection(false);
			foreach (var sha in items) {
				IdDictionary[sha].IsSelected = true;
			}

			OnSelectionChanged?.Invoke();
		}

		/// <summary>
		/// Change selected sanpshot
		/// </summary>
		/// <param name="index">index in snapshots list</param>
		public void SelectSnapshot(int index) {
			if (index < 0 || Snapshots.Count == 0) return;

			SnapshotIndex = index;
			Snapshot.IsSelected = true;
			//FindPreferredDownWay(Snapshot);

			ResetSnapshotsSelection(false);

			OnViewIndexChanged?.Invoke(index, Snapshot);
			OnSelectionChanged?.Invoke();
		}
	}
}
