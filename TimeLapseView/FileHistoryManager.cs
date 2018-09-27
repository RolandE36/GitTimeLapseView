using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView {
	public class FileHistoryManager {
		private const int PAGE_SIZE = 1000;

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

		public Action<List<Snapshot>> OnSnapshotsHistoryUpdated;

		private List<Snapshot> snapshots;
		private List<Snapshot> visibleSnapshots;

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

		public void GetCommitsHistory(CommitsAnalyzingStatus status) {
			if (snapshots == null) snapshots = new List<Snapshot>();
			visibleSnapshots = new List<Snapshot>();
			var commitsRelatedToFile = snapshots.Count(e => e.IsCommitRelatedToFile);

			using (var repo = new Repository(repositoryPath)) {
				// TODO: History in different branches
				// TODO: Add progress infomation

				status.ItemsTotal = repo.Commits.Count();

				// Full seek done
				if (repo.Commits.Count() < status.ItemsProcessed) {
					status.IsSeekCompleted = true;
					return;
				}

				var treeFile = filePath;
				// TODO: Investigate:
				// https://github.com/libgit2/libgit2sharp/issues/1074
				// var commits = repo.Commits.QueryBy(parameters.FilePath.Replace(_repository, "")).ToList();
				foreach (var commit in repo.Commits.Skip(status.ItemsProcessed).Take(status.ItemsPerPage)) {
					status.ItemsProcessed++;

					// Observable commit
					var snapshot = new Snapshot(commit.Sha) {
						FilePath = treeFile,
						Commit = new Commit(commit),
						IsCommitRelatedToFile = IsFileWasUpdated(commit, treeFile)
					};

					// Skip all commits before first changes
					if (snapshot.IsCommitRelatedToFile || snapshots.Count() != 0) {
						snapshots.Add(snapshot);
					}


					// TODO: Not working for tree. Should revrite code for files renaming
					/*
					// Check is file was renamed/moved
					treeFile = GetPreviousCommitFileName(snapshot, repo.Diff, commit, treeFile);

					// First file mention
					if (snapshot.FilePathState == FilePathState.Unknown || snapshot.FilePathState == FilePathState.Added) {
						break;
					}*/
				}

				// Do nothing in case if no changes found 
				if (snapshots.Count(e => e.IsCommitRelatedToFile) == 0) return;

				// Do nothing in case if no new changes found 
				if (commitsRelatedToFile == snapshots.Count(e => e.IsCommitRelatedToFile)) return;

				InitializeNewSnapshotsRelations();
				RemoveNotExistingParentsAndChilds(snapshots);
				LinkParentsWithChilds();
				UnlinkCommitsWithoutChanges();
				FindAllCommitAncestors();
				RemoveNotValuableLinks();
				GetSnapshotsWithChanges();

				if (visibleSnapshots.Count() == 0) {
					OnSnapshotsHistoryUpdated.Invoke(visibleSnapshots);
					return;
				}

				FindRelatedLines();
				AdvancedBranchesArchivation();

				FindRelatedLines();
				AdvancedBranchesArchivation();

				ReadFilesContent();
				FindLifeTimeForEachLine();

				// TODO: Related only to rendering. Move to other place
				var lineGroup = visibleSnapshots.GroupBy(e => e.BranchLineId);
				Parallel.ForEach(lineGroup, (commitsGroup) => {
					// Find line start/end
					commitsGroup.First().IsFirstInLine = true;
					commitsGroup.Last().IsLastInLine = true;
				});
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
				*/

				//SimpleBranchesArchivation();
			}

			OnSnapshotsHistoryUpdated.Invoke(visibleSnapshots);
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

		/// <summary>
		/// For rebuilding tree, we should clear all relations.
		/// </summary>
		private void InitializeNewSnapshotsRelations() {
			Parallel.ForEach(snapshots, (snapshot) => {
				snapshot.Childs.Clear();
				snapshot.Parents.Clear();
				snapshot.Commit.Base.Clear();
				snapshot.IsCommitVisible = true;
				foreach (var p in snapshot.Commit.Parents) {
					snapshot.Parents.Add(p);
				}
			});
		}

		/// <summary>
		/// Remove links to parent commits which not in the current sample (for performance reasons).
		/// </summary>
		private void RemoveNotExistingParentsAndChilds(List<Snapshot> items) {
			Parallel.ForEach(items, (snapshot) => {
				// .ToList() - for preventing collection modification
				foreach (var p in snapshot.Parents.ToList()) {
					if (!Snapshot.All.ContainsKey(p)) snapshot.Parents.Remove(p);
				}
				foreach (var c in snapshot.Childs.ToList()) {
					if (!Snapshot.All.ContainsKey(c)) snapshot.Childs.Remove(c);
				}
			});
		}

		/// <summary>
		/// Remove links to parent commits without changes
		/// </summary>
		public void RemoveParentsAndChildsWithoutChanges(List<Snapshot> items) {
			Parallel.ForEach(items, (snapshot) => {
				// .ToList() - for preventing collection modification
				foreach (var p in snapshot.Parents.ToList()) {
					if (!Snapshot.All[p].IsCommitVisible) snapshot.Parents.Remove(p);
				}
				foreach (var c in snapshot.Childs.ToList()) {
					if (!Snapshot.All[c].IsCommitVisible) snapshot.Childs.Remove(c);
				}
			});
		}

		/// <summary>
		/// Link parent and child commits
		/// </summary>
		private void LinkParentsWithChilds() {
			foreach (var snapshot in snapshots) {
				foreach (var sha in snapshot.Parents) {
					Snapshot.All[sha].Childs.Add(snapshot.Sha);
				}
			}
		}

		/// <summary>
		/// If file wasn't changed in commit then update links and hide commit
		/// </summary>
		private void UnlinkCommitsWithoutChanges() {
			for (int i = snapshots.Count - 1; i >= 0; i--) {
				var snapshot = snapshots[i];
				if (snapshot.IsCommitRelatedToFile) continue;

				foreach (var c in snapshot.Childs) {
					var child = Snapshot.All[c];
					foreach (var sha in snapshot.Parents) {
						if (!child.Parents.Contains(sha)) child.Parents.Add(sha);
					}
				}

				foreach (var p in snapshot.Parents) {
					var parent = Snapshot.All[p];
					foreach (var sha in snapshot.Childs) {
						if (!parent.Childs.Contains(sha)) parent.Childs.Add(sha);
					}
				}

				snapshot.IsCommitVisible = false;
			}
		}

		/// <summary>
		/// Remove not valuable commits
		/// </summary>
		private void GetSnapshotsWithChanges() {
			visibleSnapshots.Clear();
			visibleSnapshots.AddRange(snapshots.Where(e => e.IsCommitVisible));

			// Remove unvisible snapshots
			for (int i = 0; i < visibleSnapshots.Count; i++) {
				visibleSnapshots[i].VisibleIndex = i;
			}

			 RemoveParentsAndChildsWithoutChanges(visibleSnapshots);
		}

		/// <summary>
		/// Link related commits in to lines
		/// </summary>
		private void FindRelatedLines() {
			// Find related lines
			var offset = 0;
			var branch = 0;

			foreach (var snapshot in visibleSnapshots) {
				snapshot.TreeOffset = -1;
				snapshot.BranchLineId = -1;
			}

			foreach (var snapshot in visibleSnapshots) {
				// Set new offset is not yet defined
				if (snapshot.TreeOffset == -1) {
					snapshot.TreeOffset = offset++;
					snapshot.BranchLineId = branch++;
				}

				if (snapshot.Parents.Count == 0) continue;    // Do nothing if no parents
				
				foreach (var p in snapshot.Parents) {
					var parent = Snapshot.All[p];
					if (parent.TreeOffset != -1) continue;    // Do nothing if offset already defined

					if (snapshot.Parents.Count == 1 &&        // If commit has only one parrent 
						parent.Childs.Count != 1 &&           // and parent has several chils 
						parent.Childs.Last() == snapshot.Sha)
						continue;                             // than set only values from last child

					parent.TreeOffset = snapshot.TreeOffset;
					parent.BranchLineId = snapshot.BranchLineId;
					break;
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
		/// To remove not valuable links we should have details about all commit ancestors
		/// </summary>
		private void FindAllCommitAncestors() {
			// Calculate Commit Base history
			for (int i = snapshots.Count - 1; i >= 0; i--) {
				var snapshot = snapshots[i];
				if (!snapshot.IsCommitVisible) continue;

				foreach (var psha in snapshot.Parents) {
					AddAncestorToCommit(snapshot, psha);

					foreach (var bsha in Snapshot.All[psha].Commit.Base) {
						AddAncestorToCommit(snapshot, bsha.Key);
					}
				}
			}
		}

		/// <summary>
		/// Remove duplicates to cleanup relations
		/// </summary>
		private void RemoveNotValuableLinks() {
			// TODO: Investigate matrix (Parent cross Childs) perfomance
			// Remove not Important parents based on Base History
			foreach (var snapshot in snapshots.Where(e => e.IsCommitVisible)) {
				foreach (var p in snapshot.Parents.ToList()) {
					if (snapshot.Commit.Base[p] != 1) {
						snapshot.Parents.Remove(p);
					}
				}
			}
		}

		private void AdvancedBranchesArchivation() {
			// Where(e => e.TreeOffset != int.MaxValue) ???? Is it could be??
			var maxBranchoffset = visibleSnapshots.Where(e => e.TreeOffset != int.MaxValue).Max(e => e.TreeOffset);

			for (int i = 1; i < maxBranchoffset + 1; i++) {
				// Find line Y
				var miny = visibleSnapshots.Where(e => e.BranchLineId == i).Min(e => e.Id); // VisibleOrder ~= ID
				var maxy = visibleSnapshots.Where(e => e.BranchLineId == i).Max(e => e.Id);// ~= ID

				// Check lines before current
				for (int j = 0; j < i; j++) {
					var linesInOffset = visibleSnapshots.Where(e => e.TreeOffset == j).GroupBy(e => e.BranchLineId);
					var canChangeOffset = true;
					foreach (var line in linesInOffset) {
						var lmin = visibleSnapshots.Where(e => e.BranchLineId == line.Key).Min(e => e.Id);// ~= ID
						var lmax = visibleSnapshots.Where(e => e.BranchLineId == line.Key).Max(e => e.Id);// ~= ID

						// Is place empty
						if ((lmax >= miny || lmin >= miny) && (lmax <= maxy || lmin <= maxy)) {
							canChangeOffset = false;
							break;
						}
					}

					if (canChangeOffset) {
						foreach (var snapshot in visibleSnapshots.Where(e => e.BranchLineId == i)) snapshot.TreeOffset = j;
						break;
					}
				}
			}
		}

		/// <summary>
		/// Read file content
		/// </summary>
		private string LoadFileContent(Snapshot snapshot) {
			var blob = (Blob)snapshot.Commit.GitCommit[snapshot.FilePath].Target;
			// TODO: probably use commit.Encoding
			using (var reader = new StreamReader(blob.GetContentStream(), Encoding.UTF8)) {
				return reader.ReadToEnd();
			}
		}

		/// <summary>
		/// Reead files content
		/// </summary>
		private void ReadFilesContent() {
			Parallel.ForEach(visibleSnapshots, (snapshot) => {
				snapshot.LoadFileContent();
			});
		}

		private void InitializeSnapshotFileDetails(Snapshot snapshot) {
			// TODO: Calculate file size after read
			var fileLinesCount = snapshot.File.Split('\n').Count();

			snapshot.FileDetails = new CodeFile(fileLinesCount);
			for (var i = 0; i < fileLinesCount; i++) {
				snapshot.FileDetails.State[i] = LineState.Inserted;

				// new aproach
				var lineBaseId = CodeFile.LineBase.Count + 1;
				snapshot.FileDetails.LineHistory[i] = lineBaseId;
				CodeFile.LineBase[lineBaseId] = new HashSet<string>();
				CodeFile.LineBase[lineBaseId].Add(snapshot.Sha);
			}
		}

		/// <summary>
		/// Find life time of each line
		/// </summary>
		private void FindLifeTimeForEachLine() {
			// Set default values for commits without parents
			// visibleSnapshots => snapshots
			foreach (var snapshot in snapshots.Where(e => e.Parents.Count() == 0)) {
				InitializeSnapshotFileDetails(snapshot);
			};

			for (var i = visibleSnapshots.Count - 2; i >= 0; i--) {
				var snapshot = visibleSnapshots[i];

				if (snapshot.FileDetails != null &&  // Check is already processed
					snapshot.Parents.Count() == 0) { // New snapshots could be added after previous loop
					continue;
				}

				// TODO: Calculate file size after read
				snapshot.FileDetails = new CodeFile(snapshot.File.Split('\n').Count());

				foreach (var p in visibleSnapshots[i].Parents) {
					var parent = Snapshot.All[p];
					if (!parent.IsCommitVisible) continue;

					// TODO: Double check before commit
					if (parent.FileDetails == null) InitializeSnapshotFileDetails(parent);

					// TODO: OutOfMemoryException with large files in diff class.
					// TODO: https://github.com/mmanela/diffplex - ISidebySideDiffer 
					var diff = fileComparer.BuildDiffModel(parent.File, snapshot.File);

					int parentLineNumber = -1;
					int lineIndex = 0;
					foreach (var line in diff.Lines) {
						parentLineNumber++;
						switch (line.Type) {
							case ChangeType.Modified:
								snapshot.FileDetails.State[lineIndex] |= LineState.Modified;
								if (snapshot.FileDetails.State[lineIndex] == LineState.Modified) {

									var lineBaseId = CodeFile.LineBase.Count + 1;
									snapshot.FileDetails.LineHistory[lineIndex] = lineBaseId;
									CodeFile.LineBase[lineBaseId] = new HashSet<string>();
									CodeFile.LineBase[lineBaseId].Add(snapshot.Sha);
								}
								lineIndex++;
								break;
							case ChangeType.Inserted:
								snapshot.FileDetails.State[lineIndex] |= LineState.Inserted;
								if (snapshot.FileDetails.State[lineIndex] == LineState.Inserted) {

									var lineBaseId = CodeFile.LineBase.Count + 1;
									snapshot.FileDetails.LineHistory[lineIndex] = lineBaseId;
									CodeFile.LineBase[lineBaseId] = new HashSet<string>();
									CodeFile.LineBase[lineBaseId].Add(snapshot.Sha);
								}
								--parentLineNumber;
								lineIndex++;
								break;
							case ChangeType.Deleted:
								// Nothing to add. Parent line number already calculated.
								break;
							default:
								// Already unchanged
								// Initial commit wasn't added to history and as result, 
								// line could be unchanged for two parents.
								if (snapshot.FileDetails.State[lineIndex] == LineState.Unchanged) {
									var parentLineBaseId = parent.FileDetails.LineHistory[parentLineNumber];
									var snapshotLineBaseId = snapshot.FileDetails.LineHistory[lineIndex];
									foreach (var sha in CodeFile.LineBase[parentLineBaseId]) {
										if (!CodeFile.LineBase[snapshotLineBaseId].Contains(sha)) {
											CodeFile.LineBase[snapshotLineBaseId].Add(sha);
										}
									}

									//  E        Related lines should have the same history
									//  |    D   E is related to D
									//  C   /
									//  /\ /
									// /  B      initial parent was missed
									// A         so A and B is the same lines
									CodeFile.LineBase[parentLineBaseId] = CodeFile.LineBase[snapshotLineBaseId];
								} else {
									snapshot.FileDetails.State[lineIndex] |= LineState.Unchanged;
									var parentLineBaseId = parent.FileDetails.LineHistory[parentLineNumber];
									snapshot.FileDetails.LineHistory[lineIndex] = parentLineBaseId;
									CodeFile.LineBase[parentLineBaseId].Add(snapshot.Sha);
								}

								lineIndex++;
								break;
						}
					}
				}
			}
		}

		private void SimpleBranchesArchivation() {
			var maxBranchoffset = snapshots.Where(e => e.TreeOffset != int.MaxValue).Max(e => e.TreeOffset);
			int position = 1;
			for (int i = 1; i < maxBranchoffset + 1; i++) {
				if (snapshots.Any(e => Snapshot.All[e.Sha].IsCommitVisible && Snapshot.All[e.Sha].TreeOffset == i)) {
					foreach (var snapshot in snapshots.Where(e => e.TreeOffset == i)) snapshot.TreeOffset = position;
					position++;
				}
			}
		}
		/*
		/// <summary>
		/// Reserve position for the current branch. Any branch on the same position should move right.
		/// </summary>
		/// <param name="snapshot">Comit that claims position</param>
		private void ReserveBranchOffset(Snapshot snapshot) {
			for (int i = snapshot.VisibleOrder + 1; i < snapshots.Where(e => e.IsCommitVisible).Count(); i++) {
				if (snapshots[i].TreeOffset == snapshot.TreeOffset) {
					snapshots[i].TreeOffset++;
					ReserveBranchOffset(snapshots[i]);
				}
			}
		}
		*/
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
