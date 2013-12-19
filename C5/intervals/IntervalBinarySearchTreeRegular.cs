﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
namespace C5.intervals
{
    /// <summary>
    /// An implementation of the Interval Binary Search Tree as described by Hanson et. al in "The IBS-Tree: A Data Structure for Finding All Intervals That Overlap a Point" using an AVL tree balancing scheme.
    /// </summary>
    /// <remarks>It is important to implement a proper hashing method for <see cref="I"/> not based on endpoints. Failing to do so will result in worse runtimes for interval duplicate objects.</remarks>
    /// <typeparam name="I">The interval type.</typeparam>
    /// <typeparam name="T">The interval endpoint type.</typeparam>
    public class IntervalBinarySearchTreeRegular<I, T> : CollectionValueBase<I>, IIntervalCollection<I, T>
        where I : IInterval<T>
        where T : IComparable<T>
    {
        #region Fields

        private Node _root;
        private int _count;
        private IInterval<T> _span;

        private static readonly IEqualityComparer<I> Comparer = ComparerFactory<I>.CreateEqualityComparer((x, y) => ReferenceEquals(x, y), x => x.GetHashCode());

        #endregion

        #region Code Contracts

        [ContractInvariantMethod]
        // Check the invariants of the IBS tree.
        private void invariants()
        {
            // Check the balance invariant holds.
            Contract.Invariant(contractHelperConfirmBalance(_root));

            // Check nodes are sorted
            Contract.Invariant(contractHelperCheckNodesAreSorted(_root));

            // Check that the maximum depth variables are correct for all nodes
            Contract.Invariant(contractHelperCheckMnoAndIntervalsEndingInNodeForEachNode(_root));

            // Check that the IBS tree invariants from the Hanson article holds.
            Contract.Invariant(Contract.ForAll(nodes(_root), contractHelperCheckIbsInvariants));

            // Check that the intervals are correctly placed
            Contract.Invariant(contractHelperConfirmIntervalPlacement(_root));
        }

        [Pure]
        private static bool contractHelperCheckNodesAreSorted(Node root)
        {
            return nodes(root).IsSorted();
        }

        [Pure]
        private static bool contractHelperCheckMnoAndIntervalsEndingInNodeForEachNode(Node root)
        {
            if (root != null && root.Sum != 0)
                return false;

            foreach (var keyValuePair in contractHelperGetIntervalsByEndpoint(root))
            {
                var key = keyValuePair.Key;
                var intervals = keyValuePair.Value;

                var deltaAt = 0;
                var deltaAfter = 0;

                foreach (var interval in intervals)
                {
                    if (interval.Low.CompareTo(key) == 0)
                    {
                        if (interval.LowIncluded)
                            deltaAt++;
                        else
                            deltaAfter++;
                    }

                    if (interval.High.CompareTo(key) == 0)
                    {
                        if (interval.HighIncluded)
                            deltaAfter--;
                        else
                            deltaAt--;
                    }
                }

                var node = contractHelperFindNode(root, key);

                // Check DeltaAt and DeltaAfter
                if (node.DeltaAt != deltaAt || node.DeltaAfter != deltaAfter)
                    return false;

                // Check Sum and Max
                if (!contractHelperCheckMno(node))
                    return false;

                // Check IntervalsEndingInNode
                if (!Contract.ForAll(node.IntervalsEndingInNode, intervals.Contains) && node.IntervalsEndingInNode.Count() != intervals.Count)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Find the node containing the search key.
        /// </summary>
        /// <param name="node">The root node which subtree should be searched.</param>
        /// <param name="key">The key being searched.</param>
        /// <returns>The node containing the key if it exists, otherwise null.</returns>
        [Pure]
        private static Node contractHelperFindNode(Node node, T key)
        {
            Contract.Requires(node != null);
            Contract.Ensures(Contract.Result<Node>() != null);

            while (node != null)
            {
                var compare = key.CompareTo(node.Key);

                if (compare > 0)
                    node = node.Right;
                else if (compare < 0)
                    node = node.Left;
                else
                    break;
            }

            return node;
        }

        [Pure]
        private static IEnumerable<KeyValuePair<T, IntervalSet>> contractHelperGetIntervalsByEndpoint(Node root)
        {
            var dictionary = new TreeDictionary<T, IntervalSet>();

            foreach (var interval in intervals(root))
            {
                // Make sure the sets exist
                if (!dictionary.Contains(interval.Low))
                    dictionary.Add(interval.Low, new IntervalSet());
                if (!dictionary.Contains(interval.High))
                    dictionary.Add(interval.High, new IntervalSet());

                // Add interval for low and high
                dictionary[interval.Low].Add(interval);
                dictionary[interval.High].Add(interval);
            }

            return dictionary;
        }

        [Pure]
        private static bool contractHelperCheckMno(Node node)
        {
            Contract.Requires(node != null);

            // Check sum
            var sum = (node.Left != null ? node.Left.Sum : 0) + node.DeltaAt + node.DeltaAfter +
                      (node.Right != null ? node.Right.Sum : 0);

            if (node.Sum != sum)
                return false;

            // Check max
            var max = Int32.MinValue;

            sum = node.Left != null ? node.Left.Max : 0;
            if (sum > max)
                max = sum;

            sum = (node.Left != null ? node.Left.Sum : 0) + node.DeltaAt;
            if (sum > max)
                max = sum;

            sum = (node.Left != null ? node.Left.Sum : 0) + node.DeltaAt + node.DeltaAfter;
            if (sum > max)
                max = sum;

            sum = (node.Left != null ? node.Left.Sum : 0) + node.DeltaAt + node.DeltaAfter + (node.Right != null ? node.Right.Max : 0);
            if (sum > max)
                max = sum;

            if (node.Max != max)
                return false;

            return true;
        }

        /// <summary>
        /// Check the invariants of the IBS tree.
        /// The invariants are from the article "The IBS-tree: A Data Structure for Finding All Intervals That Overlap a Point" by Hanson and Chaabouni.
        /// The invariants are located on page 4 of the article.
        /// </summary>
        /// <param name="v">The node to check (It only makes sense to check all the nodes of the tree, so call this enumerating the entire tree)</param>
        /// <returns>Returns true if all the invariants hold and false if one of them does not hold.</returns>
        [Pure]
        private bool contractHelperCheckIbsInvariants(Node v)
        {
            Contract.Requires(v != null);

            Node rightUp = null;
            Node leftUp = null;

            // Find j intervals and left and right parent
            var js = contractHelperFindJs(v, ref leftUp, ref rightUp);

            // Interval represented by Less
            var greaterInterval = rightUp != null ? new IntervalBase<T>(v.Key, rightUp.Key, IntervalType.Open) : null;
            // Interval represented by Equal
            var equalInterval = new IntervalBase<T>(v.Key);
            // Interval represented by Greater
            var lessInterval = leftUp != null ? new IntervalBase<T>(leftUp.Key, v.Key, IntervalType.Open) : null;


            // Check containment invariant
            if (v.Less != null && lessInterval != null)
                if (v.Less.Any(i => !i.Contains(lessInterval)))
                    return false;

            if (v.Greater != null && greaterInterval != null)
                if (v.Greater.Any(i => !i.Contains(greaterInterval)))
                    return false;

            if (v.Equal != null)
                if (v.Equal.Any(i => !i.Contains(equalInterval)))
                    return false;


            // Check maximum invariant
            foreach (var j in js)
            {
                // Check containment invariant for both Less and Greater
                if (v.Less != null && lessInterval != null)
                    // If there is an inteval in Less that contains J the invariant doesn't hold
                    if (v.Less.Any(i => i.Contains(j)))
                        return false;

                if (v.Greater != null && greaterInterval != null)
                    // If there is an inteval in Greater that contains J the invariant doesn't hold
                    if (v.Greater.Any(i => i.Contains(j)))
                        return false;

                if (v.Equal != null)
                    // If there is an inteval in Equal that contains J the invariant doesn't hold
                    if (v.Equal.Any(i => i.Contains(j)))
                        return false;
            }

            return true;
        }

        [Pure]
        private IEnumerable<IInterval<T>> contractHelperFindJs(Node v, ref Node leftUp, ref Node rightUp)
        {
            Contract.Requires(v != null);

            var set = new HashSet<IInterval<T>>();
            var root = _root;

            while (root != null)
            {
                var compare = v.CompareTo(root);

                if (compare > 0)
                {
                    // Add a new j interval to the set
                    if (v.CompareTo(root.Right) < 0)
                        set.Add(new IntervalBase<T>(root.Key, root.Right.Key, IntervalType.Open));
                    else if (rightUp != null)
                        set.Add(new IntervalBase<T>(root.Key, rightUp.Key, IntervalType.Open));

                    // Update left parent
                    leftUp = root;

                    root = root.Right;
                }
                else if (compare < 0)
                {
                    // Add a new j interval to the set
                    if (v.CompareTo(root.Left) > 0)
                        set.Add(new IntervalBase<T>(root.Left.Key, root.Key, IntervalType.Open));
                    else if (leftUp != null)
                        set.Add(new IntervalBase<T>(leftUp.Key, root.Key, IntervalType.Open));

                    // Update right parent
                    rightUp = root;

                    root = root.Left;
                }
                else
                    // Stop the loop when we find the node
                    break;
            }

            return set;
        }

        /// <summary>
        /// Checks that the contractHelperHeight of the tree is balanced.
        /// </summary>
        /// <returns>True if the tree is balanced, else false.</returns>
        [Pure]
        private static bool contractHelperConfirmBalance(Node root)
        {
            var result = true;
            contractHelperHeight(root, ref result);
            return result;
        }

        /// <summary>
        /// Get the contractHelperHeight of the tree.
        /// </summary>
        /// <param name="node">The node you wish to check the contractHelperHeight on.</param>
        /// <param name="result">Reference to a bool that will be set to false if an in-balance is discovered.</param>
        /// <returns>Height of the tree.</returns>
        [Pure]
        private static int contractHelperHeight(Node node, ref bool result)
        {
            if (node == null)
                return 0;

            var heightLeft = contractHelperHeight(node.Left, ref result);
            var heightRight = contractHelperHeight(node.Right, ref result);

            if (node.Balance != heightRight - heightLeft)
                result = false;

            return Math.Max(heightLeft, heightRight) + 1;
        }

        [Pure]
        private bool contractHelperConfirmIntervalPlacement(Node root)
        {
            foreach (var interval in this)
            {
                if (!contractHelperConfirmLowPlacement(interval, root))
                    return false;
                if (!contractHelperConfirmHighPlacement(interval, root))
                    return false;
            }
            return true;
        }

        [Pure]
        private static bool contractHelperConfirmLowPlacement(I interval, Node root, Node rightUp = null, bool result = true)
        {
            var compare = root.Key.CompareTo(interval.Low);
            if (compare == 0)
            {
                if (rightUp != null && rightUp.Key.CompareTo(interval.High) <= 0)
                    result &= root.Greater.Contains(interval);
                if (interval.LowIncluded)
                    result &= root.Equal.Contains(interval);
            }
            else if (compare < 0)
                return contractHelperConfirmLowPlacement(interval, root.Right, rightUp, result);
            else if (compare > 0)
            {
                if (root.Key.CompareTo(interval.High) < 0)
                    result &= root.Equal.Contains(interval);
                if (rightUp != null && rightUp.Key.CompareTo(interval.High) <= 0)
                    result &= root.Greater.Contains(interval);
                return contractHelperConfirmLowPlacement(interval, root.Left, root, result);
            }
            return result;
        }

        [Pure]
        private static bool contractHelperConfirmHighPlacement(I interval, Node root, Node leftUp = null, bool result = true)
        {
            var compare = root.Key.CompareTo(interval.High);
            if (compare == 0)
            {
                if (leftUp != null && leftUp.Key.CompareTo(interval.Low) >= 0)
                    result &= root.Less.Contains(interval);
                if (interval.HighIncluded)
                    result &= root.Equal.Contains(interval);
            }
            else if (compare > 0)
                return contractHelperConfirmHighPlacement(interval, root.Left, leftUp, result);
            else if (compare < 0)
            {
                if (root.Key.CompareTo(interval.Low) > 0)
                    result &= root.Equal.Contains(interval);
                if (leftUp != null && leftUp.Key.CompareTo(interval.Low) >= 0)
                    result &= root.Less.Contains(interval);
                return contractHelperConfirmHighPlacement(interval, root.Right, root, result);
            }
            return result;
        }

        #endregion

        #region Inner Classes

        class Node : IComparable<Node>
        {
            #region Code Contracts

            [ContractInvariantMethod]
            private void invariant()
            {
                // The key cannot be null
                Contract.Invariant(Key != null);
                // Balance never has an absolute value greater than 2
                Contract.Invariant(-2 <= Balance && Balance <= 2);
            }

            #endregion

            #region Properties

            public T Key { get; private set; }

            public Node Left { get; internal set; }
            public Node Right { get; internal set; }

            // Balance - between -2 and +2
            public sbyte Balance { get; internal set; }

            // Fields for Maximum Depth
            public int DeltaAt { get; internal set; }
            public int DeltaAfter { get; internal set; }
            public int Sum { get; private set; }
            public int Max { get; private set; }

            // Used for printing
            public bool Dummy { get; private set; }

            public IntervalSet Less { get; set; }
            public IntervalSet Equal { get; set; }
            public IntervalSet Greater { get; set; }

            public IntervalSet IntervalsEndingInNode { get; private set; }

            #endregion

            #region Constructors

            public Node(T key)
            {
                Contract.Requires(key != null);

                Key = key;
                IntervalsEndingInNode = new IntervalSet();
            }

            public Node()
            {
                Dummy = true;
            }

            #endregion

            #region Public Methods

            /// <summary>
            /// Update the maximum depth value for the node.
            /// </summary>
            /// <returns>True if value changed.</returns>
            public bool UpdateMaximumDepth()
            {
                Contract.Ensures(Contract.Result<bool>() == (Contract.OldValue(Max) != Max || Contract.OldValue(Sum) != Sum));

                // Cache values
                var oldMax = Max;
                var oldSum = Sum;

                // Set Max to Left's Max
                Max = Left != null ? Left.Max : 0;

                // Start building up the other possible Max sums
                var value = (Left != null ? Left.Sum : 0) + DeltaAt;
                // And check if they are higher the previously found max
                if (value > Max)
                    Max = value;

                // Add DeltaAfter and check for new max
                value += DeltaAfter;
                if (value > Max)
                    Max = value;

                // Save the sum value using the previous calculations
                Sum = value + (Right != null ? Right.Sum : 0);

                // Add Right's max and check for new max
                value += Right != null ? Right.Max : 0;
                if (value > Max)
                    Max = value;

                // Return true if either Max or Sum changed
                return oldMax != Max || oldSum != Sum;
            }

            public int CompareTo(Node other)
            {
                return Key.CompareTo(other.Key);
            }

            public override string ToString()
            {
                return Key.ToString();
            }

            public void Swap(Node successor)
            {
                Contract.Requires(successor != null);

                var tmp = Key;
                Key = successor.Key;
                successor.Key = tmp;

                IntervalsEndingInNode = successor.IntervalsEndingInNode;
                DeltaAfter = successor.DeltaAfter;
                DeltaAt = successor.DeltaAt;

                // Reset delta values in successor
                successor.DeltaAt = successor.DeltaAfter = 0;
            }

            #endregion

        }

        private sealed class IntervalSet : HashSet<I>
        {
            public IntervalSet(IEnumerable<I> set)
            {
                AddAll(set);
            }

            public IntervalSet()
                : base(Comparer)
            {
            }

            public override string ToString()
            {
                var s = new ArrayList<string>();

                foreach (var interval in this)
                    s.Add(interval.ToString());

                return s.IsEmpty ? String.Empty : String.Join(", ", s.ToArray());
            }

            public static IntervalSet operator -(IntervalSet s1, IntervalSet s2)
            {
                Contract.Requires(s1 != null);
                Contract.Requires(s2 != null);

                var res = new IntervalSet();
                foreach (var interval in s1.Where(interval => !s2.Contains(interval)))
                    res.Add(interval);
                return res;
            }
        }

        #endregion

        #region AVL Tree Methods

        private static Node rotateForAdd(Node root, ref bool updateBalance)
        {
            Contract.Requires(root != null);

            Contract.Requires(root.Balance != -2 || root.Left != null);
            Contract.Requires(root.Balance != -2 || root.Left.Balance != -1 || root.Left.Left != null);
            Contract.Requires(root.Balance != -2 || root.Left.Balance != +1 || root.Left.Right != null);

            Contract.Requires(root.Balance != +2 || root.Right != null);
            Contract.Requires(root.Balance != +2 || root.Right.Balance != -1 || root.Right.Left != null);
            Contract.Requires(root.Balance != +2 || root.Right.Balance != +1 || root.Right.Right != null);

            switch (root.Balance)
            {
                // Node is balanced after the node was added
                case 0:
                    updateBalance = false;
                    break;

                // Node is unbalanced, so we rotate
                case -2:
                    switch (root.Left.Balance)
                    {
                        // Left Left Case
                        case -1:
                            root = rotateRight(root);
                            root.Balance = root.Right.Balance = 0;
                            break;

                        // Left Right Case
                        case +1:
                            root.Left = rotateLeft(root.Left);
                            root = rotateRight(root);

                            // root.Balance is either -1, 0, or +1
                            root.Left.Balance = (sbyte) (root.Balance == +1 ? -1 : 0);
                            root.Right.Balance = (sbyte) (root.Balance == -1 ? +1 : 0);
                            root.Balance = 0;
                            break;
                    }
                    updateBalance = false;
                    break;

                // Node is unbalanced, so we rotate
                case +2:
                    switch (root.Right.Balance)
                    {
                        // Right Right Case
                        case +1:
                            root = rotateLeft(root);
                            root.Balance = root.Left.Balance = 0;
                            break;

                        // Right Left Case
                        case -1:
                            root.Right = rotateRight(root.Right);
                            root = rotateLeft(root);

                            // root.Balance is either -1, 0, or +1
                            root.Left.Balance = (sbyte) (root.Balance == +1 ? -1 : 0);
                            root.Right.Balance = (sbyte) (root.Balance == -1 ? +1 : 0);
                            root.Balance = 0;
                            break;
                    }

                    updateBalance = false;
                    break;
            }

            return root;
        }

        private static Node rotateForRemove(Node root, ref bool updateBalance)
        {
            Contract.Requires(root != null);

            Contract.Requires(root.Balance != -2 || root.Left != null);
            Contract.Requires(root.Balance != -2 || root.Left.Balance != -1 || root.Left.Left != null);
            Contract.Requires(root.Balance != -2 || root.Left.Balance != +1 || root.Left.Right != null);

            Contract.Requires(root.Balance != +2 || root.Right != null);
            Contract.Requires(root.Balance != +2 || root.Right.Balance != -1 || root.Right.Left != null);
            Contract.Requires(root.Balance != +2 || root.Right.Balance != +1 || root.Right.Right != null);

            switch (root.Balance)
            {
                // High will not change for parent, so we can stop here
                case -1:
                case +1:
                    updateBalance = false;
                    break;

                // Node is unbalanced, so we rotate
                case -2:
                    switch (root.Left.Balance)
                    {
                        // Left Left Case
                        case -1:
                            root = rotateRight(root);
                            root.Balance = root.Right.Balance = 0;
                            break;

                        case 0:
                            root = rotateRight(root);
                            root.Right.Balance = -1;
                            root.Balance = +1;
                            updateBalance = false;
                            break;

                        // Left Right Case
                        case +1:
                            root.Left = rotateLeft(root.Left);
                            root = rotateRight(root);

                            // root.Balance is either -1, 0, or +1
                            root.Left.Balance = (sbyte) ((root.Balance == +1) ? -1 : 0);
                            root.Right.Balance = (sbyte) ((root.Balance == -1) ? +1 : 0);
                            root.Balance = 0;
                            break;
                    }
                    break;

                // Node is unbalanced, so we rotate
                case +2:
                    switch (root.Right.Balance)
                    {
                        // Right Right Case
                        case +1:
                            root = rotateLeft(root);
                            root.Balance = root.Left.Balance = 0;
                            break;

                        case 0:
                            root = rotateLeft(root);
                            root.Left.Balance = 1;
                            root.Balance = -1;
                            updateBalance = false;
                            break;

                        // Right Left Case
                        case -1:
                            root.Right = rotateRight(root.Right);
                            root = rotateLeft(root);

                            // root.Balance is either -1, 0, or +1
                            root.Left.Balance = (sbyte) (root.Balance == +1 ? -1 : 0);
                            root.Right.Balance = (sbyte) (root.Balance == -1 ? +1 : 0);
                            root.Balance = 0;
                            break;
                    }
                    break;
            }

            return root;
        }

        private static Node rotateRight(Node root)
        {
            Contract.Requires(root != null);
            Contract.Requires(root.Left != null);

            // Rotate
            var node = root.Left;
            root.Left = node.Right;
            node.Right = root;

            if (root.Less != null && !root.Less.IsEmpty)
            {
                // node.Less = node.Less U root.Less
                if (node.Less == null)
                    node.Less = new IntervalSet(root.Less);
                else
                    node.Less.AddAll(root.Less);

                // node.Equal = node.Less U root.Less
                if (node.Equal == null)
                    node.Equal = new IntervalSet(root.Less);
                else
                    node.Equal.AddAll(root.Less);
            }

            if (node.Greater != null && !node.Greater.IsEmpty)
            {
                var rootGreaterIsEmpty = root.Greater == null || root.Greater.IsEmpty;
                // unique = node.Greater - root.Greater
                var uniqueInNodeGreater = rootGreaterIsEmpty
                    ? node.Greater
                    : node.Greater - root.Greater;

                // root.Less = root.Less U unique
                if (root.Less != null)
                    root.Less.AddAll(uniqueInNodeGreater);
                else
                {
                    // If root.Greater is all unique
                    if (uniqueInNodeGreater.Count == node.Greater.Count)
                    {
                        // Swap references
                        root.Less = node.Greater;
                        node.Greater = null;
                    }
                    else
                    {
                        // If root.Greater is empty, uniqueInNodeGreater is a pointer to the set node.Greater
                        // We don't want root.Less and node.Greater to be the same IntervalSet object, so we duplicate it
                        root.Less = rootGreaterIsEmpty ? new IntervalSet(uniqueInNodeGreater) : uniqueInNodeGreater;
                    }
                }

                // node.Greater = node.Greater - unique
                if (rootGreaterIsEmpty)
                    node.Greater = null;
                else if (node.Greater != null)
                    node.Greater.RemoveAll(uniqueInNodeGreater);
            }

            if (node.Greater != null && !node.Greater.IsEmpty)
            {
                // root.Greater = root.Greater - node.Greater
                if (root.Greater != null && !root.Greater.IsEmpty)
                    root.Greater.RemoveAll(node.Greater);

                // root.Equal = root.Equal - node.Greater
                if (root.Equal != null && !root.Equal.IsEmpty)
                    root.Equal.RemoveAll(node.Greater);
            }

            // Update maximum depth
            root.UpdateMaximumDepth();
            node.UpdateMaximumDepth();

            return node;
        }

        private static Node rotateLeft(Node root)
        {
            Contract.Requires(root != null);
            Contract.Requires(root.Right != null);

            // Rotate
            var node = root.Right;
            root.Right = node.Left;
            node.Left = root;

            if (root.Greater != null && !root.Greater.IsEmpty)
            {
                // node.Greater = node.Greater U root.Greater
                if (node.Greater == null)
                    node.Greater = new IntervalSet(root.Greater);
                else
                    node.Greater.AddAll(root.Greater);

                // node.Equal = node.Greater U root.Greater
                if (node.Equal == null)
                    node.Equal = new IntervalSet(root.Greater);
                else
                    node.Equal.AddAll(root.Greater);
            }

            if (node.Less != null && !node.Less.IsEmpty)
            {
                var rootLessIsEmpty = root.Less == null || root.Less.IsEmpty;
                // unique = node.Less - root.Less
                var uniqueInNodeLess = rootLessIsEmpty
                    ? node.Less
                    : node.Less - root.Less;

                // root.Greater = root.Greater U unique
                if (root.Greater != null)
                    root.Greater.AddAll(uniqueInNodeLess);
                else
                {
                    // If root.Less is all unique
                    if (uniqueInNodeLess.Count == node.Less.Count)
                    {
                        // Swap references
                        root.Greater = node.Less;
                        node.Less = null;
                    }
                    else
                    {
                        // If root.Less is empty, uniqueInNodeLess is a pointer to the set node.Less
                        // We don't want root.Greater and node.Less to be the same IntervalSet object, so we duplicate it
                        root.Greater = rootLessIsEmpty ? new IntervalSet(uniqueInNodeLess) : uniqueInNodeLess;
                    }
                }

                // node.Less = node.Less - unique
                if (rootLessIsEmpty)
                    node.Less = null;
                else if (node.Less != null)
                    node.Less.RemoveAll(uniqueInNodeLess);
            }

            if (node.Less != null && !node.Less.IsEmpty)
            {
                // root.Less = root.Less - node.Less
                if (root.Less != null && !root.Less.IsEmpty)
                    root.Less.RemoveAll(node.Less);

                // root.Equal = root.Equal - node.Less
                if (root.Equal != null && !root.Equal.IsEmpty)
                    root.Equal.RemoveAll(node.Less);
            }

            // Update maximum depth
            root.UpdateMaximumDepth();
            node.UpdateMaximumDepth();

            return node;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Create an Interval Binary Search Tree with a collection of intervals.
        /// </summary>
        /// <param name="intervals">The collection of intervals.</param>
        /// <param name="preconstructTree">True if a balanced tree structure should be preconstructed, before inserting the intervals.</param>
        public IntervalBinarySearchTreeRegular(IEnumerable<I> intervals, bool preconstructTree = true)
        {
            Contract.Requires(intervals != null);

            var intervalArray = intervals as I[] ?? intervals.ToArray();

            if (preconstructTree)
                preconstructNodeStructure(intervalArray);

            foreach (var interval in intervalArray)
                Add(interval);
        }

        private void preconstructNodeStructure(I[] intervals)
        {

            var intervalCount = intervals.Count();

            // Save all endpoints to array
            var endpoints = new T[intervalCount * 2];
            for (var i = 0; i < intervalCount; i++)
            {
                var interval = intervals[i];

                endpoints[i * 2] = interval.Low;
                endpoints[i * 2 + 1] = interval.High;
            }

            // Sort endpoints
            Sorting.IntroSort(endpoints);

            // Remove duplicate endpoints
            var uniqueEndpoints = new T[intervalCount * 2];
            var endpointCount = 0;

            foreach (var endpoint in endpoints)
                if (endpointCount == 0 || uniqueEndpoints[endpointCount - 1].CompareTo(endpoint) < 0)
                    uniqueEndpoints[endpointCount++] = endpoint;

            var height = 0;
            _root = createNodes(ref uniqueEndpoints, 0, endpointCount - 1, ref height);
        }

        private Node createNodes(ref T[] endpoints, int lower, int upper, ref int height)
        {
            if (lower > upper)
                return null;

            var mid = lower + (upper - lower >> 1);

            var node = new Node(endpoints[mid]);
            var leftHeight = 0;
            var rightHeight = 0;

            node.Left = createNodes(ref endpoints, lower, mid - 1, ref leftHeight);
            node.Right = createNodes(ref endpoints, mid + 1, upper, ref rightHeight);

            node.Balance = (sbyte) (rightHeight - leftHeight);

            height = Math.Max(leftHeight, rightHeight) + 1;

            return node;
        }

        /// <summary>
        /// Create empty Interval Binary Search Tree.
        /// </summary>
        public IntervalBinarySearchTreeRegular()
        {
        }

        #endregion

        #region Collection Value

        /// <inheritdoc/>
        public override bool IsEmpty
        {
            get
            {
                Contract.Ensures(Contract.Result<bool>() == (_count == 0));
                Contract.Ensures(Contract.Result<bool>() == (_root == null));
                return _count == 0;
            }
        }

        /// <inheritdoc/>
        public override int Count
        {
            get
            {
                Contract.Ensures(Contract.Result<int>() == _count);
                return _count;
            }
        }

        /// <inheritdoc/>
        public override Speed CountSpeed { get { return Speed.Constant; } }

        /// <inheritdoc/>
        public override I Choose()
        {
            if (IsEmpty)
                throw new NoSuchItemException();

            return _root.IntervalsEndingInNode.Choose();
        }

        #endregion

        #region Enumerable

        /// <inheritdoc/>
        public override IEnumerator<I>
            GetEnumerator()
        {
            var set = new IntervalSet();

            // TODO: Is Linq version lazy?
            foreach (var interval in intervals(_root).Where(set.Add))
                yield return interval;
        }

        /// <summary>
        /// Enumerates nodes in the tree of root and returns each interval in each interval set.
        /// </summary>
        /// <param name="root">The root of the subtree to traverse</param>
        /// <remarks>Is very likely to contain duplicates, as intervals are returned without any filtering!</remarks>
        /// <returns>An enumerable of intervals</returns>
        private static IEnumerable<I> intervals(Node root)
        {
            // TODO: Is this lazy?
            return nodes(root).SelectMany(node => node.IntervalsEndingInNode);
        }

        [Pure]
        private static IEnumerable<Node> nodes(Node root)
        {
            // TODO: Make fully iterative
            while (root != null)
            {
                foreach (var node in nodes(root.Left))
                    yield return node;

                yield return root;

                root = root.Right;
            }
        }

        #endregion

        #region Events

        /// <inheritdoc/>
        public override EventTypeEnum ListenableEvents { get { return EventTypeEnum.Basic; } }
        //public EventTypeEnum ActiveEvents { get; private set; }
        //public event CollectionChangedHandler<T> CollectionChanged;
        //public event CollectionClearedHandler<T> CollectionCleared;
        //public event ItemsAddedHandler<T> ItemsAdded;
        //public event ItemInsertedHandler<T> ItemInserted;
        //public event ItemsRemovedHandler<T> ItemsRemoved;
        //public event ItemRemovedAtHandler<T> ItemRemovedAt;

        #endregion

        #region Interval Collection

        #region Properties

        #region Span

        /// <inheritdoc/>
        public IInterval<T> Span
        {
            get
            {
                return _span ?? (_span = new IntervalBase<T>(span(_root, true), span(_root, false)));
            }
        }

        private static IInterval<T> span(Node root, bool findLow)
        {
            Contract.Requires(root != null);

            // Search for the lowest/highest node
            if (findLow)
                while (root.Left != null)
                    root = root.Left;
            else
                while (root.Right != null)
                    root = root.Right;

            // Check if the lowest/highest endpoint is included
            if (root.Equal != null && !root.Equal.IsEmpty)
                return root.Equal.Choose();

            // Otherwise pick an excluded one from intervals ending in node
            return root.IntervalsEndingInNode.Choose();
        }

        #endregion

        #region Maximum Depth

        /// <inheritdoc/>
        public int MaximumDepth
        {
            get { return _root != null ? _root.Max : 0; }
        }

        /// <summary>
        /// Recursively search for the split node, while updating the maximum depth on the way
        /// back if necessary.
        /// </summary>
        /// <param name="root">The root for the tree to search.</param>
        /// <param name="interval">The interval whose endpoints we search for.</param>
        /// <returns>True if we need to update the maximum depth for the parent node.</returns>
        private static bool updateMaximumDepth(Node root, IInterval<T> interval)
        {
            Contract.Requires(root != null);
            Contract.Requires(interval != null);

            // Search left for split node and update maximum depth if necessary
            if (interval.High.CompareTo(root.Key) < 0)
                return updateMaximumDepth(root.Left, interval) && root.UpdateMaximumDepth();

            // Search right for split node and update maximum depth if necessary
            if (interval.Low.CompareTo(root.Key) > 0)
                return updateMaximumDepth(root.Right, interval) && root.UpdateMaximumDepth();

            // Return true if maximum depth has changed for either endpoint
            return updateMaximumDepth(root, interval.High) | updateMaximumDepth(root, interval.Low);
        }

        private static bool updateMaximumDepth(Node root, T key)
        {
            Contract.Requires(root != null);

            var compare = key.CompareTo(root.Key);

            // Search left for key and update maximum depth if necessary
            if (compare < 0)
                return updateMaximumDepth(root.Left, key) && root.UpdateMaximumDepth();

            // Search right for key and update maximum depth if necessary
            if (compare > 0)
                return updateMaximumDepth(root.Right, key) && root.UpdateMaximumDepth();

            // Update maximum depth when low is found
            return root.UpdateMaximumDepth();
        }

        #endregion

        /// <inheritdoc/>
        public bool AllowsReferenceDuplicates { get { return false; } }

        #endregion

        #region Find Overlaps

        /// <inheritdoc/>
        public IEnumerable<I> FindOverlaps(T query)
        {
            // Break if the query is outside the collections span
            if (_span != null && !_span.Overlaps(query))
                return Enumerable.Empty<I>();

            return findOverlaps(_root, query);
        }

        private static IEnumerable<I> findOverlaps(Node root, T query)
        {
            // Search the tree until we reach the bottom of the tree    
            while (root != null)
            {
                // Store compare value as we need it twice
                var compare = query.CompareTo(root.Key);

                // Query is to the left of the current node
                if (compare < 0)
                {
                    // Return all intervals in Less
                    if (root.Less != null && !root.Less.IsEmpty)
                        foreach (var interval in root.Less)
                            yield return interval;

                    // Move left
                    root = root.Left;
                }
                // Query is to the right of the current node
                else if (compare > 0)
                {
                    // Return all intervals in Greater
                    if (root.Greater != null && !root.Greater.IsEmpty)
                        foreach (var interval in root.Greater)
                            yield return interval;

                    // Move right
                    root = root.Right;
                }
                // Node with query value found
                else
                {
                    // Return all intervals in Equal
                    if (root.Equal != null && !root.Equal.IsEmpty)
                        foreach (var interval in root.Equal)
                            yield return interval;

                    // Stop as the search is done
                    yield break;
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<I> FindOverlaps(IInterval<T> query)
        {
            // Break if the collection is empty or the query is outside the collections span
            if (IsEmpty || _span != null && !_span.Overlaps(query))
                yield break;

            var set = new IntervalSet();
            var splitNode = _root;

            // Use a lambda instead of out, as out or ref isn't allowed for iterators
            foreach (var interval in findSplitNode(_root, query, n => { splitNode = n; }).Where(set.Add))
                yield return interval;

            // Find all intersecting intervals in left subtree
            if (query.Low.CompareTo(splitNode.Key) < 0)
                foreach (var interval in findLeft(splitNode.Left, query).Where(set.Add))
                    yield return interval;

            // Find all intersecting intervals in right subtree
            if (splitNode.Key.CompareTo(query.High) < 0)
                foreach (var interval in findRight(splitNode.Right, query).Where(set.Add))
                    yield return interval;
        }

        /// <summary>
        /// Create an enumerable, enumerating all intersecting intervals on the path to the split node. Returns the split node with setSplitNode.
        /// </summary>
        private static IEnumerable<I> findSplitNode(Node root, IInterval<T> query, Action<Node> setSplitNode)
        {
            while (root != null)
            {
                // Update split node
                setSplitNode(root);

                // Interval is lower than root, go left
                var compareHigh = query.High.CompareTo(root.Key);
                int compareLow;

                if (compareHigh < 0)
                {
                    if (root.Less != null && !root.Less.IsEmpty)
                        foreach (var interval in root.Less)
                            yield return interval;

                    // Update root to left node
                    root = root.Left;
                }
                // Interval is higher than root, go right
                else if ((compareLow = query.Low.CompareTo(root.Key)) > 0)
                {
                    if (root.Greater != null && !root.Greater.IsEmpty)
                        foreach (var interval in root.Greater)
                            yield return interval;

                    // Update root to right node
                    root = root.Right;
                }
                // Otherwise add overlapping nodes in split node
                else
                {
                    if (root.Less != null && !root.Less.IsEmpty && compareLow < 0)
                        foreach (var interval in root.Less)
                            yield return interval;
                    if (root.Equal != null && !root.Equal.IsEmpty &&
                        ((compareLow < 0 || query.LowIncluded) && (compareHigh > 0 || query.HighIncluded)))
                        foreach (var interval in root.Equal)
                            yield return interval;
                    if (root.Greater != null && !root.Greater.IsEmpty && compareHigh > 0)
                        foreach (var interval in root.Greater)
                            yield return interval;

                    yield break;
                }
            }
        }

        private static IEnumerable<I> findLeft(Node root, IInterval<T> query)
        {
            while (root != null)
            {
                var compareTo = query.Low.CompareTo(root.Key);

                // Search in right subtree
                if (compareTo > 0)
                {
                    if (root.Greater != null && !root.Greater.IsEmpty)
                        foreach (var interval in root.Greater)
                            yield return interval;

                    // Iteratively travese right subtree
                    root = root.Right;
                }
                // Search in left subtree
                else if (compareTo < 0)
                {
                    foreach (var interval in root.IntervalsEndingInNode)
                        yield return interval;

                    // Recursively add all intervals in right subtree as they must be
                    // contained by (root.Key:splitNode.Key)
                    foreach (var interval in intervals(root.Right))
                        yield return interval;

                    // Iteratively travese left subtree
                    root = root.Left;
                }
                else
                {
                    // Add all intervals from greater
                    if (root.Greater != null && !root.Greater.IsEmpty)
                        foreach (var interval in root.Greater)
                            yield return interval;

                    // Add all intervals from Equal if query overlaps
                    if (query.LowIncluded && root.Equal != null && !root.Equal.IsEmpty)
                        foreach (var interval in root.Equal)
                            yield return interval;

                    // If we find the matching node, we can add everything in the left subtree
                    foreach (var interval in intervals(root.Right))
                        yield return interval;

                    yield break;
                }
            }
        }

        private static IEnumerable<I> findRight(Node root, IInterval<T> query)
        {
            // If root is null we have reached the end
            while (root != null)
            {
                var compareTo = query.High.CompareTo(root.Key);

                //
                if (compareTo < 0)
                {
                    // Add all intersecting intervals from left list
                    if (root.Less != null && !root.Less.IsEmpty)
                        foreach (var interval in root.Less)
                            yield return interval;

                    // Otherwise Recursively travese left subtree
                    root = root.Left;
                }
                //
                else if (compareTo > 0)
                {
                    // As our query interval contains the interval [root.Key:splitNode]
                    // all intervals in root can be returned without any checks
                    foreach (var interval in root.IntervalsEndingInNode)
                        yield return interval;

                    // Recursively add all intervals in right subtree as they must be
                    // contained by (splitNode.Key:root.Key)
                    foreach (var interval in intervals(root.Left))
                        yield return interval;

                    // Recursively travese left subtree
                    root = root.Right;
                }
                else
                {
                    // Add all intersecting intervals from left list
                    if (root.Less != null && !root.Less.IsEmpty)
                        foreach (var interval in root.Less)
                            yield return interval;

                    if (query.HighIncluded && root.Equal != null && !root.Equal.IsEmpty)
                        foreach (var interval in root.Equal)
                            yield return interval;

                    // If we find the matching node, we can add everything in the left subtree
                    foreach (var interval in intervals(root.Left))
                        yield return interval;

                    yield break;
                }
            }
        }

        #endregion

        #region Find Overlap

        /// <inheritdoc/>
        public bool FindOverlap(T query, ref I overlap)
        {
            bool result;

            using (var enumerator = FindOverlaps(query).GetEnumerator())
            {
                result = enumerator.MoveNext();

                if (result)
                    overlap = enumerator.Current;
            }

            return result;
        }

        /// <inheritdoc/>
        public bool FindOverlap(IInterval<T> query, ref I overlap)
        {
            bool result;

            using (var enumerator = FindOverlaps(query).GetEnumerator())
            {
                result = enumerator.MoveNext();

                if (result)
                    overlap = enumerator.Current;
            }

            return result;
        }

        #endregion

        #region Count Overlaps

        /// <inheritdoc/>
        public int CountOverlaps(T query)
        {
            // Break if the query is outside the collections span
            if (_span != null && !_span.Overlaps(query))
                return 0;

            return countOverlaps(query, _root);
        }

        private static int countOverlaps(T query, Node root)
        {
            var count = 0;

            // Search the tree until we reach the bottom of the tree    
            while (root != null)
            {
                // Store compare value as we need it twice
                var compare = query.CompareTo(root.Key);

                // Query is to the left of the current node
                if (compare < 0)
                {
                    // Add set count to total
                    if (root.Less != null)
                        count += root.Less.Count;

                    // Move left
                    root = root.Left;
                }
                // Query is to the right of the current node
                else if (compare > 0)
                {
                    // Add set count to total
                    if (root.Greater != null)
                        count += root.Greater.Count;

                    // Move right
                    root = root.Right;
                }
                // Node with query value found
                else
                {
                    // Add set count to total
                    if (root.Equal != null)
                        count += root.Equal.Count;

                    // Stop as the search is done
                    break;
                }
            }

            return count;
        }

        /// <inheritdoc/>
        public int CountOverlaps(IInterval<T> query)
        {
            return FindOverlaps(query).Count();
        }

        #endregion

        #region Extensible

        /// <inheritdoc/>
        public bool IsReadOnly { get { return false; } }

        #region Add

        /// <inheritdoc/>
        public bool Add(I interval)
        {
            // References to endpoint nodes needed when maintaining Interval
            Node lowNode = null, highNode = null;

            // Used to check if interval was actually added
            var intervalWasAdded = false;

            // Insert low endpoint
            var nodeWasAdded = false;
            _root = addLow(interval, _root, null, ref nodeWasAdded, ref intervalWasAdded, ref lowNode);

            // Insert high endpoint
            nodeWasAdded = false;
            _root = addHigh(interval, _root, null, ref nodeWasAdded, ref intervalWasAdded, ref highNode);

            // Increase counters and raise event if interval was added
            if (intervalWasAdded)
            {
                // Update span if necessary
                if (_span != null && !_span.Contains(interval))
                    _span = _span.JoinedSpan(interval);

                // Update maximum depth delta for low
                if (interval.LowIncluded)
                    lowNode.DeltaAt++;
                else
                    lowNode.DeltaAfter++;

                // Update maximum depth delta for high
                if (!interval.HighIncluded)
                    highNode.DeltaAt--;
                else
                    highNode.DeltaAfter--;

                // Update maximum depth
                updateMaximumDepth(_root, interval);

                lowNode.IntervalsEndingInNode.Add(interval);
                highNode.IntervalsEndingInNode.Add(interval);

                _count++;
                raiseForAdd(interval);
            }

            // TODO: Add event for change in maximum depth

            return intervalWasAdded;
        }

        /// <inheritdoc/>
        public void AddAll(IEnumerable<I> intervals)
        {
            // TODO: Look into bulk insertion
            foreach (var interval in intervals)
                Add(interval);
        }

        private static void addLow(I interval, Node root, Node rightUp)
        {
            var nodeWasAdded = false;
            var intervalWasAdded = false;
            Node lowNode = null;
            addLow(interval, root, rightUp, ref nodeWasAdded, ref intervalWasAdded, ref lowNode);
        }

        private static Node addLow(I interval, Node root, Node rightUp, ref bool nodeWasAdded, ref bool intervalWasAdded, ref Node lowNode)
        {
            Contract.Requires(interval != null);

            // No node existed for the low endpoint
            if (root == null)
            {
                root = new Node(interval.Low);
                nodeWasAdded = true;
                intervalWasAdded = true;
            }

            Contract.Assert(root != null);

            var compare = interval.Low.CompareTo(root.Key);

            if (compare > 0)
            {
                root.Right = addLow(interval, root.Right, rightUp, ref nodeWasAdded, ref intervalWasAdded, ref lowNode);

                // Adjust node balance, if node was added
                if (nodeWasAdded)
                    root.Balance++;
            }
            else if (compare < 0)
            {
                // Everything in the right subtree of root will lie within the interval
                if (rightUp != null && rightUp.Key.CompareTo(interval.High) <= 0)
                {
                    if (root.Greater == null)
                        root.Greater = new IntervalSet();

                    if (!(intervalWasAdded |= root.Greater.Add(interval)))
                        return root;
                }

                // root key is between interval.low and interval.high
                if (root.Key.CompareTo(interval.High) < 0)
                {
                    if (root.Equal == null)
                        root.Equal = new IntervalSet();

                    if (!(intervalWasAdded |= root.Equal.Add(interval)))
                        return root;
                }

                root.Left = addLow(interval, root.Left, root, ref nodeWasAdded, ref intervalWasAdded, ref lowNode);

                // Adjust node balance, if node was added
                if (nodeWasAdded)
                    root.Balance--;
            }
            else
            {
                // If everything in the right subtree of root will lie within the interval
                if (rightUp != null && rightUp.Key.CompareTo(interval.High) <= 0)
                {
                    if (root.Greater == null)
                        root.Greater = new IntervalSet();

                    if (!(intervalWasAdded |= root.Greater.Add(interval)))
                        return root;
                }

                if (interval.LowIncluded)
                {
                    if (root.Equal == null)
                        root.Equal = new IntervalSet();

                    if (!(intervalWasAdded |= root.Equal.Add(interval)))
                        return root;
                }

                // Save reference to endpoint node
                lowNode = root;
            }

            // Tree might be unbalanced after node was added, so we rotate
            if (nodeWasAdded && compare != 0)
                root = rotateForAdd(root, ref nodeWasAdded);

            return root;
        }

        private static void addHigh(I interval, Node root, Node leftUp)
        {
            var nodeWasAdded = false;
            var intervalWasAdded = false;
            Node highNode = null;
            addHigh(interval, root, leftUp, ref nodeWasAdded, ref intervalWasAdded, ref highNode);
        }

        private static Node addHigh(I interval, Node root, Node leftUp, ref bool nodeWasAdded, ref bool intervalWasAdded, ref Node highNode)
        {
            Contract.Requires(interval != null);

            // No node existed for the high endpoint
            if (root == null)
            {
                root = new Node(interval.High);
                nodeWasAdded = true;
                intervalWasAdded = true;
            }

            Contract.Assert(root != null);

            var compare = interval.High.CompareTo(root.Key);

            if (compare < 0)
            {
                root.Left = addHigh(interval, root.Left, leftUp, ref nodeWasAdded, ref intervalWasAdded, ref highNode);

                // Adjust node balance, if node was added
                if (nodeWasAdded)
                    root.Balance--;
            }
            else if (compare > 0)
            {
                // Everything in the right subtree of root will lie within the interval
                if (leftUp != null && leftUp.Key.CompareTo(interval.Low) >= 0)
                {
                    if (root.Less == null)
                        root.Less = new IntervalSet();

                    if (!(intervalWasAdded |= root.Less.Add(interval)))
                        return root;
                }

                // root key is between interval.low and interval.high
                if (root.Key.CompareTo(interval.Low) > 0)
                {
                    if (root.Equal == null)
                        root.Equal = new IntervalSet();

                    if (!(intervalWasAdded |= root.Equal.Add(interval)))
                        return root;
                }

                root.Right = addHigh(interval, root.Right, root, ref nodeWasAdded, ref intervalWasAdded, ref highNode);

                // Adjust node balance, if node was added
                if (nodeWasAdded)
                    root.Balance++;
            }
            else
            {
                // If everything in the right subtree of root will lie within the interval
                if (leftUp != null && leftUp.Key.CompareTo(interval.Low) >= 0)
                {
                    if (root.Less == null)
                        root.Less = new IntervalSet();

                    if (!(intervalWasAdded |= root.Less.Add(interval)))
                        return root;
                }

                if (interval.HighIncluded)
                {
                    if (root.Equal == null)
                        root.Equal = new IntervalSet();

                    if (!(intervalWasAdded |= root.Equal.Add(interval)))
                        return root;
                }

                // Save reference to endpoint node
                highNode = root;
            }

            // Tree might be unbalanced after node was added, so we rotate
            if (nodeWasAdded && compare != 0)
                root = rotateForAdd(root, ref nodeWasAdded);

            return root;
        }

        #endregion

        #region Remove

        /// <inheritdoc/>
        public bool Remove(I interval)
        {
            // Nothing to remove is the collection is empty or the interval doesn't overlap the span
            if (IsEmpty || !interval.Overlaps(Span))
                return false;

            // References to endpoint nodes needed when maintaining Interval
            Node lowNode = null, highNode = null;

            // Used to check if interval was actually added
            var intervalWasRemoved = false;

            // Remove low endpoint
            removeLow(interval, _root, null, ref intervalWasRemoved, ref lowNode);

            // Remove high endpoint
            removeHigh(interval, _root, null, ref intervalWasRemoved, ref highNode);

            // Increase counters and raise event if interval was removed
            if (intervalWasRemoved)
            {
                // Invalidate span if necessary
                if (!_span.StrictlyContains(interval))
                    _span = null;

                // Update maximum depth delta for low
                if (interval.LowIncluded)
                    lowNode.DeltaAt--;
                else
                    lowNode.DeltaAfter--;
                // Update maximum depth delta for high
                if (!interval.HighIncluded)
                    highNode.DeltaAt++;
                else
                    highNode.DeltaAfter++;

                lowNode.IntervalsEndingInNode.Remove(interval);
                highNode.IntervalsEndingInNode.Remove(interval);

                // Update maximum depth
                updateMaximumDepth(_root, interval);

                // Check for unnecessary endpoint nodes, if interval was actually removed
                if (lowNode.IntervalsEndingInNode.IsEmpty)
                {
                    var updateBalanace = false;
                    _root = removeNodeWithKey(interval.Low, _root, ref updateBalanace);

                    // Check that the node does not exist anymore
                    Contract.Assert(!Contract.Exists(nodes(_root), n => n.Key.Equals(interval.Low)));
                }

                // Skip if low and high are equal (true for point intervals)
                if (highNode.IntervalsEndingInNode.IsEmpty && lowNode != highNode)
                {
                    var updateBalanace = false;
                    _root = removeNodeWithKey(interval.High, _root, ref updateBalanace);

                    // Check that the node does not exist anymore
                    Contract.Assert(!Contract.Exists(nodes(_root), n => n.Key.Equals(interval.High)));
                }

                _count--;
                raiseForRemove(interval);
            }

            // TODO: Add event for change in maximum depth

            return intervalWasRemoved;
        }

        private static void removeLow(I interval, Node root, Node rightUp, ref bool intervalWasRemoved, ref Node lowNode)
        {
            Contract.Requires(!ReferenceEquals(interval, null));

            while (root != null)
            {
                var compare = interval.Low.CompareTo(root.Key);

                if (compare > 0)
                    root = root.Right;
                else if (compare < 0)
                {
                    // Everything in the right subtree of root will lie within the interval
                    if (rightUp != null && rightUp.Key.CompareTo(interval.High) <= 0)
                        if (root.Greater == null || !(intervalWasRemoved |= root.Greater.Remove(interval)))
                            return;

                    // root key is between interval.low and interval.high
                    if (root.Key.CompareTo(interval.High) < 0)
                        if (root.Equal == null || !(intervalWasRemoved |= root.Equal.Remove(interval)))
                            return;

                    rightUp = root;
                    root = root.Left;
                }
                else
                {
                    // If everything in the right subtree of root will lie within the interval
                    if (rightUp != null && rightUp.Key.CompareTo(interval.High) <= 0)
                        if (root.Greater == null || !(intervalWasRemoved |= root.Greater.Remove(interval)))
                            return;

                    if (interval.LowIncluded)
                        if (root.Equal == null || !(intervalWasRemoved |= root.Equal.Remove(interval)))
                            return;

                    // Save reference to endpoint node
                    lowNode = root;

                    break;
                }
            }
        }

        private static void removeHigh(I interval, Node root, Node leftUp, ref bool intervalWasRemoved, ref Node highNode)
        {
            // No node existed for the high endpoint
            while (root != null)
            {

                var compare = interval.High.CompareTo(root.Key);

                if (compare < 0)
                    root = root.Left;
                else if (compare > 0)
                {
                    // Everything in the right subtree of root will lie within the interval
                    if (leftUp != null && leftUp.Key.CompareTo(interval.Low) >= 0)
                        if (root.Less == null || !(intervalWasRemoved |= root.Less.Remove(interval)))
                            return;

                    // root key is between interval.low and interval.high
                    if (root.Key.CompareTo(interval.Low) > 0)
                        if (root.Equal == null || !(intervalWasRemoved |= root.Equal.Remove(interval)))
                            return;

                    leftUp = root;
                    root = root.Right;
                }
                else
                {
                    // If everything in the right subtree of root will lie within the interval
                    if (leftUp != null && leftUp.Key.CompareTo(interval.Low) >= 0)
                        if (root.Less == null || !(intervalWasRemoved |= root.Less.Remove(interval)))
                            return;

                    if (interval.HighIncluded)
                        if (root.Equal == null || !(intervalWasRemoved |= root.Equal.Remove(interval)))
                            return;

                    // Save reference to endpoint node
                    highNode = root;

                    break;
                }
            }
        }

        private static Node removeNodeWithKey(T key, Node root, ref bool updateBalance, Node leftUp = null, Node rightUp = null)
        {
            Contract.Requires(root != null);
            Contract.Requires(Contract.Exists(nodes(root), n => n.Key.Equals(key)));

            var compare = key.CompareTo(root.Key);

            // Remove node from right subtree
            if (compare > 0)
            {
                // Update left parent
                root.Right = removeNodeWithKey(key, root.Right, ref updateBalance, root, rightUp);

                if (updateBalance)
                    root.Balance--;
            }
            // Remove node from left subtree
            else if (compare < 0)
            {
                root.Left = removeNodeWithKey(key, root.Left, ref updateBalance, leftUp, root);

                if (updateBalance)
                    root.Balance++;
            }
            // Node found
            // Replace node with successor
            else if (root.Left != null && root.Right != null)
            {
                var successor = findSuccessor(root.Right);

                // Get intervals in successor
                var intervalsNeedingReinsertion = successor.IntervalsEndingInNode;

                // Remove marks for intervals in successor
                foreach (var interval in intervalsNeedingReinsertion)
                {
                    var intervalWasRemoved = false;
                    Node node = null;

                    if (leftUp == null || leftUp.Key.CompareTo(interval.Low) < 0)
                        removeLow(interval, root, rightUp, ref intervalWasRemoved, ref node);

                    if (rightUp == null || interval.High.CompareTo(rightUp.Key) < 0)
                        removeHigh(interval, root, leftUp, ref intervalWasRemoved, ref node);
                }

                // Swap root and successor nodes
                root.Swap(successor);

                updateMaximumDepth(root.Right, successor.Key);

                // Remove the successor node
                updateBalance = false;
                root.Right = removeNodeWithKey(successor.Key, root.Right, ref updateBalance, leftUp, rightUp);

                if (updateBalance)
                    root.Balance--;

                // Reinsert marks for intervals in successor
                foreach (var interval in intervalsNeedingReinsertion)
                {
                    if (leftUp == null || leftUp.Key.CompareTo(interval.Low) < 0)
                        addLow(interval, root, rightUp);
                    if (rightUp == null || interval.High.CompareTo(rightUp.Key) < 0)
                        addHigh(interval, root, leftUp);
                }

                root.UpdateMaximumDepth();
            }
            else
            {
                updateBalance = true;

                // Return Left if not null, otherwise Right
                return root.Left ?? root.Right;
            }

            if (updateBalance)
                root = rotateForRemove(root, ref updateBalance);

            return root;
        }

        /// <summary>
        /// Find the successor node.
        /// </summary>
        /// <returns>The successor node.</returns>
        private static Node findSuccessor(Node node)
        {
            Contract.Requires(node != null);
            Contract.Ensures(Contract.Result<Node>() != null);
            Contract.Ensures(Contract.Result<Node>() == nodes(Contract.OldValue(node)).First());

            while (node.Left != null)
                node = node.Left;

            return node;
        }

        #endregion

        #region Clear

        /// <inheritdoc/>
        public void Clear()
        {
            // Return if tree is empty
            if (IsEmpty)
                return;

            // Save old count and reset all values
            var oldCount = _count;
            clear();

            // Raise events
            if ((ActiveEvents & EventTypeEnum.Cleared) != 0)
                raiseCollectionCleared(true, oldCount);
            if ((ActiveEvents & EventTypeEnum.Changed) != 0)
                raiseCollectionChanged();
        }

        private void clear()
        {
            _root = null;
            _count = 0;
        }

        #endregion

        #endregion

        #endregion
    }
}