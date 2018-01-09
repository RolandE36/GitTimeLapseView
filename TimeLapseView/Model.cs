using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView {
	public class Snapshot {
		public int Index { get; set; }
		public string File { get; set; }
		public string FilePath { get; set; }
		public FilePathState FilePathState { get; set; }
		public string PreviousFilePath { get; set; }
		public List<CodeLine> Lines = new List<CodeLine>();
		public Commit Commit { get; set; }
		public string Sha { get { return Commit.Sha; } }
		/// <summary>
		/// Commit position in branches tree
		/// </summary>
		public int TreeOffset { get; set; }
		/// <summary>
		/// Tree visualization in text format
		/// </summary>
		public string TextTree { 
			get {
				return new string(' ', TreeOffset*2) + "*";
			} 
			set { } 
		}
	}

	public class Branch {
		public string Child;
		public string Parent;
		public string Current;
	}

	public class Commit {
		public string Sha { get; set; }
		public string ShortSha { 
			get {
				return string.Join("", Sha.Take(7));
			}
			set { }
		}
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
		public List<string> Parents { get; set; }
		public string DateString {
			get	{
				return Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
			}
			set { }
		}

		public Commit(LibGit2Sharp.Commit commit) {
			Sha = string.Join("", commit.Sha);
			Author = commit.Author.Name;
			Description = commit.Message;
			DescriptionShort = commit.MessageShort.Replace("\n", " ");
			Date = commit.Author.When;
			Parents = new List<string>();
			foreach (var parent in commit.Parents) {
				Parents.Add(string.Join("", parent.Sha));
			}
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
