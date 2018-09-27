using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView.Model {
	/// <summary>
	/// Represent current commits analyzing progress
	/// </summary>
	public class CommitsAnalyzingStatus {
		public int ItemsPerPage { get; set; }
		public int ItemsProcessed { get; set; }
		public int ItemsTotal { get; set; }
		public bool IsSeekCompleted { get; set; }
	}
}
