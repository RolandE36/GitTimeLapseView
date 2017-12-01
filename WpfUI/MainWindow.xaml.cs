﻿using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
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
using ICSharpCode.AvalonEdit.Document;

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

			manager = new FileHistoryManager(@"C:\git\ok-booking\OkBooking\BAL\ExchangeManager.cs");
			manager.GetCommitsHistory();
			slHistoy.Maximum = manager.Snapshots.Count;
			slHistoy.Value = manager.Snapshots.Count;
			slHistoy.Minimum = 1;
			tbCode.Text = manager.Snapshots[0].File;
		}

		private void slHistoyValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			int index = (int) (slHistoy.Maximum - slHistoy.Value);

			var ttt = new TimeLapseLineBackgroundRenderer(manager.Snapshots[index], (int)slHistoy.Value+1, manager.Snapshots.Count);
			tbCode.TextArea.TextView.BackgroundRenderers.Clear();// TODO:
			tbCode.TextArea.TextView.BackgroundRenderers.Add(ttt);
			tbCode.Text = manager.Snapshots[index].File;
			lblDetails.Content = manager.Snapshots[index].Commit.Description;
		}
	}
}
