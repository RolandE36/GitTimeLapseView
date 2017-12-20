using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView {
	public class Snapshot {
		public string File;
		public string FilePath;
		public FilePathState FilePathState;
		public string PreviousFilePath;
		public List<CodeLine> Lines = new List<CodeLine>();
		public Commit Commit { get; set; }
	}
	
	public class Commit {
		public string Sha { get; set; }
		public string AuthorInitials {
			get {
				return string.Join("", Author.Split(' ').Take(2).Select(e => e[0]));
			}
			set { }
		}
		public string Author { get; set; }
		public string Description { get; set; }
		public string DescriptionShort { get; set; }
		public DateTimeOffset Date { get; set; }
		public string DateString {
			get	{
				return Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
			}
			set { }
		}
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
}
