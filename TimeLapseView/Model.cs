using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeLapseView {
	public class Snapshot {
		public string File;
		public List<CodeLine> Lines = new List<CodeLine>();
		public Commit Commit;
	}
	
	public class Commit {
		public string Author;
		public string Description;
		public DateTimeOffset Date;
	}

	public class CodeLine {
		public LineState State;
		public int ParentLineNumber;
		public int SequenceBegining;
	}

	public enum LineState {
		Modified,
		Inserted,
		Unchanged,
		Deleted
	}
}
