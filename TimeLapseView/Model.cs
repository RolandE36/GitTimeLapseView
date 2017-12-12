using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView {
	public class Snapshot {
		public string File;
		public List<CodeLine> Lines = new List<CodeLine>();
		public Commit Commit;
	}
	
	public class Commit {
		public string Author;
		public string Description;
		public DateTimeOffset Date;
	}

	/// <summary>
	/// Represent details about line of code in snapshot
	/// </summary>
	public class CodeLine {
		/// <summary>
		/// Line Life Id. LID is unique only in the scope of line life or one commit. Not unique in general.
		/// </summary>
		public int LID;

		/// <summary>
		/// Line state (Modified/Inserted/Unchanged/Deleted)
		/// </summary>
		public LineState State;

		/// <summary>
		/// Number of the same line in parent commit
		/// </summary>
		public int ParentLineNumber;

		/// <summary>
		/// Index of the first commit with current line
		/// </summary>
		public int SequenceStart;

		/// <summary>
		/// Index of the last commit with current line
		/// </summary>
		public int SequenceEnd;

		private static int maxlid = 1;

		public CodeLine() {
			LID = maxlid++;
		}
	}

	public enum LineState {
		Modified,
		Inserted,
		Unchanged,
		Deleted
	}
}
