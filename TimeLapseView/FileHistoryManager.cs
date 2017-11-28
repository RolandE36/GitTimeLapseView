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

		// Temp variable for storing file history
		public List<string> FileHistory = new List<string>();
		public List<DiffPaneModel> FileHistoryDiff = new List<DiffPaneModel>();
		public List<Commit> Commits = new List<Commit>();

		public void GetCommitsHistory() {
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
						FileHistory.Add(reader.ReadToEnd());
						Commits.Add(commit);
					}
				}

				// Compare code
				var diffBuilder = new InlineDiffBuilder(new Differ());
				FileHistoryDiff.Add(diffBuilder.BuildDiffModel("", FileHistory[0]));
				for (int i = FileHistory.Count-1; i >= 1; i--) {
					FileHistoryDiff.Add(diffBuilder.BuildDiffModel(FileHistory[i], FileHistory[i-1]));
				}
				FileHistoryDiff.Reverse();
			}
		}
	}
}
