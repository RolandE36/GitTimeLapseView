using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfUI.Renderer {
	/// <summary>
	/// Line background highlighting mode
	/// </summary>
	public enum RendererMode {
		/// <summary>
		/// Background color based on commit number
		/// </summary>
		TimeLapse,

		/// <summary>
		/// Differences between current and previous commit
		/// </summary>
		IncrementalDiff
	}
}
