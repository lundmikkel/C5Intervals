﻿using C5.intervals;

namespace C5.Performance.Wpf.Benchmarks
{
    public class IbsAvlIntervalSetsBenchmarker : Benchmarkable
    {
        private IntervalBinarySearchTreeAvl<IInterval<int>, int> _collection;
        private IInterval<int>[] _intervals;

        public override void CollectionSetup()
        {
            _intervals = Tests.intervals.BenchmarkTestCases.DataSetA(CollectionSize);
            _collection = new IntervalBinarySearchTreeAvl<IInterval<int>, int>();
            ItemsArray = SearchAndSort.FillIntArray(CollectionSize);
            SearchAndSort.Shuffle(ItemsArray);
        }

        public override void Setup()
        {
            _collection.Clear();
        }

        public override double Call(int i)
        {
            foreach (var interval in _intervals)
                _collection.Add(interval);
            return _collection.Count;
        }

        public override string BenchMarkName()
        {
            return "IBS Interval Set New";
        }
    }

    public class IbsAvlIntervalSetsPrebuildBenchmarker : Benchmarkable
    {
        private IntervalBinarySearchTreeAvl<IInterval<int>, int> _collection;
        private IInterval<int>[] _intervals;

        public override void CollectionSetup()
        {
            _intervals = Tests.intervals.BenchmarkTestCases.DataSetA(CollectionSize);
            ItemsArray = SearchAndSort.FillIntArray(CollectionSize);
            SearchAndSort.Shuffle(ItemsArray);
        }

        public override void Setup()
        {
        }

        public override double Call(int i)
        {
            _collection = new IntervalBinarySearchTreeAvl<IInterval<int>, int>(_intervals);
            return _collection.Count;
        }

        public override string BenchMarkName()
        {
            return "IBS Interval Set New - PreBuild";
        }
    }
}