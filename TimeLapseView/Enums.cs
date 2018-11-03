using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView {
	public static class Constants {
		// TODO: Use windows DateTime Format settings
		public const string DATE_TIME_FORMAT = "dd.MM.yyyy hh:mm";
		public const string DATE_FORMAT = "dd.MM.yyyy";
	}

	public enum LineState {
		Unknown   = 0x0000,
		Deleted   = 0x0001,
		Modified  = 0x0011,
		Inserted  = 0x0111,
		Unchanged = 0x1111,
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
