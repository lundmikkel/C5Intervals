﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using C5.Intervals;

namespace C5.intervals
{
    public class NestedContainmentListArticle<I, T> : CollectionValueBase<I>, IIntervalCollection<I, T>
        where I : class, IInterval<T>
        where T : IComparable<T>
    {
        #region Fields

        private readonly Node[] _list;
        private readonly Sublist[] _header;
        private readonly int _count;
        private readonly IInterval<T> _span;

        private int _maximumDepth = -1;
        private IInterval<T> _intervalOfMaximumDepth;

        #endregion

        #region Code Contracts

        [ContractInvariantMethod]
        private void invariant()
        {
            // TODO
        }

        #endregion

        #region Inner Classes

        private struct Node
        {
            public readonly I Interval;
            public int Sublist;

            public Node(I interval)
                : this()
            {
                Interval = interval;
                Sublist = 0;
            }

            public bool HasSublist { get { return Sublist >= 0; } }

            public override string ToString()
            {
                return String.Format("{0} / {1}", Interval, Sublist);
            }
        }

        private struct Sublist
        {
            public int Start;
            public int Length;

            public int End
            {
                get { return Start + Length; }
            }

            public Sublist(int start, int length)
                : this()
            {
                Start = start;
                Length = length;
            }

            public override string ToString()
            {
                return String.Format("{0} / {1}", Start, Length);
            }
        }

        #endregion

        #region Constructors

        public NestedContainmentListArticle(IEnumerable<I> intervals)
        {
            var intervalArray = intervals as I[] ?? intervals.ToArray();

            if (!intervalArray.Any())
                return;

            _count = intervalArray.Length;

            // Sort
            Sorting.HeapSort(intervalArray, 0, _count, IntervalExtensions.CreateComparer<I, T>());

            // Create list with intervals
            _list = new Node[_count];
            for (var i = 0; i < _count; i++)
                _list[i] = new Node(intervalArray[i]);

            // Count sublists
            var sublistCount = initSublists(ref intervalArray);

            _header = new Sublist[sublistCount];

            labelSublists();
            computeSubStart(sublistCount);
            computeAbsolutePosition(sublistCount);

            // Sort sublists
            var sublistComparer = ComparerFactory<Node>.CreateComparer((i, j) =>
            {
                var compareTo = i.Sublist.CompareTo(j.Sublist);
                return compareTo != 0 ? compareTo : i.Interval.CompareTo(j.Interval);
            });
            Sorting.HeapSort(_list, 0, _count, sublistComparer);

            sublistInvert(sublistCount);

            _span = new IntervalBase<T>(_list[0].Interval, _list[_header[0].Length - 1].Interval);
        }

        /// <summary>
        /// Returns the number of sublists in the data structure
        /// </summary>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The number of sublists</returns>
        private int initSublists(ref I[] intervals)
        {
            var n = 1;

            for (var i = 1; i < _count; ++i)
                if (intervals[i - 1].Contains(intervals[i]))
                    ++n;

            return n;
        }

        /// <summary>
        /// The function stores in _list an interval and the id of the sublist it belongs to,
        /// and in _header the id of the sublist's parent and its length.
        /// 
        /// The algorithm works by taking the last interval and check if it contains the next interval.
        /// If not, it works its way from sublist to sublist down to the main list, looking for the right sublist.
        /// When found it sets the intervals sublist and moves on to the next interval.
        /// </summary>
        private void labelSublists()
        {
            // Initialize the parent index to be the first interval
            var parent = 0;
            // Initialize interval index to be the second interval
            var i = 1;

            // The first list (the main list) has no parent and initially contains one interval (the first)
            _header[0] = new Sublist(-1, 1);

            // The list id of the list owned by the parent
            var parentList = 1;

            // The initial number of lists is just one (the main list)
            var listCount = 1;

            // Iterate through all intervals
            while (i < _count)
            {
                // Check if we have found the right sublist of the current interval
                // (Are in the main list, or does the parent interval contains the current interval?)
                if (parentList == 0 || _list[parent].Interval.StrictlyContains(_list[i].Interval))
                {
                    // Check if the interval is the first in the list
                    if (_header[parentList].Length == 0)
                    {
                        // Increment list count
                        listCount++;
                        // Set its parent in the header
                        _header[parentList].Start = parent;
                    }

                    // Increment the length of the parent's list
                    _header[parentList].Length++;

                    // Update which sublist the ith interval belongs to
                    _list[i].Sublist = parentList;

                    // Set the new parent to be the current interval
                    parent = i;

                    // And update the parent's list to be the next list (which is equal to the list count)
                    parentList = listCount;

                    // Continue to the next interval
                    i++;
                }
                // Otherwise move down to the containing sublist
                else
                {
                    // Get the next parent's list
                    parentList = _list[parent].Sublist;

                    // Get the parent of the current list
                    parent = _header[parentList].Start;
                }
            }
        }

        private void computeSubStart(int sublistCount)
        {
            var total = 0;

            for (var i = 0; i < sublistCount; ++i)
            {
                var tmp = _header[i].Length;
                _header[i].Length = total;
                total += tmp;
            }
        }

        private void computeAbsolutePosition(int sublistCount)
        {
            var currentSublist = 1;
            for (var i = 0; i < _count; ++i)
            {
                var intervalSublist = _list[i].Sublist;

                if (currentSublist < sublistCount && i == _header[currentSublist].Start)
                {
                    _header[currentSublist].Start = _header[intervalSublist].Length;
                    ++currentSublist;
                }
                ++_header[intervalSublist].Length;
            }
        }

        private void sublistInvert(int sublistCount)
        {
            /*var isub = 0;
            for (var i = 0; i < _count; ++i)
            {
                if (_list[i].Sublist > isub)
                {
                    isub = _list[i].Sublist;
                    var parent = _header[isub].Start;
                    _list[parent].Sublist = isub;
                    _header[isub].Start = i;
                    _header[isub].Length = 0;
                }

                _header[isub].Length++;
                _list[i].Sublist = -1;
            }*/

            // Reset all sublists
            for (var i = 0; i < _count; ++i)
                _list[i].Sublist = -1;

            // Fix sublists in list
            for (var j = 1; j < sublistCount; ++j)
                _list[_header[j].Start].Sublist = j;

            // Fix start in header
            _header[0].Start = 0;
            for (var j = 1; j < sublistCount; ++j)
                _header[j].Start = _header[j - 1].Length;

            // Fix length in header
            for (var j = sublistCount - 1; j > 0; --j)
                _header[j].Length = _header[j].Length - _header[j - 1].Length;
        }

        #endregion

        #region Collection Value

        public override bool IsEmpty { get { return _count == 0; } }

        public override int Count { get { return _count; } }

        public override Speed CountSpeed { get { return Speed.Constant; } }

        public override I Choose()
        {
            if (IsEmpty)
                throw new NoSuchItemException();

            return _list[0].Interval;
        }

        #endregion

        #region Interval Collection

        #region Properties

        #region Data Structure Properties

        /// <inheritdoc/>
        public bool AllowsOverlaps { get { return true; } }

        /// <inheritdoc/>
        public bool AllowsReferenceDuplicates { get { return true; } }

        #endregion

        #region Collection Properties

        /// <inheritdoc/>
        public IInterval<T> Span { get { return _span; } }

        /// <inheritdoc/>
        public I LowestInterval { get { return _list[0].Interval; } }

        /// <inheritdoc/>
        public IEnumerable<I> LowestIntervals
        {
            get
            {
                if (IsEmpty)
                    yield break;

                var lowestInterval = _list[0].Interval;

                yield return lowestInterval;

                // Iterate through bottom layer as long as the intervals share a low
                for (var i = 1; i < _header[0].Length; ++i)
                {
                    if (_list[i].Interval.CompareLow(lowestInterval) == 0)
                        yield return _list[i].Interval;
                    else
                        yield break;
                }
            }
        }

        /// <inheritdoc/>
        public I HighestInterval { get { return _list[_header[0].Length - 1].Interval; } }

        /// <inheritdoc/>
        public IEnumerable<I> HighestIntervals
        {
            get
            {
                if (IsEmpty)
                    yield break;

                var highestInterval = _list[_header[0].Length - 1].Interval;

                yield return highestInterval;

                // Iterate through bottom layer as long as the intervals share a low
                for (var i = _header[0].Length - 2; i >= 0; i--)
                {
                    var interval = _list[i].Interval;
                    if (interval.CompareHigh(highestInterval) == 0)
                        yield return interval;
                    else
                        yield break;
                }
            }
        }

        /// <inheritdoc/>
        public int MaximumDepth
        {
            get
            {
                if (_maximumDepth < 0)
                    _maximumDepth = Sorted.MaximumDepth(ref _intervalOfMaximumDepth);

                return _maximumDepth;
            }
        }

        /// <summary>
        /// Get the interval in which the maximum depth is.
        /// </summary>
        public IInterval<T> IntervalOfMaximumDepth
        {
            get
            {
                // If the Maximum Depth is below 0, then the interval of maximum depth has not been set yet
                Contract.Assert(_maximumDepth >= 0 || _intervalOfMaximumDepth == null);

                if (_maximumDepth < 0)
                    _maximumDepth = Sorted.MaximumDepth(ref _intervalOfMaximumDepth);

                return _intervalOfMaximumDepth;
            }
        }

        #endregion

        #endregion

        #region Enumerable

        /// <inheritdoc/>
        public override IEnumerator<I> GetEnumerator()
        {
            if (IsEmpty)
                return Enumerable.Empty<I>().GetEnumerator();

            return _list.Select(item => item.Interval).GetEnumerator();
        }

        /// <inheritdoc/>
        public IEnumerable<I> Sorted {
            get
            {
                if (IsEmpty)
                    return Enumerable.Empty<I>();

                return sorted(_header[0]);
            } 
        }

        private IEnumerable<I> sorted(Sublist sublist)
        {
            int min = sublist.Start,
                max = sublist.Start + sublist.Length;

            for (var i = min; i < max; ++i)
            {
                yield return _list[i].Interval;

                if (_list[i].Sublist >= 0)
                    foreach (var interval in sorted(_header[_list[i].Sublist]))
                        yield return interval;
            }
        }

        #endregion

        #region Find Overlaps

        /// <inheritdoc/>
        public IEnumerable<I> FindOverlaps(T query)
        {
            if (IsEmpty)
                return Enumerable.Empty<I>();

            return findOverlaps(new IntervalBase<T>(query), _header[0]);
        }

        /// <inheritdoc/>
        public IEnumerable<I> FindOverlaps(IInterval<T> query)
        {
            if (IsEmpty)
                return Enumerable.Empty<I>();

            return findOverlaps(query, _header[0]);
        }

        private IEnumerable<I> findOverlaps(IInterval<T> query, Sublist sublist)
        {
            if (IsEmpty || sublist.Length == 0)
                yield break;

            // Find first overlapping interval
            var first = findFirst(sublist, query);

            // If index is out of bound, or interval doesn't overlap, we can just stop our search
            if (first < sublist.Start || sublist.End - 1 < first || !_list[first].Interval.Overlaps(query))
                yield break;

            Node node;

            while (first < sublist.End && (node = _list[first++]).Interval.CompareLowHigh(query) <= 0)
            {
                yield return node.Interval;

                if (node.HasSublist)
                    // If the interval is contained in the query, all intervals in the sublist must overlap the query
                    foreach (var interval in findOverlaps(query, _header[node.Sublist]))
                        yield return interval;
            }
        }

        private int findFirst(Sublist sublist, IInterval<T> query)
        {
            int min = sublist.Start - 1,
                max = sublist.Start + sublist.Length;

            while (min + 1 < max)
            {
                var middle = min + ((max - min) >> 1);

                if (query.CompareLowHigh(_list[middle].Interval) <= 0)
                    max = middle;
                else
                    min = middle;
            }

            return max;
        }

        #endregion

        #region Find Overlap

        /// <inheritdoc/>
        public bool FindOverlap(T query, out I overlap)
        {
            return FindOverlap(new IntervalBase<T>(query), out overlap);
        }

        /// <inheritdoc/>
        public bool FindOverlap(IInterval<T> query, out I overlap)
        {
            overlap = null;

            // Check if query overlaps the collection at all
            if (_list == null || !query.Overlaps(Span))
                return false;

            // Find first overlap
            var i = findFirst(_header[0], query);

            // Check if index is in bound and if the interval overlaps the query
            var result = 0 <= i && i < _header[0].Length && _list[i].Interval.Overlaps(query);

            if (result)
                overlap = _list[i].Interval;

            return result;
        }

        #endregion

        #region Count Overlaps

        /// <inheritdoc/>
        public int CountOverlaps(T query)
        {
            return FindOverlaps(query).Count();
        }

        /// <inheritdoc/>
        public int CountOverlaps(IInterval<T> query)
        {
            return FindOverlaps(query).Count();
        }

        #endregion

        #region Gaps

        /// <inheritdoc/>
        public IEnumerable<IInterval<T>> Gaps
        {
            get
            {
                if (IsEmpty)
                    return Enumerable.Empty<IInterval<T>>();

                return _list.Take(_header[0].Length).Select(x => x.Interval).Gaps();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IInterval<T>> FindGaps(IInterval<T> query)
        {
            if (IsEmpty)
                yield break;

            // We know first doesn't overlap so we can increment it before searching
            var first = findFirst(_header[0], query);

            // Cache variables to speed up iteration
            var last = _header[0].Length;
            I interval;

            while (first < last && (interval = _list[first++].Interval).CompareLowHigh(query) <= 0)
                yield return interval;
        }

        #endregion

        #region Extensible

        /// <inheritdoc/>
        public bool IsReadOnly { get { return true; } }

        /// <inheritdoc/>
        public bool Add(I interval)
        {
            throw new ReadOnlyCollectionException();
        }

        /// <inheritdoc/>
        public void AddAll(IEnumerable<I> intervals)
        {
            throw new ReadOnlyCollectionException();
        }

        /// <inheritdoc/>
        public bool Remove(I interval)
        {
            throw new ReadOnlyCollectionException();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            throw new ReadOnlyCollectionException();
        }

        #endregion

        #endregion
    }
}