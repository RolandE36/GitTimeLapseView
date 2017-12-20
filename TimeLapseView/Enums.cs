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
		/// File just created
		/// </summary>
		Added,

		/// <summary>
		/// File was renamed or moved
		/// </summary>
		Changed,

		/// <summary>
		/// No file path/name changes
		/// </summary>
		NotChanged,

		/// <summary>
		/// Probably initial commit
		/// </summary>
		Unknown

		// TODO: Deleted
	}
}
