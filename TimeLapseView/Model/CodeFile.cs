using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView.Model {
	/// <summary>
	/// Represent details about all file lines in snapshot
	/// </summary>
	public class CodeFile {
		/// <summary>
		/// Line Life Id. LID is unique only in the scope of line life or one commit. Not unique in general.
		/// </summary>
		public int[] Lid;

		/// <summary>
		/// Line state (Modified/Inserted/Unchanged/Deleted)
		/// </summary>
		public LineState[] State;

		/// <summary>
		/// Static variable for unique id's generation
		/// </summary>
		private static int uniqueId;

		/// <summary>
		/// Lines count
		/// </summary>
		public readonly int Count;

		/// <summary>
		/// Link to dictionary with line life
		/// </summary>
		public int[] LineHistory;

		/// <summary>
		/// Life of each unique line sequence
		/// </summary>
		public static Dictionary<int, HashSet<int>> LineBase;


		/// <param name="lines">Lines count in file</param>
		public CodeFile(int lines) {
			Lid = new int[lines];
			State = new LineState[lines];
			LineHistory = new int[lines];
			Count = lines;
			if (LineBase == null) LineBase = new Dictionary<int, HashSet<int>>();

			for (int i = 0; i < lines; i++) {
				Lid[i] = uniqueId++;
				State[i] = LineState.Unknown;
			}
		}

		/// <summary>
		/// Return line details (Fully reference object)
		/// </summary>
		public CodeLine this[int i] {
			get {
				return new CodeLine {
					ParentFile = this,
					Number = i
				};
			}
		}
	}
}
