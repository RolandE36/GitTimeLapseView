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

		// TODO: Use CodeLine class
		/// <summary>
		/// 
		/// </summary>
		//public CodeLine SelectedLine;

		public int SelectedSnapshotIndex;
		public int SelectedLine;
		public int SelectedLineLID;

		#region Events

		/// <summary>
		/// On changed current view index event.
		/// </summary>
		/// <param name="index">new selected index</param>
		public Action<int, Snapshot> OnViewIndexChangedEvent;

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
		}


		/// <summary>
		/// Change ative code index
		/// </summary>
		/// <param name="index">index in snapshots list</param>
		public void SetViewIndex(int index) {
			if (index < 0 || Snapshots.Count == 0) return;

			SnapshotIndex = index;
			OnViewIndexChangedEvent?.Invoke(index, Snapshot);
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
