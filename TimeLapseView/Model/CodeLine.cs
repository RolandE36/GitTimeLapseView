using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView.Model {
	/// <summary>
	/// Represent details about line in snapshot
	/// </summary>
	public class CodeLine {
		/// <summary>
		/// Refference to parent file
		/// </summary>
		public CodeFile ParentFile;

		/// <summary>
		/// Line number in parent file
		/// </summary>
		public int Number;

		/// <summary>
		/// Line Life Id. LID is unique only in the scope of line life or one commit. Not unique in general.
		/// </summary>
		public int LID {
			get { return ParentFile.Lid[Number]; }
			set { ParentFile.Lid[Number] = value; }
		}

		/// <summary>
		/// Line state (Modified/Inserted/Unchanged/Deleted)
		/// </summary>
		public LineState State {
			get { return ParentFile.State[Number]; }
			set { ParentFile.State[Number] = value; }
		}
	}
}
