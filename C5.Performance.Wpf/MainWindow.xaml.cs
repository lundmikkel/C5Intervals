﻿using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using C5.Performance.Wpf.Benchmarks;

namespace C5.Performance.Wpf
{
    public partial class MainWindow
    {
        // Parameters for running the benchmarks
        private const int MinCollectionSize = 100;
        private const int MaxCollectionSize = 50000;
        private const int CollectionMultiplier = 2;
        private const int MaxCount = Int32.MaxValue/100000;
        private const int Repeats = 10;
        private const double MaxExecutionTimeInSeconds = 0.25;
        // Path of the exported pdf file containing the benchmark
        private const String PdfPath = "pdfplot.pdf";
        private readonly Plotter _plotter;
        // Every time we benchmark we count this up in order to get a new color for every benchmark
        private int _lineSeriesIndex;

        public MainWindow()
        {
            _plotter = Plotter.createPlotter();
            DataContext = _plotter;
            InitializeComponent();
        }

        private void button1_Click_1(object sender, RoutedEventArgs e)
        {
            var b = new SimpleBenchmark();
            var b2 = new IbsAvlAddBenchmarker();
            var b3 = new IbsAddBenchmarker();
            var thread = new Thread(() => RunBenchmarks(b));
            thread.Start();
        }

        private void RunBenchmarks(params Benchmarkable[] benchmarks)
        {
            foreach (var b in benchmarks)
            {
                _plotter.addAreaSeries(b.BenchMarkName());
                for (b.CollectionSize = MinCollectionSize;
                    b.CollectionSize < MaxCollectionSize;
                    b.CollectionSize *= CollectionMultiplier)
                {
                    UpdateStatusLabel("Running " + b.BenchMarkName() + " with collection size " + b.CollectionSize);
                    var benchmark = b.Benchmark(MaxCount, Repeats, MaxExecutionTimeInSeconds, this);
                    Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                        _plotter.addDataPoint(_lineSeriesIndex, benchmark)));
                    Thread.Sleep(100);
                }
                _lineSeriesIndex++;
            }
            UpdateRunningLabel("");
            UpdateStatusLabel("Finished");
            Thread.Sleep(1000);
            UpdateStatusLabel("");
            _plotter.exportPdf(PdfPath);
        }

        private void UpdateStatusLabel(String s)
        {
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => StatusLabel.Content = s));
        }

        public void UpdateRunningLabel(String s)
        {
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => RunningLabel.Content = s));
        }
    }
}