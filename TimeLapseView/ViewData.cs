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
	public class ViewData {
		public readonly DiffManager DiffManager = new DiffManager();

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
				return ShaDictionary[CurrentSnapshotSha];
			}
		}

		public SnapshotVM SnapshotParent {
			get {
				if (string.IsNullOrEmpty(ParentSnapshotSha)) return null;
				return ShaDictionary[ParentSnapshotSha];
			}
		}

		/// <summary>
		/// Index of current commit
		/// </summary>
		private string CurrentSnapshotSha;
		/// <summary>
		/// SHA of parent commit
		/// </summary>
		private string ParentSnapshotSha;

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
		public Action<int, SnapshotVM, SnapshotVM> OnViewIndexChanged;

		/// <summary>
		/// On snapshot selection changed
		/// </summary>
		public Action OnSelectionChanged;

		#endregion

		public ViewData() {
			preferredDowntWay = new Dictionary<string, string>();
			preferredUpWay = new Dictionary<string, string>();
			ShaDictionary = new Dictionary<string, SnapshotVM>();
			IdDictionary = new Dictionary<int, SnapshotVM>();
			SeekStatus = new CommitsAnalyzingStatus() { ItemsPerPage = 100 };
		}

		/// <summary>
		/// Index of the first commit with provided line nuber
		/// </summary>
		public int GetLineBirth(SnapshotVM snapshot, int lineID) {
			var selectedLineId = snapshot.FileDetails.LineHistory[lineID];

			if (selectedLineId == 0) return 0;

			// TODO: Investigate how to avoid .ToList() 
			// TODO: Investigate how to avoid All.ContainsKey(e) for preventing KeyNotFoundException
			var selectedLineCommits = CodeFile.LineBase[selectedLineId].ToList().Where(e => IdDictionary.ContainsKey(e));
			return selectedLineCommits.Max(e => IdDictionary[e].Index);
		}

		/// <summary>
		/// Index of the last commit with provided line nuber
		/// </summary>
		public int GetLineDeath(int lineID) {
			var selectedLineId = Snapshot.FileDetails.LineHistory[lineID];

			// TODO: Investigate how to avoid .ToList() 
			// TODO: Investigate how to avoid All.ContainsKey(e) for preventing KeyNotFoundException
			var selectedLineCommits = CodeFile.LineBase[selectedLineId].ToList().Where(e => IdDictionary.ContainsKey(e));
			return selectedLineCommits.Min(e => IdDictionary[e].Index);
		}

		#region Snapshots navigation

		/// <summary>
		/// Step down
		/// </summary>
		public void MoveToNextSnapshot() {
			SelectSnapshot(SnapshotParent.Index);
		}

		/// <summary>
		/// Step up
		/// </summary
		public void MoveToPrevSnapshot() {
			var c = FindPreferredUpWay(Snapshot);
			if (c == null) return;

			RememberPreferredWay(c.Sha, Snapshot.Sha);
			SelectSnapshot(c.Index);
		}

		/// <summary>
		/// Step left
		/// </summary
		public void MoveToLeftSnapshot() {
			var orderedParents = Snapshot.Parents.OrderBy(e => ShaDictionary[e].TreeOffset).ToList();
			var pIndex = orderedParents.IndexOf(SnapshotParent.Sha);

			if (pIndex > 0) {
				ChangeParentSnapshot(orderedParents[pIndex - 1]);
			}
		}

		/// <summary>
		/// Step right
		/// </summary
		public void MoveToRightSnapshot() {
			var orderedParents = Snapshot.Parents.OrderBy(e => ShaDictionary[e].TreeOffset).ToList();
			var pIndex = orderedParents.IndexOf(SnapshotParent.Sha);

			if (pIndex < orderedParents.Count-1) {
				ChangeParentSnapshot(orderedParents[pIndex + 1]);
			}
		}

		/// <summary>
		/// Change parent snapshot selection without changing current snapshot
		/// </summary>
		public void ChangeParentSnapshot(string sha) {
			var newNextElement = ShaDictionary[sha];
			RememberPreferredWay(Snapshot.Sha, newNextElement.Sha);
			ParentSnapshotSha = newNextElement.Sha;
			OnSelectionChanged?.Invoke();
		}

		/// <summary>
		/// Remember user choices
		/// </summary>
		private void RememberPreferredWay(string from, string to) {
			if (string.IsNullOrEmpty(to)) return;
			preferredDowntWay[from] = to;
			preferredUpWay[to] = from;
		}

		/// <summary>
		/// Select next snapshot according to viewed history
		/// </summary>
		private void FindPreferredDownWay(SnapshotVM snapshot) {
			ParentSnapshotSha = "";
			if (Snapshot.Parents.Count > 0) {
				string p = "";
				// Try to find next snapshot from history
				if (preferredDowntWay.Keys.Contains(snapshot.Sha)) p = preferredDowntWay[snapshot.Sha];
				// Try to fin next snapshot from the same line
				if (string.IsNullOrEmpty(p)) p = Snapshot.Parents.FirstOrDefault(e => ShaDictionary[e].TreeOffset == Snapshot.TreeOffset);
				// In other case take first snapshot
				if (string.IsNullOrEmpty(p)) p = Snapshot.Parents.First();

				ParentSnapshotSha = p;
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

				return ShaDictionary[c];
			}

			return null;
		}

		#endregion

		/// <summary>
		/// Change selected sanpshot
		/// </summary>
		/// <param name="index">index in snapshots list</param>
		public void SelectSnapshot(int index) {
			if (index < 0 || Snapshots.Count == 0) return;

			CurrentSnapshotSha = Snapshots[index].Sha;
			FindPreferredDownWay(Snapshot);
			RememberPreferredWay(Snapshot.Sha, SnapshotParent?.Sha);

			OnViewIndexChanged?.Invoke(index, Snapshot, SnapshotParent);
			OnSelectionChanged?.Invoke();
		}
	}
}
