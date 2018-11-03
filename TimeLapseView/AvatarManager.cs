using LibGit2Sharp;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TimeLapseView.Model;

namespace TimeLapseView {
	public class AvatarManager {
		private Dictionary<string, string> Cache { get; set; }
		public readonly bool isGitHubRepository;

		public AvatarManager(string file) {
			Cache = new Dictionary<string, string>();
			var fileInfo = new FileInfo(file);

			// Find repository path
			var dir = fileInfo.Directory;
			while (dir != null && !Repository.IsValid(dir.FullName)) {
				dir = dir.Parent;
			}

			// Do nothing if no repository found
			if (dir == null) return;

			// Check is it github repository
			using (var repo = new Repository(dir.FullName)) {
				if (repo.Network.Remotes.Any()) {
					var remote = repo.Network.Remotes.First();
					isGitHubRepository = remote.Url.Contains("github.com");
				}
			}
		}

		public string GetAvatar(string name, string email) {
			var cacheKey = name + "_" + email;
			if (Cache.Keys.Contains(cacheKey)) {
				return Cache[cacheKey];
			}

			return Cache[cacheKey] = isGitHubRepository ? GetGitHubAvatar(name, email) : GetGravatar(email);
		}

		private string GetGitHubAvatar(string name, string email) {
			var client = new RestClient("https://api.github.com/search/users?q=" + email);
			var request = new RestRequest("", Method.GET);
			var response = client.Execute<GitHubUserSearchResult>(request);
			if (response.ResponseStatus == ResponseStatus.Completed && response.Data.total_count > 0) {
				return response.Data.items[0].avatar_url;
			}

			client = new RestClient("https://api.github.com/search/users?q=" + name);
			request = new RestRequest("", Method.GET);
			response = client.Execute<GitHubUserSearchResult>(request);
			if (response.ResponseStatus == ResponseStatus.Completed && response.Data.total_count > 0) {
				return response.Data.items[0].avatar_url;
			}

			// Icon not found. Return default gravatar icon
			return GetGravatar(email);
		}

		private string GetGravatar(string email) {
			return $@"https://www.gravatar.com/avatar/{CalculateMD5Hash(email)}?d=identicon";
		}


		/// <summary>
		/// Get string MD5 hash
		/// </summary>
		private string CalculateMD5Hash(string input) {
			// step 1, calculate MD5 hash from input
			MD5 md5 = System.Security.Cryptography.MD5.Create();
			byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
			byte[] hash = md5.ComputeHash(inputBytes);

			// step 2, convert byte array to hex string
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < hash.Length; i++) {
				sb.Append(hash[i].ToString("X2"));
			}

			return sb.ToString().ToLowerInvariant();
		}
	}
}
