﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace C5.intervals
{
    public class LayeredContainmentList2<T> : CollectionValueBase<IInterval<T>>, IStaticIntervaled<T> where T : IComparable<T>
    {
        private readonly int _count;
        private readonly int _firstLayerCount;
        private readonly int _layerCount;

        private readonly IInterval<T>[][] _intervalLayers;
        private readonly int[][] _pointerLayers;

        private IInterval<T> _span;

        private int _pointOfMaximumOverlap = -1;
        private IInterval<T> _intervalOfMaximumOverlap;

        #region Node nested classes

        struct Node
        {
            internal IInterval<T> Interval { get; private set; }
            internal int Pointer { get; private set; }

            internal Node(IInterval<T> interval, int pointer)
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
                return Interval.ToString();
            }
        }

        #endregion

        #region Constructor

        public LayeredContainmentList2(IEnumerable<IInterval<T>> intervalEnumerable)
        {
            // Make intervals to array to allow fast sorting and counting
            var intervals = intervalEnumerable as IInterval<T>[] ?? intervalEnumerable.ToArray();

            // Stop if we have no intervals
            if (intervals.IsEmpty()) return;

            _count = intervals.Length;

            var nodeLayers = generateLayers(ref intervals);

            _layerCount = nodeLayers.Count();
            _firstLayerCount = nodeLayers.First.Count;

            // Create the list that contains the containment layers
            _intervalLayers = new IInterval<T>[_layerCount][];
            _pointerLayers = new int[_layerCount][];

            // Create each containment layer
            var lastCount = 0;
            for (var i = _layerCount - 1; i >= 0; i--)
            {
                var count = nodeLayers[i].Count;
                _intervalLayers[i] = new IInterval<T>[count];
                _pointerLayers[i] = new int[count + 1];

                for (var j = 0; j < count; j++)
                {
                    var node = nodeLayers[i][j];
                    _intervalLayers[i][j] = node.Interval;
                    _pointerLayers[i][j] = node.Pointer;
                }

                // Add sentinel pointer
                _pointerLayers[i][count] = lastCount;
                lastCount = count;
            }
        }

        private static ArrayList<ArrayList<Node>> generateLayers(ref IInterval<T>[] intervals)
        {
            // Used for tracking current layer
            var layer = 0;
            var layers = new ArrayList<ArrayList<Node>> { new ArrayList<Node>(), new ArrayList<Node>() };

            // Sort intervals
            var comparer = ComparerFactory<IInterval<T>>.CreateComparer(IntervalExtensions.CompareTo);
            Sorting.IntroSort(intervals, 0, intervals.Count(), comparer);

            foreach (var interval in intervals)
            {
                while (layer > 0 && layers[layer - 1].Last.Interval.CompareHigh(interval) <= 0)
                    layer--;

                // Check if interval will be contained in the next layer
                while (!layers[layer].IsEmpty && interval.CompareHigh(layers[layer].Last.Interval) < 0)
                    layer++;

                // Add extra layer if needed
                if (layers.Count == layer + 1)
                    layers.Add(new ArrayList<Node>());

                // Add interval and pointer to list
                layers[layer].Add(new Node(interval, layers[layer + 1].Count));
            }

            // Remove empty layer
            layers.Remove();

            return layers;
        }

        #endregion

        #region CollectionValue

        public override bool IsEmpty
        {
            get { return Count == 0; }
        }

        public override int Count { get { return _count; } }

        public override Speed CountSpeed
        {
            get { return Speed.Constant; }
        }

        public override IInterval<T> Choose()
        {
            if (IsEmpty)
                throw new NoSuchItemException();

            return _intervalLayers.First().First();
        }

        #endregion

        public int CountOverlaps(IInterval<T> query)
        {
            // Break if we won't find any overlaps
            if (query == null || IsEmpty)
                return 0;

            return countOverlaps(0, 0, _firstLayerCount, query);
        }

        private int countOverlaps(int layer, int lower, int upper, IInterval<T> query)
        {
            var count = 0;

            while (lower < upper)
            {
                var first = lower;

                // The first interval doesn't overlap we need to search for it
                if (!_intervalLayers[layer][first].Overlaps(query))
                {
                    // We know first doesn't overlap so we can increment it before searching
                    first = findFirst(layer, ++first, upper, query);

                    // If index is out of bound, or found interval doesn't overlap, then the layer won't contain any overlaps
                    if (upper <= first || !_intervalLayers[layer][first].Overlaps(query))
                        return count;
                }

                // We can use first as lower to speed up the search
                var last = findLast(layer, first, upper, query);

                lower = _pointerLayers[layer][first];
                upper = _pointerLayers[layer][last];
                layer++;

                count += last - first;
            }

            return count;
        }

        /// <summary>
        /// Will return the index of the first interval that overlaps the query
        /// </summary>
        /// <returns></returns>
        private int findFirst(int layer, int lower, int upper, IInterval<T> query)
        {
            int min = lower - 1, max = upper;

            var intervalLayer = _intervalLayers[layer];

            while (max - min > 1)
            {
                var middle = min + ((max - min) >> 1); // Shift one is the same as dividing by 2

                var interval = intervalLayer[middle];

                var compare = query.Low.CompareTo(interval.High);

                if (compare < 0 || compare == 0 && query.LowIncluded && interval.HighIncluded)
                    max = middle;
                else
                    min = middle;
            }

            return max;
        }

        private int findLast(int layer, int lower, int upper, IInterval<T> query)
        {
            int min = lower - 1, max = upper;
            var intervalLayer = _intervalLayers[layer];

            while (max - min > 1)
            {
                var middle = min + ((max - min) >> 1); // Shift one is the same as dividing by 2

                var interval = intervalLayer[middle];

                var compare = interval.Low.CompareTo(query.High);

                if (compare < 0 || compare == 0 && interval.LowIncluded && query.HighIncluded)
                    min = middle;
                else
                    max = middle;
            }

            return max;
        }

        /// <summary>
        /// Fast enumeration of intervals in arbitrary order, not sorted. For sorted enumerator see <see cref="GetEnumeratorSorted"/> or better <see cref="Sorted"/>.
        /// </summary>
        /// <returns>Enumerator of all intervals in the data structure in arbitrary order</returns>
        public override IEnumerator<IInterval<T>> GetEnumerator()
        {
            if (IsEmpty)
                return (new IInterval<T>[] { }).Cast<IInterval<T>>().GetEnumerator();

            return getEnumerator();
        }

        /// <summary>
        /// Loops through each layer and yield its intervals
        /// </summary>
        /// <returns>Enumerator of all intervals in the data structure</returns>
        private IEnumerator<IInterval<T>> getEnumerator()
        {
            for (var i = 0; i < _layerCount; i++)
            {
                var intervalCount = _intervalLayers[i].Count();
                for (var j = 0; j < intervalCount; j++)
                    yield return _intervalLayers[i][j];
            }
        }

        /// <summary>
        /// Enumeration of intervals in sorted order according to <see cref="IntervalExtensions.CompareTo{T}"/>. For a faster, but unsorted, enumerator see <see cref="GetEnumerator"/>.
        /// </summary>
        /// <returns>Enumerator of all intervals in the data structure in sorted order</returns>
        public IEnumerator<IInterval<T>> GetEnumeratorSorted()
        {
            if (IsEmpty)
                return (new IInterval<T>[] { }).Cast<IInterval<T>>().GetEnumerator();

            return getEnumeratorSorted(0, _firstLayerCount);
        }

        /// <summary>
        /// Property exposing the method <see cref="GetEnumeratorSorted"/> as IEnumerable&lt;IInterval&lt;T&gt;&gt;.
        /// Usefull for loops: foreach (var interval in intervaled.Sorted) { }. 
        /// </summary>
        public IEnumerable<IInterval<T>> Sorted
        {
            get
            {
                var iterator = GetEnumeratorSorted();
                while (iterator.MoveNext())
                    yield return iterator.Current;
            }
        }

        /// <summary>
        /// Enumerate intervals in sorted order using the pointers
        /// </summary>
        /// <param name="start">The index of the first interval in the first layer</param>
        /// <param name="end">The index after the last interval in the first layer</param>
        /// <returns>Enumerator of all intervals in the data structure in sorted order</returns>
        private IEnumerator<IInterval<T>> getEnumeratorSorted(int start, int end)
        {
            // Create our own stack to avoid stack overflow and to speed up the enumerator
            var stack = new int[_layerCount * 2];
            var i = 0;
            // We stack both values consecutively instead of stacking pairs
            stack[i++] = start;
            stack[i++] = end;

            // Continue as long as we still have values on the stack
            while (i > 0)
            {
                // Get start and end from stack
                end = stack[--i];
                start = stack[--i];

                // Cache layers for speed
                var intervalLayer = _intervalLayers[i >> 1];
                var pointerLayer = _pointerLayers[i >> 1];

                while (start < end)
                {
                    yield return intervalLayer[start];

                    // If this and the next interval point to different intervals in the next layer, we need to swap layer
                    if (pointerLayer[start] < pointerLayer[start + 1])
                    {
                        // Push the current values
                        stack[i++] = start + 1;
                        stack[i++] = end;
                        // Push the values for the next layer
                        stack[i++] = pointerLayer[start];
                        stack[i++] = pointerLayer[start + 1];
                        break;
                    }

                    start++;
                }
            }
        }

        public IInterval<T> Span
        {
            get
            {
                if (IsEmpty)
                    throw new InvalidOperationException("An empty collection has no span");

                // Cache value for later requests
                if (_span == null)
                    _span = new IntervalBase<T>(_intervalLayers.First().First(), _intervalLayers.First()[_firstLayerCount - 1]);

                return _span;
            }
        }

        public IEnumerable<IInterval<T>> FindOverlaps(T query)
        {
            // Break if we won't find any overlaps
            if (ReferenceEquals(query, null) || IsEmpty)
                return Enumerable.Empty<IInterval<T>>();

            return FindOverlaps(new IntervalBase<T>(query));
        }

        public IEnumerable<IInterval<T>> FindOverlaps(IInterval<T> query)
        {
            // Break if we won't find any overlaps
            if (query == null || IsEmpty)
                yield break;

            int layer = 0, lower = 0, upper = _firstLayerCount;

            // Make sure first and last don't point at the same interval (theorem 2)
            while (lower < upper)
            {
                var currentLayer = _intervalLayers[layer];

                var first = lower;

                // The first interval doesn't overlap we need to search for it
                if (!currentLayer[first].Overlaps(query))
                {
                    // We know first doesn't overlap so we can increment it before searching
                    first = findFirst(layer, ++first, upper, query);

                    // If index is out of bound, or found interval doesn't overlap, then the list won't contain any overlaps
                    if (upper <= first || !currentLayer[first].Overlaps(query))
                        yield break;
                }

                // We can use first as lower to speed up the search
                var last = findLast(layer, first, upper, query);

                // Save values for next iteration
                lower = _pointerLayers[layer][first]; // 0
                upper = _pointerLayers[layer][last]; // _counts[layer]
                layer++;

                while (first < last)
                    yield return currentLayer[first++];
            }
        }

        public bool OverlapExists(IInterval<T> query)
        {
            // No overlap if query is null, collection is empty, or query doesn't overlap collection
            if (query == null || IsEmpty || !query.Overlaps(Span))
                return false;

            // Find first overlap
            var i = findFirst(0, 0, _firstLayerCount, query);

            // Check if index is in bound and if the interval overlaps the query
            return 0 <= i && i < _firstLayerCount && _intervalLayers[0][i].Overlaps(query);
        }

        public int MaximumOverlap
        {
            get
            {
                if (_pointOfMaximumOverlap < 0)
                {
                    // Init running maximum to the number of layers as that is the minimum number of overlaps
                    var maximum = _layerCount;

                    // Create queue sorted on high intervals
                    var comparer = ComparerFactory<IInterval<T>>.CreateComparer(IntervalExtensions.CompareHigh);
                    var queue = new IntervalHeap<IInterval<T>>(comparer);

                    // Loop through intervals in sorted order
                    foreach (var interval in Sorted)
                    {
                        // Remove all intervals not overlapping the current interval from the queue
                        while (!queue.IsEmpty && interval.CompareLowHigh(queue.FindMin()) > 0)
                            queue.DeleteMin();

                        queue.Add(interval);

                        if (queue.Count > maximum)
                        {
                            maximum = queue.Count;
                            // Create a new interval when new maximum is found
                            // The low is the current intervals low due to the intervals being sorted
                            // The high is the smallest high in the queue
                            _intervalOfMaximumOverlap = new IntervalBase<T>(interval, queue.FindMin());
                        }
                    }

                    // Cache value for later requests
                    _pointOfMaximumOverlap = maximum;
                }

                return _pointOfMaximumOverlap;
            }
        }

        public IInterval<T> IntervalOfMaximumOverlap
        {
            get
            {
                if (_intervalOfMaximumOverlap == null)
                    throw new InvalidOperationException("An empty collection has no interval of maximum overlap");

                return _intervalOfMaximumOverlap;
            }
        }

        public string Graphviz()
        {
            return String.Format("digraph LayeredContainmentList2 {{\n\trankdir=BT;\n\tnode [shape=record];\n\n{0}\n}}", graphviz());
        }

        private string graphviz()
        {
            var s = String.Empty;

            var layer = 0;
            int lower = 0, upper = _firstLayerCount;

            while (lower < upper)
            {
                var l = new ArrayList<string>();
                var p = String.Empty;
                for (var i = 0; i < upper; i++)
                {
                    l.Add(String.Format("<n{0}> {0}: {1}", i, _intervalLayers[layer][i]));

                    p += String.Format("layer{0}:n{1} -> layer{2}:n{3};\n\t", layer, i, layer + 1, _pointerLayers[layer][i]);
                }

                // Sentinel node
                l.Add(String.Format("<n{0}> {0}: *", upper));
                p += String.Format("layer{0}:n{1} -> layer{2}:n{3};\n\t", layer, upper, layer + 1, _pointerLayers[layer][upper - 1]);

                s += String.Format("\tlayer{0} [fontname=consola, label=\"{1}\"];\n\t{2}\n", layer, String.Join("|", l.ToArray()), p);


                lower = _pointerLayers[layer][lower];
                upper = _pointerLayers[layer][upper];
                layer++;
            }

            s += String.Format("\tlayer{0} [fontname=consola, label=\"<n0> 0: *\"];", layer);

            return s;
        }
    }
}