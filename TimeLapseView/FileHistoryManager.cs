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
			// It's first commit so file was just created.
			bool fileWasChanged = !commit.Parents.Any();

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

					// TODO: Not sure that it was good idea
					// If nothing was changed then go to the next commit
					// if (!IsFileWasUpdated(commit, treeFile)) continue;

					// Observable commit
					var snapshot = new Snapshot() {
						Index = snapshots.Count,
						FilePath = treeFile,
						Commit = new Commit(commit),
						TreeOffset = -1,
						BranchLineId = -1
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
							// TODO: Compare with each parent file, not with previous snapshot
							// TODO: Remove snapshots without changes (as results of merge requests)
							// TODO: OutOfMemoryException with large files in diff class.
							// TODO: https://github.com/mmanela/diffplex - ISidebySideDiffer 
							var diff = fileComparer.BuildDiffModel(snapshots[count].File, snapshots[count - 1].File);
							snapshots[count - 1].FileDetails = new CodeFile(diff.Lines.Count(e => e.Type != ChangeType.Deleted));
							int parentLineNumber = -1;
							// TODO: Compare Line SubPieces -> diff.Lines[0].SubPieces
							foreach (var line in diff.Lines) {
								parentLineNumber++;
								switch (line.Type) {
									case ChangeType.Modified:
										snapshots[count - 1].FileDetails.InitializeNextLine(LineState.Modified, -1);
										break;
									case ChangeType.Inserted:
										snapshots[count - 1].FileDetails.InitializeNextLine(LineState.Inserted, -1);
										--parentLineNumber;
										break;
									case ChangeType.Deleted:
										// Nothing to add. Parent line number already calculated.
										continue;
									default:
										// TODO: count - 1 .........
										snapshots[count - 1].FileDetails.InitializeNextLine(LineState.Unchanged, parentLineNumber);
										break;
								}
							}
						}
					}

					snapshots.Last().FileDetails = new CodeFile(0);

					// Check is file was renamed/moved
					treeFile = GetPreviousCommitFileName(snapshot, repo.Diff, commit, treeFile);

					// First file mention
					if (snapshot.FilePathState == FilePathState.Unknown || snapshot.FilePathState == FilePathState.Added) {
						break;
					}
				}

				// Find lifetime for all lines in all commits
				for (int i = 0; i < snapshots.Count; i++) {
					for (int j = 0; j < snapshots[i].FileDetails.Count; j++) {
						if (snapshots[i].FileDetails[j].Birth == 0) {
							MeasureLineLife(i, j, i, snapshots[i].FileDetails[j].LID);
						}
					}
				}

				// Remove not existing parents
				foreach (var snapshot in snapshots) {
					for (int i = snapshot.Commit.Parents.Count-1; i >= 0; i--) {
						if (!dictionary.ContainsKey(snapshot.Commit.Parents[i])) snapshot.Commit.Parents.RemoveAt(i);
					}
				}

				// Link parent and child commits
				foreach (var snapshot in snapshots) {
					foreach (var sha in snapshot.Commit.Parents) {
						dictionary[sha].Commit.Childs.Add(snapshot.Sha);
					}
				}

				// Find related lines
				var offset = 0;
				var branch = 0;
				foreach (var snapshot in snapshots) {
					// Set new offset is not yet defined
					if (snapshot.TreeOffset == -1) {
						snapshot.TreeOffset = offset++;
						snapshot.BranchLineId = branch++;
					}

					if (snapshot.Commit.Parents.Count == 0) continue;    // Fo nothing if no parents
					var parent = dictionary[snapshot.Commit.Parents[0]]; // Get first parent
					if (parent.TreeOffset != -1) continue;               // Do nothing if offset already defined
					
					if (snapshot.Commit.Parents.Count == 1 &&            // If commit has only one parrent 
						parent.Commit.Childs.Count != 1 &&               // and parent has several chils 
						parent.Commit.Childs.Last() == snapshot.Sha) 
						continue;                                        // than set only values from last child

					parent.TreeOffset   = snapshot.TreeOffset;
					parent.BranchLineId = snapshot.BranchLineId;
				}

				// Archivation
				// TODO: Not optimized
				var maxBranchoffset = snapshots.Max(e => e.TreeOffset);
				for (int i = 1; i < maxBranchoffset+1; i++) {
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

			GraphTest();
			return snapshots;
		}

		// TODO: Move to separate class
		public string CalculateMD5Hash(string input) {
			// step 1, calculate MD5 hash from input
			MD5 md5 = System.Security.Cryptography.MD5.Create();
			byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
			byte[] hash = md5.ComputeHash(inputBytes);

			// step 2, convert byte array to hex string
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < hash.Length; i++) {
				sb.Append(hash[i].ToString("X2"));
			}

			return sb.ToString().ToLower();
		}

		// TODO: Move to separate class
		private void GraphTest() {

			var colors = new List<string>() {
							"#e6194b",
							"#3cb44b",
							"#ffe119",
							"#0082c8",
							"#f58231",
							"#911eb4",
							"#46f0f0",
							"#f032e6",
							"#d2f53c",
							"#fabebe",
							"#008080",
							"#e6beff",
							"#aa6e28",
							"#fffac8",
							"#800000",
							"#aaffc3",
							"#808000",
							"#ffd8b1",
							"#000080",
							"#808080"
						};




			var rnd = new Random();


			var fileName = @"Wtree.html";
			//var fileName = @"Wtree_"+DateTime.Now.Ticks+".html";

			using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileName, false)) {

				file.WriteLine(@"<!DOCTYPE html>
								<html>
								<body>
								<svg height='21000' width='5000'>");

				int i = 0;
				foreach (var snapshot in snapshots) {
					var color = snapshot.Commit.Parents.Count == 1 ? "blue" : "green";
					file.WriteLine(string.Format("<text x='{0}' y='{1}' fill='red'>{2}</text>", 300, 50 * i + 5, snapshot.Commit.DescriptionShort));
					for (int j = snapshot.Commit.Parents.Count - 1; j >= 0; j--)
					//foreach (var parent in snapshot.Commit.Parents)
					{
						var parent = snapshot.Commit.Parents[j];
						if (!dictionary.ContainsKey(parent)) continue;
						var p = dictionary[parent];

						/*var x1 = 10;
						var y1 = 50*i;
						var x2 = 10;
						var y2 = 50*p.Index;
						var x3 = 10 + 50 + (y2 - y1) / 10;
						var y3 = (int)((y2 - y1)/2 + y1);
						var sameLine = snapshot.Index == p.Index - 1;*/

						var x1 = 10 + 50 * snapshot.TreeOffset;
						var y1 = 50 * i;
						var x2 = 10 + 50 * p.TreeOffset;
						var y2 = 50 * p.Index;
						//var x3 = 10 + 50*snapshot.TreeOffset + 50 + (y2 - y1) / 10;
						//var y3 = (int)((y2 - y1)/2 + y1);
						var x3 = x2;
						var y3 = y1;

						if (y3 > y2) {
							x3 = x1;
							y3 = y2;
						}

						var sameLine = p.BranchLineId == snapshot.BranchLineId;

						/*if (sameLine)
						{
							for (int l = snapshot.Index + 1; l <= p.Index - 1; l++)
							{
								if (snapshots[l].TreeOffset <= snapshot.TreeOffset)
								{
									sameLine = false;
									break;
								}
							}
						}*/
						if (sameLine) {
							file.WriteLine(string.Format("<line x1='{0}' y1='{1}' x2='{2}' y2='{3}' style='stroke:{4};stroke-width:3' />", x1, y1, x2, y2, colors[snapshot.BranchLineId % colors.Count]));
						} else {
							file.WriteLine(string.Format("<path d='M{0} {1} C {4} {5}, {4} {5}, {2} {3}' stroke='{6}' fill='transparent'/>",
															x1, y1, x2, y2, x3, y3, colors[p.BranchLineId % colors.Count]));
						}
					}


					file.WriteLine(string.Format("<image x='{0}' y='{1}' width='20' height='20' xlink:href='https://www.gravatar.com/avatar/{3}?d=identicon&s=20' />", 10 + 50 * snapshot.TreeOffset - 10, 50 * i - 10, color, CalculateMD5Hash(snapshot.Commit.Email)));
					file.WriteLine(string.Format("<circle cx='{0}' cy='{1}' r='12' stroke='white' stroke-width='5' fill='transparent' />", 10 + 50 * snapshot.TreeOffset, 50 * i, color));
					file.WriteLine(string.Format("<circle cx='{0}' cy='{1}' r='10' stroke='{2}' stroke-width='2' fill='transparent' />", 10 + 50 * snapshot.TreeOffset, 50 * i, color));

					i++;
				}

				file.WriteLine(@"</svg>
					<style>
						line {
							filter: drop-shadow( 0 2px 1px black );
						}

						line:hover {
							#filter: drop-shadow( 0 2px 1px black );
						}
					</style>
				</body></html>");
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
			if (line.State == LineState.Unchanged) {
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
	}
}
