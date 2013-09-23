﻿using C5.intervals;
using C5.Tests.intervals;

namespace C5.Performance.Wpf.Benchmarks
{
    class LCListEnumeratorSorted : Benchmarkable
    {
        private LayeredContainmentList2<int> _lclist;

        public override string BenchMarkName()
        {
            return "LCList Enumerable Sorted";
        }

        public override void CollectionSetup()
        {
            _lclist = new LayeredContainmentList2<int>(BenchmarkTestCases.DataSetA(CollectionSize));
        }

        public override void Setup()
        {
        }

        public override double Call(int i)
        {
            var sum = 0.0;
            foreach (var interval in _lclist.Sorted)
                sum += interval.Low;
            return sum;
        }
    }
}