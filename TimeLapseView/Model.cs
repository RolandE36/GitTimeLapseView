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
		public CodeFile FileDetails { get; set; }
		public Commit Commit { get; set; }
		public string Sha { get { return Commit.Sha; } }
		/// <summary>
		/// Commit position in branches tree
		/// </summary>
		public int TreeOffset { get; set; }
		public int BranchLineId { get; set; }
		public bool IsCommitRelatedToFile { get; set; }
		public bool IsCommitVisible { get; set; }

		public bool IsImportantCommit {
			get { return IsCommitVisible && IsCommitRelatedToFile && Commit.Parents.Count == 1; }
		}

		public bool IsMerge {
			get { return Commit.Parents.Count > 1; }
		}

		/// <summary>
		/// Tree visualization in text format
		/// </summary>
		public string TextTree { 
			get {
				return "";//new string(' ', TreeOffset*2) + "*"; // TODO: Delete this. Not required any more.
			} 
			set { } 
		}

		public double ViewIndex { get; set; }
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
		public string Email { get; set; }
		public string Description { get; set; }
		public string DescriptionShort { get; set; }
		public DateTimeOffset Date { get; set; }
		public List<string> Parents { get; set; }
		public List<string> Childs { get; set; }
		public string DateString {
			get	{
				return Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
			}
			set { }
		}

		public Commit(LibGit2Sharp.Commit commit) {
			Sha = string.Join("", commit.Sha);
			Author = commit.Author.Name;
			Email = commit.Author.Email;
			Description = commit.Message;
			DescriptionShort = commit.MessageShort.Replace("\n", " ");
			Date = commit.Author.When;
			Parents = new List<string>();
			Childs = new List<string>();
			foreach (var parent in commit.Parents) {
				Parents.Add(string.Join("", parent.Sha));
			}
		}
	}

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
		/// Number of the same line in parent commit
		/// </summary>
		public int[] ParentLineNumber;

		/// <summary>
		/// Index of the first commit with current line
		/// </summary>
		public int[] Birth;

		/// <summary>
		/// Index of the last commit with current line
		/// </summary>
		public int[] Death;

		/// <summary>
		/// Pointer for lines initialization
		/// </summary>
		private int cursor;

		/// <summary>
		/// Static variable for unique id's generation
		/// </summary>
		private static int uniqueId;

		/// <summary>
		/// Lines count
		/// </summary>
		public readonly int Count;

		/// <param name="lines">Lines count in file</param>
		public CodeFile(int lines) {
			Lid = new int[lines];
			State = new LineState[lines];
			ParentLineNumber = new int[lines];
			Birth = new int[lines];
			Death = new int[lines];
			Count = lines;
			cursor = 0;

			for (int i = 0; i < lines; i++) {
				Lid[i] = uniqueId++;
				State[i] = LineState.Unchanged;
			}
		}

		/// <summary>
		/// Initialize next line state in queue
		/// </summary>
		/// <param name="state">New state</param>
		/// <param name="parentLineNumber">Number of the same line in parent commit</param>
		public void InitializeNextLine(LineState state, int parentLineNumber) {
			State[cursor] = state;
			ParentLineNumber[cursor] = parentLineNumber;
			cursor++;
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

		/// <summary>
		/// Number of the same line in parent commit
		/// </summary>
		public int ParentLineNumber {
			get { return ParentFile.ParentLineNumber[Number]; }
			set { ParentFile.ParentLineNumber[Number] = value; }
		}
		/// <summary>
		/// Index of the first commit with current line
		/// </summary>
		public int Birth {
			get { return ParentFile.Birth[Number]; }
			set { ParentFile.Birth[Number] = value; }
		}

		/// <summary>
		/// Index of the last commit with current line
		/// </summary>
		public int Death {
			get { return ParentFile.Death[Number]; }
			set { ParentFile.Death[Number] = value; }
		}
	}
}
