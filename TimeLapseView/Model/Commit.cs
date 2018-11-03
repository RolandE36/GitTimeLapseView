using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView.Model {
	public class Commit {
		public LibGit2Sharp.Commit GitCommit;

		public string Sha { get; set; }
		public string ShortSha {
			get {
				return string.Join("", Sha.Take(7));
			}
		}
		public string AuthorInitials {
			get {
				return string.Join("", Author.Split(' ').Take(2).Select(e => e[0]));
			}
		}
		public string Author { get; set; }
		public string Email { get; set; }
		public string Description { get; set; }
		public string DescriptionShort { get; set; }
		public DateTimeOffset Date { get; set; }
		public HashSet<string> Parents { get; }
		public Dictionary<int, int> Base { get; set; }
		public string DateString {
			get {
				return Date.ToLocalTime().ToString(Constants.DATE_TIME_FORMAT);
			}
		}

		public Commit(LibGit2Sharp.Commit commit) {
			GitCommit = commit;
			Sha = string.Join("", commit.Sha);
			Author = commit.Author.Name;
			Email = commit.Author.Email;
			Description = commit.Message;
			DescriptionShort = commit.MessageShort.Replace("\n", " ");
			Date = commit.Author.When;
			Parents = new HashSet<string>();
			Base = new Dictionary<int, int>();
			foreach (var parent in commit.Parents) {
				Parents.Add(string.Join("", parent.Sha));
			}
		}
	}
}
