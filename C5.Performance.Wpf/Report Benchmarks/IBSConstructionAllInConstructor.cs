﻿using C5.intervals;
using C5.Performance.Wpf.Benchmarks;
using C5.Tests.intervals;

namespace C5.Performance.Wpf.Report_Benchmarks
{
    public class IBSConstructionAllInConstructor : Benchmarkable
    {
        private IInterval<int>[] _intervals;
        private IntervalBinarySearchTreeAvl<IInterval<int>, int> _intervalCollection; 

        private int intervalSearch(int intervalId)
        {
            _intervalCollection = new IntervalBinarySearchTreeAvl<IInterval<int>, int>(_intervals);
            return 1;
        }

        public override void CollectionSetup()
        {
            _intervals = BenchmarkTestCases.DataSetA(CollectionSize);
            ItemsArray = SearchAndSort.FillIntArray(CollectionSize);
        }

        public override void Setup()
        {
        }

        public override double Call(int i)
        {
            return intervalSearch(i);
        }

        public override string BenchMarkName()
        {
            return "IBS Construct Add All In Constructor";
        }
    }
}