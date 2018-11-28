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
	/// Interaction logic for SearchWindow.xaml
	/// </summary>
	public partial class SearchWindow : Window {
		private MainWindow MainWindow;

		public SearchWindow(MainWindow mainWindow) {
			MainWindow = mainWindow;
			InitializeComponent();
			tbSearchText.Focus();
			rbCurrent.IsChecked = true;
		}

		private TextEditor GetSelectedTextEditor() {
			return rbCurrent.IsChecked.Value ? MainWindow.tbCodeA : MainWindow.tbCodeB;
		}

		private void btnNextClick(object sender, RoutedEventArgs e) {
			var textEditor = GetSelectedTextEditor();
			var index = MainWindow.tbCodeA.Text.IndexOf(tbSearchText.Text, textEditor.CaretOffset, StringComparison.OrdinalIgnoreCase);
			SelectSearch(index);
		}

		private void btnPrewClick(object sender, RoutedEventArgs e) {
			var textEditor = GetSelectedTextEditor();
			var offset = textEditor.CaretOffset - tbSearchText.Text.Length;
			var index = -1;
			if (offset > 0) index = MainWindow.tbCodeA.Text.LastIndexOf(tbSearchText.Text, textEditor.CaretOffset - tbSearchText.Text.Length, StringComparison.OrdinalIgnoreCase);
			SelectSearch(index);
		}

		private void tbSearchText_KeyUp(object sender, KeyEventArgs e) {
			if (e.Key != Key.Enter) return;
			var textEditor = GetSelectedTextEditor();
			var index = textEditor.Text.IndexOf(tbSearchText.Text, textEditor.CaretOffset, StringComparison.OrdinalIgnoreCase);
			SelectSearch(index);
		}

		private void SelectSearch(int index) {
			if (index == -1) {
				MessageBox.Show("Nothing found.");
				return;
			}

			var textEditor = GetSelectedTextEditor();
			textEditor.CaretOffset = index;
			textEditor.ScrollTo(textEditor.TextArea.Caret.Line, 0);
			textEditor.Select(index, tbSearchText.Text.Length);
		}
	}
}
