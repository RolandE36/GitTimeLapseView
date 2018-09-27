using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView.Model {
	public class Snapshot : IDisposable {
		internal static Dictionary<string, Snapshot> All = new Dictionary<string, Snapshot>();

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

			var gitFile = Commit.GitCommit[FilePath];

			if (gitFile == null) {
				// TODO: Compare with previous file path/name
				// File was renamed or moved.
				file = string.Empty;
			} else {
				var blob = (Blob)Commit.GitCommit[FilePath].Target;
				// TODO: probably use commit.Encoding
				using (var reader = new StreamReader(blob.GetContentStream(), Encoding.UTF8)) {
					file = reader.ReadToEnd();
				}
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
}
