using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView.Model {

	/// <summary>
	/// Helper model to store file changes
	/// </summary>
	public class FileChanges {
		public string Path { get; set; }
		public string OldPath { get; set; }
		public bool IsRenamed { get; set; }
	}
}
