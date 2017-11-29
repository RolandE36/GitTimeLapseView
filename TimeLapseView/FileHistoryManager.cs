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
		public List<Snapshot> Snapshots = new List<Snapshot>();

		private string repositoryPath;
		private string filePath;

		public FileHistoryManager(string file) {
			var fileInfo = new FileInfo(file);
			if (!fileInfo.Exists) throw new FileNotFoundException($"File '{file}' not found.");

			// Find repository
			var dir = fileInfo.Directory;
			while (dir != null && !Repository.IsValid(dir.FullName)) {
				dir = dir.Parent;
			}

			if (dir != null) {
				repositoryPath = dir.FullName;
				filePath = file.Replace(repositoryPath + "\\", "");
			}
		}

		public void GetCommitsHistory() {
			var diffBuilder = new InlineDiffBuilder(new Differ()); // TODO: probably static

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
						Snapshots.Add(new Snapshot() {
							File = reader.ReadToEnd(),
							Commit = new Commit() {
								Author = commit.Author.Name,
								Description = commit.Message,
								Date = commit.Author.When
							}
						});

						var count = Snapshots.Count - 1;
						if (count > 1) {
							var diff = diffBuilder.BuildDiffModel(Snapshots[count].File, Snapshots[count - 1].File);
							foreach (var line in diff.Lines.Where(l => l.Type != ChangeType.Deleted)) {
								var diffLine = new CodeLine();
								switch (line.Type) {
									case ChangeType.Modified:
										diffLine.State = LineState.Modified;
										break;
									case ChangeType.Inserted:
										diffLine.State = LineState.Inserted;
										break;
									default:
										diffLine.State = LineState.Unchanged;
										break;
								}

								Snapshots[count - 1].Lines.Add(diffLine);
							}
						}
					}
				}
			}
		}
	}
}
