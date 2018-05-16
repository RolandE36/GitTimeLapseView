using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
		// TODO: Should be private
		public Dictionary<string, Snapshot> dictionary;

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
		/// Answer is file was modified in current commit (in compare with all parent commits)
		/// </summary>
		/// <param name="commit">Current commit</param>
		/// <param name="file">File path</param>
		/// <returns>True if file was modified</returns>
		private bool IsFileWasUpdated(LibGit2Sharp.Commit commit, string file) {
			// File not exist
			if (commit.Tree[file] == null) return false;
			// TODO: File was deleted or renamed: if (commit.Tree[file] == null && commit.Parents.Count() > 0 && commit.Parents.All(e => e.Tree[file] != null)) return true;

			// It's first commit so file was just created.
			if (commit.Parents.Count() == 0) return true;

			// Did not exist before
			if (commit.Parents.All(e => e.Tree[file] == null)) return true;

			// Nothing related to parent files
			if (commit.Parents.All(e => e.Tree[file] == null || e.Tree[file].Target.Id != commit.Tree[file].Target.Id)) return true;

			return false;
		}

		public List<Snapshot> GetCommitsHistory() {
			snapshots = new List<Snapshot>();
			dictionary = new Dictionary<string, Snapshot>();

			using (var repo = new Repository(repositoryPath)) {
				// TODO: History in different branches
				// TODO: Add progress infomation

				var treeFile = filePath;
				// TODO: Investigate:
				// https://github.com/libgit2/libgit2sharp/issues/1074
				// var commits = repo.Commits.QueryBy(parameters.FilePath.Replace(_repository, "")).ToList();
				foreach (var commit in repo.Commits.Take(1000)) {

					// Observable commit
					var snapshot = new Snapshot() {
						Index = snapshots.Count,
						FilePath = treeFile,
						Commit = new Commit(commit),
						TreeOffset = -1,
						BranchLineId = -1,
						IsCommitRelatedToFile = IsFileWasUpdated(commit, treeFile),
						IsCommitVisible = true,
						UiElements = new List<object>()
					};

					snapshots.Add(snapshot);
					dictionary[snapshot.Sha] = snapshot;

					// TODO: Not working for tree. Should revrite code for files renaming
					/*
					// Check is file was renamed/moved
					treeFile = GetPreviousCommitFileName(snapshot, repo.Diff, commit, treeFile);

					// First file mention
					if (snapshot.FilePathState == FilePathState.Unknown || snapshot.FilePathState == FilePathState.Added) {
						break;
					}*/
				}

				// Remove not existing parents
				Parallel.ForEach(snapshots, (snapshot) => {
					foreach (var p in snapshot.Commit.Parents.ToList()) {
						if (!dictionary.ContainsKey(p)) snapshot.Commit.Parents.Remove(p);
					}
				});

				// Link parent and child commits
				foreach (var snapshot in snapshots) {
					foreach (var sha in snapshot.Commit.Parents) {
						dictionary[sha].Commit.Childs.Add(snapshot.Sha);
					}
				}

				// TODO: Slow perfomance
				// TODO: Probably wrong order
				// TODO: Very coplicated
				// Remove commits without changes in file (except merge requests to important file)
				for (int i = snapshots.Count - 1; i >= 0; i--) {
					var snapshot = snapshots[i];

					if (snapshot.Commit.Parents.Count == 0) continue;
					if (snapshot.Commit.Childs.Count == 0) continue;
					if (snapshot.IsImportantCommit) continue;
					if (!snapshot.IsCommitVisible) continue;

					// Mainly for merge requests with one child
					if (snapshot.Commit.Childs.Count == 1 &&
						dictionary[snapshot.Commit.Childs.First()].IsImportantCommit) continue;
						
					// TODO: Probably we shouldn't include all merges from the same line, only last one
					foreach (var c in snapshot.Commit.Childs) {
						var child = dictionary[c];
						foreach (var sha in snapshot.Commit.Parents) {
							if (!child.Commit.Parents.Contains(sha)) child.Commit.Parents.Add(sha);
						}
					}

					// TODO: Probably we shouldn't include all merges from the same line, only first one
					foreach (var p in snapshot.Commit.Parents) {
						var parent = dictionary[p];
						foreach (var sha in snapshot.Commit.Childs) {
							if (!parent.Commit.Childs.Contains(sha)) parent.Commit.Childs.Add(sha);
						}
					}

					snapshot.IsCommitVisible = false;
					//snapshot.Commit.Childs.Clear();
					//snapshot.Commit.Parents.Clear();
				}
				
				// Find related lines
				var offset = 0;
				var branch = 0;
				foreach (var snapshot in snapshots.Where(e => e.IsCommitVisible)) {
					// Set new offset is not yet defined
					if (snapshot.TreeOffset == -1) {
						snapshot.TreeOffset = offset++;
						snapshot.BranchLineId = branch++;
					}

					if (snapshot.Commit.Parents.Count == 0) continue;         // Do nothing if no parents
					var parent = dictionary[snapshot.Commit.Parents.First()]; // Get first parent
					if (parent.TreeOffset != -1) continue;                    // Do nothing if offset already defined

					if (snapshot.Commit.Parents.Count == 1 &&                 // If commit has only one parrent 
						parent.Commit.Childs.Count != 1 &&                    // and parent has several chils 
						parent.Commit.Childs.Last() == snapshot.Sha)
						continue;                                             // than set only values from last child

					parent.TreeOffset = snapshot.TreeOffset;
					parent.BranchLineId = snapshot.BranchLineId;
				}
				/*
				// TODO: Probably it's not required
				var lineGroup = snapshots.GroupBy(e => e.BranchLineId);
				Parallel.ForEach(lineGroup, (commitsGroup) => {
					// Hide lines without commits related to file
					if (commitsGroup.All(e => !e.IsCommitRelatedToFile)) {
						foreach (var commit in commitsGroup) {
							commit.IsCommitVisible = false;
						}
					}
				});

				// TODO: Related only to rendering. Move to other place
				lineGroup = snapshots.Where(e => e.IsCommitVisible).GroupBy(e => e.BranchLineId);
				Parallel.ForEach(lineGroup, (commitsGroup) => {
					// Find line start/end
					commitsGroup.First().IsFirstInLine = true;
					commitsGroup.Last().IsLastInLine = true;
				});

				// TODO: Probably it's not required
				// Unlink unvisible commits
				foreach (var snapshot in snapshots) {
					foreach (var p in snapshot.Commit.Parents.ToList()) {
						var parent = dictionary[p];
						if (!parent.IsCommitVisible) snapshot.Commit.Parents.Remove(p);
					}

					// TODO: Probably childs also
				}
				*/
				// Calculate Commit Base history
				for (int i = snapshots.Count - 1; i >= 0; i--) {
					var snapshot = snapshots[i];
					if (!snapshot.IsCommitVisible) continue;

					foreach (var psha in snapshot.Commit.Parents) {
						AddAncestorToCommit(snapshot, psha);

						foreach (var bsha in dictionary[psha].Commit.Base) {
							AddAncestorToCommit(snapshot, bsha.Key);
						}
					}
				}

				// TODO: Investigate matrix (Parent cross Childs) perfomance
				// Remove not Important parents based on Base History
				foreach (var snapshot in snapshots.Where(e => e.IsCommitVisible)) {
					foreach (var p in snapshot.Commit.Parents.ToList()) {
						if (snapshot.Commit.Base[p] != 1) {
							snapshot.Commit.Parents.Remove(p);
						}
					}
				}

				AdvancedBranchesArchivation();
				//SimpleBranchesArchivation();

				// Remove unvisible snapshots
				//for (int i = 0; i < snapshots.Count; i++) snapshots[i].IsCommitVisible = true;

				/*
				snapshots = snapshots.Where(e => e.IsCommitVisible).ToList();
				for (int i = 0; i < snapshots.Count; i++) snapshots[i].Index = i;
				*/
				// Read files content
				Parallel.ForEach(snapshots, (snapshot) => {
					// TODO: Lazy loading
					//snapshot.File = GetFileContent(snapshot, treeFile);
				});
				/*
				// Life time...
				//Parallel.ForEach(snapshots, (snapshot) => {
				foreach (var snapshot in snapshots) {
					foreach (var p in snapshot.Commit.Parents) {
						var parent = dictionary[p];

						// TODO: Compare with each parent file, not with previous snapshot
						// TODO: Remove snapshots without changes (as results of merge requests)
						// TODO: OutOfMemoryException with large files in diff class.
						// TODO: https://github.com/mmanela/diffplex - ISidebySideDiffer 
						var diff = fileComparer.BuildDiffModel(snapshot.File, parent.File);
						if (snapshot.FileDetails ==  null) snapshot.FileDetails = new CodeFile(diff.Lines.Count()*2);
						snapshot.FileDetails.ResetCursor();

						int parentLineNumber = -1;
						// TODO: Compare Line SubPieces -> diff.Lines[0].SubPieces
						foreach (var line in diff.Lines) {
							parentLineNumber++;
							switch (line.Type) {
								case ChangeType.Modified:
									snapshot.FileDetails.InitializeNextLine(LineState.Modified, -1);
									break;
								case ChangeType.Inserted:
									snapshot.FileDetails.InitializeNextLine(LineState.Inserted, -1);
									--parentLineNumber;
									break;
								case ChangeType.Deleted:
									// Nothing to add. Parent line number already calculated.
									continue;
								default:
									// TODO: count - 1 .........
									snapshot.FileDetails.InitializeNextLine(LineState.Unchanged, parentLineNumber);
									break;
							}
						}
					}
				}
				snapshots.Last().FileDetails = new CodeFile(0);
				/*
				// Find lifetime for all lines in all commits
				foreach (var snapshot in snapshots) {
					for (int j = 0; j < snapshot.FileDetails.Count; j++) {
						if (snapshot.FileDetails[j].Birth == 0) {
							MeasureLineLife(snapshot.Index, j, snapshot.Index, snapshot.FileDetails[j].LID);
						}
					}
				}*/
			}

			foreach(var snapshot in snapshots) {
				snapshot.FileDetails = new CodeFile(0);
			}

			return snapshots;
		}

		private void SimpleBranchesArchivation() {
			var maxBranchoffset = snapshots.Where(e => e.TreeOffset != int.MaxValue).Max(e => e.TreeOffset);
			int position = 1;
			for (int i = 1; i < maxBranchoffset + 1; i++) {
				if (snapshots.Any(e => dictionary[e.Sha].IsCommitVisible && dictionary[e.Sha].TreeOffset == i)) {
					foreach (var snapshot in snapshots.Where(e => e.TreeOffset == i)) snapshot.TreeOffset = position;
					position++;
				}
			}
		}

		private void AdvancedBranchesArchivation() {
			var maxBranchoffset = snapshots.Where(e => e.TreeOffset != int.MaxValue).Max(e => e.TreeOffset);

			for (int i = 1; i < maxBranchoffset + 1; i++) {
				// Find line Y
				var miny = snapshots.Where(e => e.BranchLineId == i).Min(e => e.Index);
				var maxy = snapshots.Where(e => e.BranchLineId == i).Max(e => e.Index);

				// Check lines before current
				for (int j = 0; j < i; j++) {
					var linesInOffset = snapshots.Where(e => e.TreeOffset == j).GroupBy(e => e.BranchLineId);
					var canChangeOffset = true;
					foreach (var line in linesInOffset) {
						var lmin = snapshots.Where(e => e.BranchLineId == line.Key).Min(e => e.Index);
						var lmax = snapshots.Where(e => e.BranchLineId == line.Key).Max(e => e.Index);

						// Is place empty
						if ((lmax >= miny || lmin >= miny) && (lmax <= maxy || lmin <= maxy)) {
							canChangeOffset = false;
							break;
						}
					}

					if (canChangeOffset) {
						foreach (var snapshot in snapshots.Where(e => e.BranchLineId == i)) snapshot.TreeOffset = j;
						break;
					}
				}
			}
		}

		/// <summary>
		/// Add sha as ancestor for commit
		/// </summary>
		private void AddAncestorToCommit(Snapshot snapshot, string sha) {
			if (!snapshot.Commit.Base.Keys.Contains(sha)) {
				snapshot.Commit.Base[sha] = 1;
			} else {
				snapshot.Commit.Base[sha]++;
			}
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

			var line = snapshots[snapshotIndex].FileDetails[lineIndex];
			line.Death = sequenceEnd;
			line.LID = lid;
			if (line.State == LineState.Unchanged && line.ParentLineNumber != -1) {
				// If line wasn't changed then go deeper
				return line.Birth = MeasureLineLife(snapshotIndex + 1, line.ParentLineNumber, sequenceEnd, line.LID);
			} else {
				// In other case we found line birthdate
				return line.Birth = snapshotIndex;
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
					ReserveBranchOffset(snapshots[i]);
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

		private string GetFileContent(Snapshot snapshot, string file) {
			var blob = (Blob)snapshot.Commit.GitCommit[file].Target;
			// TODO: probably use commit.Encoding
			using (var reader = new StreamReader(blob.GetContentStream(), Encoding.UTF8)) {
				return reader.ReadToEnd();
			}
		}
	}
}
