using ICSharpCode.AvalonEdit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfUI {
	/// <summary>
	/// Interaction logic for GoToLineWindow.xaml
	/// </summary>
	public partial class GoToLineWindow : Window {

		private MainWindow MainWindow;

		public GoToLineWindow(MainWindow mainWindow) {
			MainWindow = mainWindow;
			InitializeComponent();
			tbLineNumber.Focus();
		}

		private TextEditor GetSelectedTextEditor() {
			return MainWindow.GetActiveTextEditor();
		}

		private void btnGoToLineClick(object sender, RoutedEventArgs e) {
			GoToLine();
		}

		private void tbSearchText_KeyUp(object sender, KeyEventArgs e) {
			if (e.Key != Key.Enter) return;
			GoToLine();
		}

		private void GoToLine() {
			var textEditor = GetSelectedTextEditor();
			var line = -1;
			if (!int.TryParse(tbLineNumber.Text, out line)) return;
			if (line < 0) return;
			textEditor.ScrollTo(line, 0);
		}

		private void Window_KeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape) this.Close();
		}
	}
}
