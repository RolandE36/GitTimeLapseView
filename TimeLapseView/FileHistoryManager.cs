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
		private Dictionary<string, Snapshot> dictionary;

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

		/// <summary>
		/// Answer is file was modified in current commit (in compare with any parent commit)
		/// </summary>
		/// <param name="commit">Current commit</param>
		/// <param name="file">File path</param>
		/// <returns>True if file was modified</returns>
		private bool IsFileWasUpdated(LibGit2Sharp.Commit commit, string file) {
			var fileWasChanged = false;

			// It's first commit so file was just created.
			if (commit.Parents.Count() == 0) fileWasChanged = true;

			foreach (var parent in commit.Parents) {
				if (parent.Tree[file] == null) {
					// File not exist in the parent commit (just creted or merged)
					fileWasChanged = true;
					break;
				}

				// File was updated (Target.Id not equal to parent one)
				if (parent.Tree[file].Target.Id != commit.Tree[file].Target.Id) {
					fileWasChanged = true;
					break;
				}
			}

			return fileWasChanged;
		}

		public List<Snapshot> GetCommitsHistory() {
			snapshots = new List<Snapshot>();
			dictionary = new Dictionary<string, Snapshot>();

			using (var repo = new Repository(repositoryPath)) {
				// TODO: History in different branches
				// TODO: Add progress infomation

				var treeFile = filePath;
				foreach (var commit in repo.Commits) {
					// No such file in the commit. Make no sense to continue.
					if (commit.Tree[treeFile] == null) break;

					// If nothing was changed then go to the next commit
					if (!IsFileWasUpdated(commit, treeFile)) continue;

					// Observable commit
					var snapshot = new Snapshot() {
						Index = snapshots.Count,
						FilePath = treeFile,
						Commit = new Commit(commit)
					};

					snapshots.Add(snapshot);
					dictionary[snapshot.Sha] = snapshot;

					// Get file text from commit
					var blob = (Blob)commit[treeFile].Target;
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

				// Find positions for all commits in branches tree
				snapshots[0].TreeOffset = 0;
				foreach (var snapshot in snapshots) {
					int offset = 0;
					foreach (var parentSha in snapshot.Commit.Parents) {
						// Parrent could be not related to this file
						if (!dictionary.ContainsKey(parentSha)) continue;

						var parent = dictionary[parentSha];
						parent.TreeOffset = snapshot.TreeOffset + offset++;

						if (parent.TreeOffset != snapshot.TreeOffset) {
							ReserveBranchOffset(parent);
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
		/// Reserve position for the current branch. Any branch on the same position should move right.
		/// </summary>
		/// <param name="snapshot">Comit that claims position</param>
		private void ReserveBranchOffset(Snapshot snapshot) {
			for (int i = snapshot.Index + 1; i < snapshots.Count; i++) {
				if (snapshots[i].TreeOffset == snapshot.TreeOffset) {
					snapshots[i].TreeOffset++;
					ReserveBranchOffset(snapshot);
				}
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
			// When you git commit normally, the current commit 
			// becomes the parent commit of the new commit that's introduced by the command.

			// When you git merge two commits (or branches, whatever) without fast-forwarding, 
			// a new commit will be created with both commits as parents. 
			// You can merge more than two commits in that way, so the new commit may have more than two parents.

			if (commit.Parents.Count() == 0) {
				// No parent commits. Stop history looping. Probably is't already last (first) commit.
				snapshot.FilePathState = FilePathState.Unknown;
				return string.Empty;
			}

			foreach (var parent in commit.Parents) {
				var treeComparer = diff.Compare<TreeChanges>(parent.Tree, commit.Tree);

				// If file was renamed than continue to work with previous name
				foreach (var f in treeComparer.Renamed) {
					if (name == f.Path) {
						snapshot.FilePathState |= FilePathState.Changed;
						snapshot.PreviousFilePath = f.OldPath;
					}
				}

				// If file was just added than nothing to continue
				foreach (var f in treeComparer.Added) {
					if (name == f.Path) {
						snapshot.FilePathState |= FilePathState.Added;
					}
				}

				// TODO: Probably we should set branch source in commit
				// TODO: Not sure about files conflicts (paralel creating the same file and moving to one branch!)
				// TODO: Add notificaton message: Added/Removed/Updated Code // Renamed/Moved/Created File
				// TODO: Investigate:
				//		- treeComparer.TypeChanged
				//		- treeComparer.Copied
			}

			if (snapshot.FilePathState == FilePathState.Added)   return string.Empty;
			if (snapshot.FilePathState == FilePathState.Changed) return snapshot.PreviousFilePath;

			// No path/name changes was found.
			snapshot.FilePathState = FilePathState.NotChanged;
			return name;
		}
	}
}
