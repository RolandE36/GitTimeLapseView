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
using System.Windows.Navigation;
using System.Windows.Shapes;
using TimeLapseView;

namespace WpfUI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		public MainWindow() {
			InitializeComponent();
		}

		FileHistoryManager manager;

		protected override void OnInitialized(EventArgs e) {
			base.OnInitialized(e);

			manager = new FileHistoryManager(@"");
			manager.GetCommitsHistory();
			slHistoy.Maximum = manager.FileHistory.Count;
			slHistoy.Value = manager.FileHistory.Count;
			slHistoy.Minimum = 1;
			tbCode.Text = manager.FileHistory[(int)slHistoy.Value - 1];
		}

		private void slHistoyValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			tbCode.Text = manager.FileHistory[(int)slHistoy.Value - 1];
		}
	}
}
