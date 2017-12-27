using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView {
	public enum LineState {
		Modified,
		Inserted,
		Unchanged,
		Deleted
	}

	public enum FilePathState {
		/// <summary>
		/// Probably initial commit
		/// </summary>
		Unknown = 0x0000,

		/// <summary>
		/// No file path/name changes
		/// </summary>
		NotChanged = 0x0001,

		/// <summary>
		/// File just created
		/// </summary>
		Added = 0x0011,

		/// <summary>
		/// File was renamed or moved
		/// </summary>
		Changed = 0x0111

		// TODO: Deleted
	}
}
