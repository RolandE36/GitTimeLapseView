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
		private string filePath;

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
				// Get all commits with file.
				// Target.Id not equal to parent one mean that file was updated.
				var commits = repo.Commits.Where(c => c.Parents.Count() >= 1 && c.Tree[filePath] != null &&
												(c.Parents.First().Tree[filePath] == null ||
													c.Tree[filePath].Target.Id != c.Parents.First().Tree[filePath].Target.Id));

				foreach (var commit in commits) {
					// Get file text from commit
					var blob = (Blob) commit[filePath].Target;
					// TODO: probably use commit.Encoding
					using (var reader = new StreamReader(blob.GetContentStream(), Encoding.UTF8)) {
						//FileHistory.Add(reader.ReadToEnd());
						//Commits.Add(commit);
						snapshots.Add(new Snapshot() {
							File = reader.ReadToEnd(),
							Commit = new Commit() {
								Sha = string.Join("", commit.Sha.Take(8)),
								Author = commit.Author.Name,
								Description = commit.Message,
								DescriptionShort = commit.MessageShort.Replace("\n", " "),
								Date = commit.Author.When
							}
						});

						var count = snapshots.Count - 1;
						if (count > 0) {
							var diff = fileComparer.BuildDiffModel(snapshots[count].File, snapshots[count - 1].File);
							int parentLineNumber = -1;
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
	}
}
