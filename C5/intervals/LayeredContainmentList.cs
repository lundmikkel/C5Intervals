﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace C5.intervals
{
    /// <summary>
    /// An implementation of the Layered Containment List by Mikkel Riise Lund
    /// </summary>
    /// <typeparam name="I">The interval type.</typeparam>
    /// <typeparam name="T">The interval endpoint type.</typeparam>
    public class LayeredContainmentList<I, T> : CollectionValueBase<I>, IIntervalCollection<I, T>
        where I : IInterval<T>
        where T : IComparable<T>
    {
        private readonly int _count;
        private readonly Node[][] _layers;
        private readonly int[] _counts;
        private readonly IInterval<T> _span;

        #region Node nested classes

        struct Node
        {
            internal I Interval { get; private set; }
            internal int Pointer { get; private set; }

            internal Node(I interval, int pointer)
                : this()
            {
                Interval = interval;
                Pointer = pointer;
            }

            internal Node(int pointer)
                : this()
            {
                Pointer = pointer;
            }

            public override string ToString()
            {
                return Interval != null ? Interval.ToString() : "*";
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Create a Layered Containment List with a collection of intervals.
        /// </summary>
        /// <param name="intervals">The collection of intervals.</param>
        public LayeredContainmentList(IEnumerable<I> intervals)
        {
            // Make intervals to array to allow fast sorting and counting
            var intervalArray = intervals as I[] ?? intervals.ToArray();

            // Only do the work if we have something to work with
            if (intervalArray.Any())
            {
                // Count intervals so we can use it later on
                _count = intervalArray.Length;

                // Sort intervals
                var comparer = ComparerFactory<I>.CreateComparer((x, y) => x.CompareTo(y));
                Sorting.IntroSort(intervalArray, 0, _count, comparer);

                // Put intervals in the arrays
                var layers = createLayers(intervalArray);

                // Create the list that contains the containment layers
                _layers = new Node[layers.Count][];
                _counts = new int[layers.Count];
                // Create each containment layer
                for (var i = 0; i < _counts.Length - 1; i++)
                {
                    _layers[i] = layers[i].ToArray();
                    _counts[i] = layers[i].Count - 1; // Subtract one for the dummy node
                }

                // Save the span once
                _span = new IntervalBase<T>(_layers[0][0].Interval, _layers[0][_counts[0] - 1].Interval);
            }
        }

        private ArrayList<ArrayList<Node>> createLayers(I[] intervals)
        {
            // Use a stack to keep track of current containment
            var layer = 0;
            var layers = new ArrayList<ArrayList<Node>> { new ArrayList<Node>() };

            foreach (var interval in intervals)
            {
                // Track containment
                while (layer > 0)
                {
                    // If the interval is contained in the top of the stack, leave it...
                    if (layers[layer - 1].Last.Interval.StrictlyContains(interval))
                        break;

                    layer--;
                }

                // Check if interval will be contained in the next layer
                while (!layers[layer].IsEmpty && layers[layer].Last.Interval.StrictlyContains(interval))
                    layer++;

                layer++;

                while (layers.Count < layer + 1)
                    layers.Add(new ArrayList<Node>());

                layers[layer - 1].Add(new Node(interval, layers[layer].Count));
            }

            var lastCount = 0;
            for (var i = layers.Count - 1; i >= 0; i--)
            {
                layers[i].Add(new Node(lastCount));
                lastCount = layers[i].Count - 1;
            }

            return layers;
        }

        #endregion

        #region CollectionValue

        /// <inheritdoc/>
        public override bool IsEmpty
        {
            get { return Count == 0; }
        }

        /// <inheritdoc/>
        public override int Count { get { return _count; } }

        /// <inheritdoc/>
        public override Speed CountSpeed
        {
            get { return Speed.Constant; }
        }

        /// <inheritdoc/>
        public override I Choose()
        {
            if (Count == 0)
                throw new NoSuchItemException();

            return _layers.First().First().Interval;
        }

        #endregion

        /// <inheritdoc/>
        public int CountOverlaps(IInterval<T> query)
        {
            // Break if we won't find any overlaps
            if (ReferenceEquals(query, null) || IsEmpty)
                return 0;

            return countOverlaps(0, 0, _counts[0], query);
        }

        private int countOverlaps(int layer, int lower, int upper, IInterval<T> query)
        {
            var count = 0;

            while (lower < upper)
            {
                var first = lower;

                // The first interval doesn't overlap we need to search for it
                if (!_layers[layer][first].Interval.Overlaps(query))
                {
                    // We know first doesn't overlap so we can increment it before searching
                    first = findFirst(layer, ++first, upper, query);

                    // If index is out of bound, or found interval doesn't overlap, then the layer won't contain any overlaps
                    if (upper <= first || !_layers[layer][first].Interval.Overlaps(query))
                        return count;
                }

                // We can use first as lower to speed up the search
                var last = findLast(layer, first, upper, query);

                lower = _layers[layer][first].Pointer;
                upper = _layers[layer][last].Pointer;
                layer++;

                count += last - first;

            }
            return count;
        }

        /// <summary>
        /// Will return the index of the first interval that overlaps the query
        /// </summary>
        private int findFirst(int layer, int lower, int upper, IInterval<T> query)
        {
            int min = lower - 1, max = upper;

            while (max - min > 1)
            {
                var middle = min + ((max - min) >> 1); // Shift one is the same as dividing by 2

                var interval = _layers[layer][middle].Interval;

                var compare = query.Low.CompareTo(interval.High);

                if (compare < 0 || compare == 0 && query.LowIncluded && interval.HighIncluded)
                    max = middle;
                else
                    min = middle;
            }

            return max;
        }

        /// <summary>
        /// Will return the index of the last interval that overlaps the query
        /// </summary>
        private int findLast(int layer, int lower, int upper, IInterval<T> query)
        {
            int min = lower - 1, max = upper;

            while (max - min > 1)
            {
                var middle = min + ((max - min) >> 1); // Shift one is the same as dividing by 2

                var interval = _layers[layer][middle].Interval;

                var compare = interval.Low.CompareTo(query.High);

                if (compare < 0 || compare == 0 && interval.LowIncluded && query.HighIncluded)
                    min = middle;
                else
                    max = middle;
            }

            return max;
        }

        /// <inheritdoc/>
        public override IEnumerator<I> GetEnumerator()
        {
            if (IsEmpty)
                return (new I[] { }).Cast<I>().GetEnumerator();

            return getEnumerator(0, 0, _counts[0]);
        }

        private IEnumerator<I> getEnumerator(int level, int start, int end)
        {
            while (start < end)
            {
                var node = _layers[level][start];

                yield return node.Interval;

                // Check if we are at the last node
                if (node.Pointer < _layers[level][start + 1].Pointer)
                {
                    var child = getEnumerator(level + 1, node.Pointer, _layers[level][start + 1].Pointer);

                    while (child.MoveNext())
                        yield return child.Current;
                }

                start++;
            }
        }

        /// <inheritdoc/>
        public IInterval<T> Span
        {
            get
            {
                if (IsEmpty)
                    throw new InvalidOperationException("An empty collection has no span");

                return _span;
            }
        }

        /// <inheritdoc/>
        public int MaximumOverlap
        {
            get { throw new NotSupportedException(); }
        }

        /// <inheritdoc/>
        public IEnumerable<I> FindOverlaps(T query)
        {
            // Break if we won't find any overlaps
            if (ReferenceEquals(query, null) || IsEmpty)
                return Enumerable.Empty<I>();

            return FindOverlaps(new IntervalBase<T>(query));
        }

        /// <inheritdoc/>
        public IEnumerable<I> FindOverlaps(IInterval<T> query)
        {
            // Break if we won't find any overlaps
            if (ReferenceEquals(query, null) || IsEmpty)
                yield break;

            int layer = 0, lower = 0, upper = _counts[0];

            // Make sure first and last don't point at the same interval (theorem 2)
            while (lower < upper)
            {
                var currentLayer = _layers[layer];

                var first = lower;

                // The first interval doesn't overlap we need to search for it
                if (!currentLayer[first].Interval.Overlaps(query))
                {
                    // We know first doesn't overlap so we can increment it before searching
                    first = findFirst(layer, ++first, upper, query);

                    // If index is out of bound, or found interval doesn't overlap, then the list won't contain any overlaps
                    if (upper <= first || !currentLayer[first].Interval.Overlaps(query))
                        yield break;
                }

                // We can use first as lower to speed up the search
                var last = findLast(layer, first, upper, query);

                // Save values for next iteration
                layer++;
                lower = currentLayer[first].Pointer; // 0
                upper = currentLayer[last].Pointer; // _counts[layer]

                while (first < last)
                    yield return currentLayer[first++].Interval;
            }
        }

        /// <inheritdoc/>
        public bool FindOverlap(IInterval<T> query, ref I overlap)
        {
            // No overlap if query is null, collection is empty, or query doesn't overlap collection
            if (query == null || IsEmpty || !query.Overlaps(Span))
                return false;

            // Find first overlap
            var i = findFirst(0, 0, _counts[0], query);

            // Check if index is in bound and if the interval overlaps the query
            var result = 0 <= i && i < _counts[0] && _layers[0][i].Interval.Overlaps(query);

            if (result)
                overlap = _layers[0][i].Interval;

            return result;
        }

        /// <inheritdoc/>
        public bool FindOverlap(T query, ref I overlap)
        {
            return FindOverlap(new IntervalBase<T>(query), ref overlap);
        }

        private IEnumerable<I> findOverlapsRecursive(int layer, int lower, int upper, IInterval<T> query)
        {
            // Make sure first and last don't point at the same interval (theorem 2)
            if (lower >= upper)
                yield break;

            var first = lower;
            var currentLayer = _layers[layer];

            // The first interval doesn't overlap we need to search for it
            if (!currentLayer[first].Interval.Overlaps(query))
            {
                // We know first doesn't overlap so we can increment it before searching
                first = findFirst(layer, ++first, upper, query);

                // If index is out of bound, or found interval doesn't overlap, then the list won't contain any overlaps
                if (upper <= first || !currentLayer[first].Interval.Overlaps(query))
                    yield break;
            }

            // We can use first as lower to speed up the search
            var last = findLast(layer, first, upper, query);

            // Find intervals in sublist
            foreach (var interval in findOverlapsRecursive(layer + 1, currentLayer[first].Pointer, currentLayer[last].Pointer, query))
                yield return interval;

            while (first < last)
                yield return currentLayer[first++].Interval;
        }
        /// <summary>
        /// Get a string representation of the tree in GraphViz dot format.
        /// </summary>
        /// <returns>GraphViz string.</returns>
        public string Graphviz()
        {
            return String.Format("digraph LayeredContainmentList {{\n\trankdir=BT;\n\tnode [shape=record];\n\n{0}\n}}", graphviz());
        }

        private string graphviz()
        {
            var s = String.Empty;

            for (var layer = 0; layer < _counts.Length - 1; layer++)
            {
                var l = new ArrayList<string>();
                var p = String.Empty;
                for (var i = 0; i <= _counts[layer]; i++)
                {
                    l.Add(String.Format("<n{0}> {0}: {1}", i, _layers[layer][i]));

                    p += String.Format("layer{0}:n{1} -> layer{2}:n{3};\n\t", layer, i, layer + 1, _layers[layer][i].Pointer);
                }

                s += String.Format("\tlayer{0} [fontname=consola, label=\"{1}\"];\n\t{2}\n", layer, String.Join("|", l.ToArray()), p);
            }

            s += String.Format("\tlayer{0} [fontname=consola, label=\"<n0> 0: *\"];", _counts.Length - 1);

            return s;
        }

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
    }
}
