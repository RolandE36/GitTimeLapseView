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

		// TODO: Probably add current state
		//public RendererMode RendererMode;

		/// <summary>
		/// Show long name: Roman Lytvyn
		/// Or only initials: RL
		/// </summary>
		//public bool ShowFullAuthorName;

		public ViewData(List<Snapshot> snapshots) {
			Snapshots = snapshots;
			SelectedSnapshotIndex = -1;
			SelectedLine = -1;
			SelectedLineLID = -1;
			//ShowFullAuthorName = false;
		}
	}
}
