﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace C5.Intervals
{
    using SCG = System.Collections.Generic;

    public class EndpointSortedIntervalCollection<I, T> : ContainmentFreeIntervalCollectionBase<I, T>
        where I : class, IInterval<T>
        where T : IComparable<T>
    {
        #region Fields

        private readonly List<I> _intervals;
        private readonly bool _isReadOnly;

        private static readonly IComparer<I> Comparer = IntervalExtensions.CreateComparer<I, T>();

        #endregion

        #region Code Contracts

        [ContractInvariantMethod]
        private void invariants()
        {
            // Intervals are sorted
            Contract.Invariant(_intervals.IsSorted(IntervalExtensions.CreateComparer<I, T>()));
            // Highs are sorted as well
            Contract.Invariant(_intervals.IsSorted<I>(IntervalExtensions.CreateHighComparer<I, T>()));
        }

        #endregion

        #region Constructors

        public EndpointSortedIntervalCollection(bool isReadOnly = false)
        {
            _isReadOnly = isReadOnly;
        }

        public EndpointSortedIntervalCollection(IEnumerable<I> intervals, bool isReadOnly = false) : this(isReadOnly)
        {
            // TODO: Find a better solution
            var list = new List<I>();
            foreach (var interval in intervals)
                if (!interval.OverlapsAny(list))
                    list.Add(interval);

            var array = list.ToArray();
            Sorting.Timsort(array, 0, list.Count, Comparer);

            _intervals = new List<I>(array);
        }
        #endregion

        #region Collection Value

        /// <inheritdoc/>
        public override int Count
        {
            get { return _intervals.Count; }
        }

        #endregion

        #region Interval Colletion

        #region Data Structure Properties

        /// <inheritdoc/>
        public override bool AllowsOverlaps
        {
            get { return false; }
        }

        /// <inheritdoc/>
        public override bool IsReadOnly
        {
            get { return _isReadOnly; }
        }

        /// <inheritdoc/>
        public override Speed IndexingSpeed
        {
            get { return Speed.Constant; }
        }
        
        #endregion

        #region Sorted Enumeration

        /// <inheritdoc/>
        public override IEnumerable<I> Sorted
        {
            get
            {
                foreach (var interval in _intervals)
                    yield return interval;
            }
        }

        /// <inheritdoc/>
        public override IEnumerable<I> SortedBackwards()
        {
            return IsEmpty ? Enumerable.Empty<I>() : enumerateBackwardsFromIndex(Count - 1);
        }

        /// <inheritdoc/>
        public override IEnumerable<I> EnumerateFrom(T point, bool includeOverlaps = true)
        {
            var query = new IntervalBase<T>(point);
            var index = findFirst(query);

            if (Count <= index)
                return Enumerable.Empty<I>();

            return !includeOverlaps && _intervals[index].Overlaps(point)
                ? EnumerateFromIndex(index + 1)
                : EnumerateFromIndex(index);
        }

        /// <inheritdoc/>
        public override IEnumerable<I> EnumerateBackwardsFrom(T point, bool includeOverlaps = true)
        {
            if (IsEmpty)
                return Enumerable.Empty<I>();

            var query = new IntervalBase<T>(point);
            var index = findLast(query) - 1;

            if (index < 0)
                return Enumerable.Empty<I>();

            return !includeOverlaps && _intervals[index].Overlaps(point)
                ? EnumerateBackwardsFromIndex(index - 1)
                : EnumerateBackwardsFromIndex(index);
        }

        /// <inheritdoc/>
        public override IEnumerable<I> EnumerateFrom(I interval, bool includeInterval = true)
        {
            var index = _intervals.BinarySearch(interval, Comparer);
            if (index < 0)
                return Enumerable.Empty<I>();
            return includeInterval ? EnumerateFromIndex(index) : EnumerateFromIndex(index + 1);
        }

        /// <inheritdoc/>
        public override IEnumerable<I> EnumerateBackwardsFrom(I interval, bool includeInterval = true)
        {
            var index = _intervals.BinarySearch(interval, Comparer);
            return includeInterval ? EnumerateBackwardsFromIndex(index) : EnumerateBackwardsFromIndex(index - 1);
        }

        /// <inheritdoc/>
        public override IEnumerable<I> EnumerateFromIndex(int index)
        {
            if (Count <= index)
                return Enumerable.Empty<I>();
            if (index < 0)
                return Sorted;

            return enumerateFromIndex(index);
        }

        /// <inheritdoc/>
        public override IEnumerable<I> EnumerateBackwardsFromIndex(int index)
        {
            if (index < 0 || IsEmpty)
                return Enumerable.Empty<I>();
            if (Count <= index)
                return SortedBackwards();

            return enumerateBackwardsFromIndex(index);
        }

        private IEnumerable<I> enumerateFromIndex(int index)
        {
            Contract.Requires(0 <= index && index < Count);

            var count = Count;
            while (index < count)
                yield return _intervals[index++];
        }

        private IEnumerable<I> enumerateBackwardsFromIndex(int index)
        {
            Contract.Requires(0 <= index && index < Count);

            while (index >= 0)
                yield return _intervals[index--];
        }
        
        #endregion

        #region Indexed Access

        /// <inheritdoc/>
        public override I this[int i]
        {
            get { return _intervals[i]; }
        }

        #endregion

        #region Find Overlaps

        /// <inheritdoc/>
        public override IEnumerable<I> FindOverlaps(IInterval<T> query)
        {
            // Break if we won't find any overlaps
            if (IsEmpty)
                yield break;

            // We know first doesn't overlap so we can increment it before searching
            var first = findFirst(query);

            // If index is out of bound, or found interval doesn't overlap, then the list won't contain any overlaps
            if (Count <= first || !_intervals[first].Overlaps(query))
                yield break;

            // We can use first as lower to minimize search area
            var last = findLast(query);

            while (first < last)
                yield return _intervals[first++];
        }

        private int findFirst(IInterval<T> query)
        {
            Contract.Requires(query != null);

            // Either the interval at index result overlaps or no intervals in the layer overlap
            Contract.Ensures(Contract.Result<int>() < 0 || Count <= Contract.Result<int>() || _intervals[Contract.Result<int>()].Overlaps(query) || Contract.ForAll(0, Count, i => !_intervals[i].Overlaps(query)));
            // All intervals before index result do not overlap the query
            Contract.Ensures(Contract.ForAll(0, Contract.Result<int>(), i => !_intervals[i].Overlaps(query)));

            int min = -1, max = Count;

            while (min + 1 < max)
            {
                var middle = min + ((max - min) >> 1);
                if (query.CompareLowHigh(_intervals[middle]) <= 0)
                    max = middle;
                else
                    min = middle;
            }

            return max;
        }

        private int findLast(IInterval<T> query)
        {
            Contract.Requires(query != null);

            // Either the interval at index result overlaps or no intervals in the layer overlap
            Contract.Ensures(Contract.Result<int>() == 0 || _intervals[Contract.Result<int>() - 1].Overlaps(query) || Contract.ForAll(_intervals, x => !x.Overlaps(query)));
            // All intervals after index result do not overlap the query
            Contract.Ensures(Contract.ForAll(Contract.Result<int>(), Count, i => !_intervals[i].Overlaps(query)));

            int min = -1, max = Count;

            while (min + 1 < max)
            {
                var middle = min + ((max - min) >> 1);
                if (_intervals[middle].CompareLowHigh(query) <= 0)
                    min = middle;
                else
                    max = middle;
            }

            return max;
        }
        
        #endregion

        #endregion
    }
}
