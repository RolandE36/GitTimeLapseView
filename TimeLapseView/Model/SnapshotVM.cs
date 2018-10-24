using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView.Model {
	/// <summary>
	/// Snapshot View Model
	/// </summary>
	public class SnapshotVM {
		public int Id { get; set; }
		public int Index { get; set; }
		public string FilePath { get; set; }
		public FilePathState FilePathState { get; set; }
		public string PreviousFilePath { get; set; }
		public CodeFile FileDetails { get; set; }
		public HashSet<string> Parents { get; set; }
		public HashSet<string> Childs { get; set; }
		public string Sha { get; set; }
		public int TreeOffset { get; set; }
		public int BranchLineId { get; set; }
		public bool IsCommitRelatedToFile { get; set; }
		public bool IsCommitVisible { get; set; }
		public bool IsSelected { get; set; }
		public bool IsImportantCommit { get; set; }
		public bool IsMerge { get; set; }
		public bool IsFirstInLine { get; set; }
		public bool IsLastInLine { get; set; }
		public string File { get; set; }
		public string Author { get; set; }
		public string Email { get; set; }
		public string AvatarUrl { get; set; }
		public DateTimeOffset Date { get; set; }
		public string Description { get; set; }
		public string DescriptionShort { get; set; }
		public string DateString { get; set; }
	}
}
