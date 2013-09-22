﻿using System.Windows;

namespace C5.Performance.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            // Create the plotter with the benchmarks
            var bench = new SimpleBenchmark().GetBenchmark();
//            var bench = new IbsAddBenchmarker().GetBenchmark();
            var viewModel = new Plotter(bench);
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}