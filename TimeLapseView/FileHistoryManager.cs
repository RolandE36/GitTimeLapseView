using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView {
	public class FileHistoryManager {
		/// <summary>
		/// Helper object for receiving differences between two files
		/// </summary>
		private static InlineDiffBuilder fileComparer = new InlineDiffBuilder(new Differ());

		/// <summary>
		/// Path to root repository directory
		/// </summary>
		private string repositoryPath;

		/// <summary>
		/// Path to investigated file (based on root dir path)
		/// </summary>
		public readonly string filePath;

		private List<Snapshot> snapshots;

		public FileHistoryManager(string file) {
			var fileInfo = new FileInfo(file);
			if (!fileInfo.Exists) throw new FileNotFoundException($"File '{file}' not found.");

			// Find repository path
			var dir = fileInfo.Directory;
			while (dir != null && !Repository.IsValid(dir.FullName)) {
				dir = dir.Parent;
			}

			// Find file path inside repository
			if (dir != null) {
				repositoryPath = dir.FullName;
				filePath = file.Replace(repositoryPath + "\\", "");
			} else {
				throw new Exception($"Git repository wasn't found.");
			}
		}

		public List<Snapshot> GetCommitsHistory() {
			snapshots = new List<Snapshot>();

			using (var repo = new Repository(repositoryPath)) {
				// TODO: History in different branches
				// TODO: Add progress infomation

				var treeFile = filePath;
				foreach (var commit in repo.Commits) {
					// Search commits where file was updated (Target.Id not equal to parent one)
					if (!(commit.Parents.Count() >= 1 &&
						  commit.Tree[treeFile] != null && 
						  (commit.Parents.First().Tree[treeFile] == null || 
						   commit.Tree[treeFile].Target.Id != commit.Parents.First().Tree[treeFile].Target.Id))) {

						// TODO: commit.Parents.First() - investigate possible issues
						continue;
					}

					// Observable commit
					var snapshot = new Snapshot() {
						FilePath = treeFile,
						Commit = new Commit() {
							Sha = string.Join("", commit.Sha.Take(8)),
							Author = commit.Author.Name,
							Description = commit.Message,
							DescriptionShort = commit.MessageShort.Replace("\n", " "),
							Date = commit.Author.When
						}
					};

					snapshots.Add(snapshot);

					// Get file text from commit
					var blob = (Blob) commit[treeFile].Target;
					// TODO: probably use commit.Encoding
					using (var reader = new StreamReader(blob.GetContentStream(), Encoding.UTF8)) {
						snapshot.File = reader.ReadToEnd();
						var count = snapshots.Count - 1;

						if (count > 0) {
							// TODO: OutOfMemoryException -> To many instances of CodeLine class 
							var diff = fileComparer.BuildDiffModel(snapshots[count].File, snapshots[count - 1].File);
							int parentLineNumber = -1;
							// TODO: Compare Line SubPieces -> diff.Lines[0].SubPieces
							foreach (var line in diff.Lines) {
								// TODO: Each line should have unique ID
								var diffLine = new CodeLine();
								parentLineNumber++;
								switch (line.Type) {
									case ChangeType.Modified:
										diffLine.State = LineState.Modified;
										diffLine.ParentLineNumber = -1;
										break;
									case ChangeType.Inserted:
										diffLine.State = LineState.Inserted;
										--parentLineNumber;
										diffLine.ParentLineNumber = -1;
										break;
									case ChangeType.Deleted:
										// Nothing to add. Parent line number already calculated.
										continue;
									default:
										diffLine.State = LineState.Unchanged;
										diffLine.ParentLineNumber = parentLineNumber;
										break;
								}

								snapshots[count - 1].Lines.Add(diffLine);
							}
						}
					}

					// Check is file was renamed/moved
					treeFile = GetPreviousCommitFileName(snapshot, repo.Diff, commit, treeFile);

					// First file mention
					if (snapshot.FilePathState == FilePathState.Unknown || snapshot.FilePathState == FilePathState.Added) {
						break;
					}
				}

				// Find lifetime for all lines in all commits
				for (int i = 0; i < snapshots.Count; i++) {
					for (int j = 0; j < snapshots[i].Lines.Count; j++) {
						if (snapshots[i].Lines[j].SequenceStart == 0) {
							MeasureLineLife(i, j, i, snapshots[i].Lines[j].LID);
						}
					}
				}
			}

			return snapshots;
		}

		/// <summary>
		/// Find first/last commits of line life
		/// </summary>
		/// <param name="snapshotIndex">Commit Index</param>
		/// <param name="lineIndex">Line Index</param>
		/// <param name="sequenceEnd">Last Commit Index</param>
		/// <param name="lid">Line Life ID</param>
		/// <returns>First Commit Index</returns>
		private int MeasureLineLife(int snapshotIndex, int lineIndex, int sequenceEnd, int lid) {
			// Stop if we reach last commit.
			if (snapshotIndex == snapshots.Count - 1) {
				return snapshotIndex;
			}

			var line = snapshots[snapshotIndex].Lines[lineIndex];
			line.SequenceEnd = sequenceEnd;
			line.LID = lid;
			if (line.State == LineState.Unchanged) {
				// If line wasn't changed then go deeper
				return line.SequenceStart = MeasureLineLife(snapshotIndex + 1, line.ParentLineNumber, sequenceEnd, line.LID);
			} else {
				// In other case we found line birthdate
				return line.SequenceStart = snapshotIndex;
			}
		}

		/// <summary>
		/// Verify is file had the same name in the previous commit or was renamed/moved
		/// </summary>
		/// <param name="snapshot">Observable snapshot</param>
		/// <param name="diff">Commits comparer</param>
		/// <param name="commit">Current commit</param>
		/// <param name="name">Current file name</param>
		/// <returns>File name in previous commit</returns>
		private string GetPreviousCommitFileName(Snapshot snapshot, Diff diff, LibGit2Sharp.Commit commit, string name) {
			// TODO: Add notificaton message: Added/Removed/Updated Code // Renamed/Moved/Created File
			// TODO: commit.Parents.FirstOrDefault(); - investigate possible issues
			var parent = commit.Parents.FirstOrDefault();

			if (parent == null) {
				// No parent commits. Stop history looping. Probably is't already last (first) commit.
				snapshot.FilePathState = FilePathState.Unknown;
				return string.Empty;
			}

			var treeComparer = diff.Compare<TreeChanges>(parent.Tree, commit.Tree);

			// If file was renamed than continue to work with previous name
			foreach (var f in treeComparer.Renamed) {
				if (name == f.Path) {
					snapshot.FilePathState = FilePathState.Changed;
					return snapshot.PreviousFilePath = f.OldPath;
				}
			}

			// If file was just adced than nothing to continue
			foreach (var f in treeComparer.Added) {
				if (name == f.Path) {
					snapshot.FilePathState = FilePathState.Added;
					return string.Empty;
				}
			}

			// TODO: Investigate:
			//		- treeComparer.TypeChanged
			//		- treeComparer.Copied

			// No name changed was found.
			snapshot.FilePathState = FilePathState.NotChanged;
			return name;
		}
	}
}
