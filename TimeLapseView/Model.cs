using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView {
	public class Snapshot : IDisposable {
		public static Dictionary<string, Snapshot> All = new Dictionary<string, Snapshot>();

		public Snapshot this[string sha] {
			get => All[sha];
			set => All[sha] = value;
		}

		public Snapshot(string sha) {
			All[sha] = this;
			Id = All.Count;
			TreeOffset = -1;
			BranchLineId = -1;
			Childs = new HashSet<string>();
			Parents = new HashSet<string>();
		}

		public int Id { get; set; }
		public string FilePath { get; set; }
		public FilePathState FilePathState { get; set; }
		public string PreviousFilePath { get; set; }
		public CodeFile FileDetails { get; set; }
		public Commit Commit { get; set; }
		public HashSet<string> Parents { get; set; }
		public HashSet<string> Childs { get; set; }
		public string Sha { get { return Commit.Sha; } }
		/// <summary>
		/// Commit position in branches tree
		/// </summary>
		public int TreeOffset { get; set; }
		public int BranchLineId { get; set; }
		public bool IsCommitRelatedToFile { get; set; }
		// TODO: TBD: Rename Visible to File Changhed.
		public bool IsCommitVisible { get; set; }

		public bool IsSelected { get; set; }

		public bool IsImportantCommit {
			get { return IsCommitVisible && IsCommitRelatedToFile && Parents.Count == 1; }
		}

		/// <summary>
		/// Snanapshot order in visible list
		/// </summary>
		public int VisibleIndex { get; set; }

		public bool IsMerge {
			get { return Parents.Count > 1; }
		}

		//TODO: Rename. It's represend offset in the tree on view
		public double ViewIndex { get; set; }

		public bool IsFirstInLine { get; set; }
		public bool IsLastInLine { get; set; }

		/// <summary>
		/// Index of the first commit with provided line nuber
		/// </summary>
		public int GetLineBirth(int lineID) {
			var selectedLineId = FileDetails.LineHistory[lineID];

			if (selectedLineId == 0) return 0;

			var selectedLineCommits = CodeFile.LineBase[selectedLineId];
			return selectedLineCommits.Max(e => Snapshot.All[e].VisibleIndex);
		}

		/// <summary>
		/// Index of the last commit with provided line nuber
		/// </summary>
		public int GetLineDeath(int lineID) {
			var selectedLineId = FileDetails.LineHistory[lineID];
			var selectedLineCommits = CodeFile.LineBase[selectedLineId];
			return selectedLineCommits.Min(e => Snapshot.All[e].VisibleIndex);
		}


		private string file;
		public string File {
			get {
				if (string.IsNullOrEmpty(file)) LoadFileContent();
				return file;
			}
		}

		/// <summary>
		/// Read file content
		/// </summary>
		public void LoadFileContent() {
			if (!string.IsNullOrEmpty(file)) return;

			var blob = (Blob)Commit.GitCommit[FilePath].Target;
			// TODO: probably use commit.Encoding
			using (var reader = new StreamReader(blob.GetContentStream(), Encoding.UTF8)) {
				file = reader.ReadToEnd();
			}
		}

		private bool disposed = false;

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if (!this.disposed) {
				if (disposing) {
					All.Remove(Sha);
				}
				disposed = true;
			}
		}
	}

	public class Branch {
		public string Child;
		public string Parent;
		public string Current;
	}

	public class Commit {
		public LibGit2Sharp.Commit GitCommit;

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
		public HashSet<string> Parents { get; }
		public Dictionary<string, int> Base { get; set; }
		public string DateString {
			get	{
				return Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
			}
			set { }
		}

		public Commit(LibGit2Sharp.Commit commit) {
			GitCommit = commit;
			Sha = string.Join("", commit.Sha);
			Author = commit.Author.Name;
			Email = commit.Author.Email;
			Description = commit.Message;
			DescriptionShort = commit.MessageShort.Replace("\n", " ");
			Date = commit.Author.When;
			Parents = new HashSet<string>();
			Base = new Dictionary<string, int>();
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
		public static Dictionary<int, HashSet<string>> LineBase;


		/// <param name="lines">Lines count in file</param>
		public CodeFile(int lines) {
			Lid = new int[lines];
			State = new LineState[lines];
			LineHistory = new int[lines];
			Count = lines;
			if (LineBase == null) LineBase = new Dictionary<int, HashSet<string>>();

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
