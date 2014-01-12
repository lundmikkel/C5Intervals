﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using C5.Intervals;
using NUnit.Framework;

namespace C5.Intervals.Tests
{
    internal class Interval : TestInterval<int>
    {
        public Interval(int query)
            : base(query)
        {
        }

        public Interval(int low, int high, bool lowIncluded = true, bool highIncluded = false)
            : base(low, high, lowIncluded, highIncluded)
        {
        }

        public Interval(int low, int high, IntervalType type)
            : base(low, high, type)
        {
        }

        public Interval(IInterval<int> i)
            : base(i)
        {
        }

        public Interval(IInterval<int> low, IInterval<int> high)
            : base(low, high)
        {
        }

        public override string ToString()
        {
            return String.Format("{0}{1:#,0} : {2:#,0}{3}",
                LowIncluded ? "[" : "(",
                Low,
                High,
                HighIncluded ? "]" : ")");
        }
    }

    internal class TestInterval<T> : IntervalBase<T> where T : IComparable<T>
    {
        public int Id { get; private set; }

        private int randomInt()
        {
            var bytes = new byte[4];
            System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        public TestInterval(T query)
            : base(query)
        {
            Id = randomInt();
        }

        public TestInterval(T low, T high, bool lowIncluded = true, bool highIncluded = false)
            : base(low, high, lowIncluded, highIncluded)
        {
            Id = randomInt();
        }

        public TestInterval(T low, T high, IntervalType type)
            : base(low, high, type)
        {
            Id = randomInt();
        }

        public TestInterval(IInterval<T> i)
            : base(i)
        {
            Id = randomInt();
        }

        public TestInterval(IInterval<T> low, IInterval<T> high)
            : base(low, high)
        {
            Id = randomInt();
        }
    }

    [TestFixture]
    abstract class IntervalCollectionTester
    {
        /*
         * Things to check for for each method:
         * [ ] Empty collection                                 (EmptyCollection)
         * [ ] Single interval collection                       (SingleInterval)
         * [ ] Many intervals collection - all same object      (SingleObject)
         * [ ] Many intervals collection - all same interval    (DuplicateIntervals)
         * [ ] Many intervals collection                        (ManyIntervals)
         */

        #region Meta

        private Random Random { get; set; }

        private int Count { get; set; }

        [SetUp]
        public void SetUp()
        {
            var bytes = new byte[4];
            System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(bytes);
            var seed = BitConverter.ToInt32(bytes, 0);

            updateRandom(seed);
        }

        private void updateRandom(int seed)
        {
            Random = new Random(seed);
            Console.Out.WriteLine("Seed: {0}", seed);

            Count = Random.Next(10, 20);
        }

        private int randomInt()
        {
            return Random.Next(Int32.MinValue, Int32.MaxValue);
        }

        public Interval SingleInterval()
        {
            var low = Random.Next(Int32.MinValue, Int32.MaxValue - 3);
            var high = Random.Next(low + 2, Int32.MaxValue);

            return new Interval(low, high, (IntervalType) Random.Next(0, 4));
        }

        public Interval[] ManyIntervals(int count = -1)
        {
            Contract.Ensures(Contract.Result<IEnumerable<IInterval<int>>>().Count() == Count);

            if (count < 0)
                count = Count;

            return Enumerable.Range(0, count).Select(i => SingleInterval()).ToArray();
        }

        public Interval[] NonOverlapping(IEnumerable<Interval> intervals)
        {
            var enumerator = intervals.OrderBy(x => x, IntervalExtensions.CreateComparer<Interval, int>()).GetEnumerator();
            enumerator.MoveNext();

            var list = new List<Interval> { enumerator.Current };
            // Save the first interval to list and as previous
            var previous = enumerator.Current;

            while (enumerator.MoveNext())
            {
                var interval = enumerator.Current;

                // Check if interval overlaps the previous interval
                if (interval.Overlaps(previous))
                    // If overlaps should be disregarded skip to the next one
                    continue;

                // Add the interval and store it as the previous
                list.Add(interval);
                previous = interval;
            }

            // TODO: Fix when default behavior has been decided on
            // foreach (var interval in intervals.Where(interval => !list.Any(x => x.Overlaps(interval))))

            return list.ToArray();
        }

        private Interval[] MeetingIntervals(int count = 101, int length = 10)
        {
            var intervals = new Interval[count];

            var lastInterval = intervals[0] = new Interval(0, Random.Next(1, 1 + length), (IntervalType) Random.Next(0, 4));
            for (var i = 1; i < count; i++)
            {
                lastInterval = intervals[i] = new Interval(lastInterval.High, Random.Next(lastInterval.High + 1, lastInterval.High + 1 + length), !lastInterval.HighIncluded, Convert.ToBoolean(Random.Next(0, 2)));
            }

            return intervals;
        }

        private static void SingleIntervalEquals(Interval query, IEnumerable<IInterval<int>> gaps)
        {
            var enumerator = gaps.GetEnumerator();
            Assert.True(enumerator.MoveNext());
            Assert.True(query.IntervalEquals(enumerator.Current));
            Assert.False(enumerator.MoveNext());
        }

        private Interval[] Normalize(Interval[] intervals)
        {
            var endpoints = intervals.UniqueEndpointValues();
            var map = new HashDictionary<int, int>();
            var counter = 0;
            foreach (var endpoint in endpoints)
                map.Add(endpoint, counter++);

            var normalizedIntervals = new Interval[intervals.Length];
            for (int i = 0; i < intervals.Length; i++)
            {
                var interval = intervals[i];

                normalizedIntervals[i] = new Interval(map[interval.Low], map[interval.High]);
            }
            return normalizedIntervals;
        }

        public Interval[] DuplicateIntervals()
        {
            var interval = SingleInterval();
            return Enumerable.Range(0, Count).Select(i => new Interval(interval)).ToArray();
        }

        public Interval[] SingleObject()
        {
            var interval = SingleInterval();
            return Enumerable.Range(0, Count).Select(i => interval).ToArray();
        }

        public Interval SinglePoint()
        {
            return new Interval(Random.Next(Int32.MinValue, Int32.MaxValue));
        }

        public static Interval[] NonOverlappingIntervals(int count, int length = 1, int space = 0)
        {
            Contract.Ensures(Contract.ForAll(0, count, i => Contract.ForAll(i, count, j => !Contract.Result<IInterval<int>[]>()[i].Overlaps(Contract.Result<IInterval<int>[]>()[j]))));

            var intervals = new Interval[count];

            var low = 0;
            for (var i = 0; i < count; i++)
            {
                intervals[i] = new Interval(low, low + length, IntervalType.LowIncluded);
                low += length + space;
            }

            return intervals;
        }

        protected abstract Type GetCollectionType();

        /// <summary>
        /// Override to add additional parameters to constructors.
        /// </summary>
        /// <returns>An object array of the extra parameters.</returns>
        protected virtual object[] AdditionalParameters()
        {
            Console.Out.WriteLine("No additional parameters.");
            return new object[0];
        }

        protected IIntervalCollection<I, T> CreateCollection<I, T>(params I[] intervals)
            where I : IInterval<T>
            where T : IComparable<T>
        {
            var additionalParameters = AdditionalParameters();
            var parameters = new object[1 + additionalParameters.Length];
            parameters[0] = intervals;
            for (var i = 0; i < additionalParameters.Length; i++)
                parameters[i + 1] = additionalParameters[i];

            // Check that test class name matches implementation
            var type = GetCollectionType();
            var className = GetType().Name.Substring(0, GetType().Name.IndexOf("Tester"));
            if (!type.Name.StartsWith(className))
                throw new ArgumentException("The class name does not match the type of the class!");

            Type[] typeArgs = { typeof(I), typeof(T) };
            var genericType = type.MakeGenericType(typeArgs);
            return (IIntervalCollection<I, T>) Activator.CreateInstance(genericType, parameters);
        }

        protected IIntervalCollection<I, T> CreateEmptyCollection<I, T>()
            where I : IInterval<T>
            where T : IComparable<T>
        {
            return CreateCollection<I, T>(new I[0]);
        }

        #endregion

        #region Test Methods

        #region Code Contracts

        [Test]
        [Category("Code Contracts")]
        public void CodeContracts_VerifyPreconditionsAreInAssembly_ContractRuntimeContractException()
        {
            const string contractExceptionName = "System.Diagnostics.Contracts.__ContractsRuntime+ContractException";
            var collection = CreateEmptyCollection<IInterval<int>, int>();

            try
            {
                var overlaps = collection.FindOverlaps(null);
            }
            catch (Exception e)
            {
                if (e.GetType().FullName != contractExceptionName)
                    throw;

                Assert.Pass();
                return;
            }

            Assert.Fail();
        }

        [Test]
        [Category("Code Contracts")]
        public void CodeContracts_VerifyPostconditionsAreInDebugAssembly_ContractRuntimeContractException()
        {
#if DEBUG
            /*
            const string contractExceptionName = "System.Diagnostics.Contracts.__ContractsRuntime+ContractException";

            try
            {
                var sum = CodeContract_EnsureFails();
            }
            catch (Exception e)
            {
                if (e.GetType().FullName != contractExceptionName)
                    throw;

                Assert.Pass();
                return;
            }

            Assert.Fail("Post conditions not activated in debug!");
            */
#else
            CodeContract_EnsureFails();
#endif
        }

        public int CodeContract_EnsureFails()
        {
            Contract.Ensures(false);
            return -1;
        }

        #endregion

        #region Collection Value

        #region IsEmpty

        [Test]
        [Category("IsEmpty")]
        public void IsEmpty_EmptyCollection_Empty()
        {
            var collection = CreateEmptyCollection<IInterval<int>, int>();
            Assert.True(collection.IsEmpty);
        }

        [Test]
        [Category("IsEmpty")]
        public void IsEmpty_SingleInterval_NotEmpty()
        {
            var interval = SingleInterval();
            var collection = CreateCollection<Interval, int>(interval);
            Assert.False(collection.IsEmpty);
        }

        [Test]
        [Category("IsEmpty")]
        public void IsEmpty_SingleObject_NotEmpty()
        {
            var intervals = SingleObject();
            var collection = CreateCollection<Interval, int>(intervals);
            Assert.False(collection.IsEmpty);
        }

        [Test]
        [Category("IsEmpty")]
        public void IsEmpty_DuplicateIntervals_NotEmpty()
        {
            var intervals = DuplicateIntervals();
            var collection = CreateCollection<Interval, int>(intervals);
            Assert.False(collection.IsEmpty);
        }

        [Test]
        [Category("IsEmpty")]
        public void IsEmpty_ManyIntervals_NotEmpty()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);
            Assert.False(collection.IsEmpty);
        }

        #endregion

        #region Count

        [Test]
        [Category("Count")]
        public void Count_EmptyCollection_Zero()
        {
            var collection = CreateEmptyCollection<Interval, int>();

            Assert.AreEqual(0, collection.Count);
        }

        [Test]
        [Category("Count")]
        public void Count_SingleInterval_One()
        {
            var interval = SingleInterval();
            var collection = CreateCollection<Interval, int>(interval);

            Assert.AreEqual(1, collection.Count);
        }

        [Test]
        [Category("Count")]
        public void Count_SingleObject_CountOrOne()
        {
            var intervals = SingleObject();
            var collection = CreateCollection<Interval, int>(intervals);

            Assert.AreEqual(collection.AllowsReferenceDuplicates ? Count : 1, collection.Count);
        }

        [Test]
        [Category("Count")]
        public void Count_DuplicateIntervals_Count()
        {
            var intervals = DuplicateIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            Assert.AreEqual(collection.AllowsOverlaps ? Count : 1, collection.Count);
        }

        [Test]
        [Category("Count")]
        public void Count_ManyIntervals_Count_FixedSeed()
        {
            updateRandom(-623960470);
            Count_ManyIntervals_Count();
        }

        [Test]
        [Category("Count")]
        public void Count_ManyIntervals_Count()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            Assert.AreEqual(collection.AllowsOverlaps ? Count : NonOverlapping(intervals).Count(), collection.Count);
        }

        #endregion

        #region CountSpeed

        protected abstract Speed CountSpeed();

        [Test]
        [Category("Count Speed")]
        public void CountSpeed_VerifyCountSpeed_EqualToStated()
        {
            var collection = CreateEmptyCollection<Interval, int>();
            Assert.AreEqual(CountSpeed(), collection.CountSpeed);
        }

        #endregion

        #region Choose

        [Test]
        [Category("Choose")]
        [ExpectedException(typeof(NoSuchItemException))]
        public void Choose_EmptyCollection_Exception()
        {
            var interval = CreateEmptyCollection<Interval, int>().Choose();
        }

        [Test]
        [Category("Choose")]
        public void Choose_SingleInterval_IntervalIsSameAsChoose()
        {
            var interval = SingleInterval();
            var collection = CreateCollection<Interval, int>(interval);
            var choose = collection.Choose();

            Assert.AreSame(interval, choose);
        }

        [Test]
        [Category("Choose")]
        public void Choose_SingleObject_IntervalsAreSameAsChoose()
        {
            var intervals = SingleObject();
            var collection = CreateCollection<Interval, int>(intervals);
            var choose = collection.Choose();

            Assert.True(intervals.All(x => ReferenceEquals(x, choose)));
        }

        [Test]
        [Category("Choose")]
        public void Choose_DuplicateIntervals_OneIntervalIsSameAsChoose()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);
            var choose = collection.Choose();

            Assert.AreEqual(1, intervals.Count(x => ReferenceEquals(x, choose)));
        }

        [Test]
        [Category("Choose")]
        public void Choose_ManyIntervals_OneIntervalIsSameAsChoose()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);
            var choose = collection.Choose();

            Assert.AreEqual(1, intervals.Count(x => ReferenceEquals(x, choose)));
        }

        #endregion

        #endregion

        #region Enumerable

        [Test]
        [Category("Enumerable")]
        public void Enumerable_EmptyCollection_Empty()
        {
            var collection = CreateEmptyCollection<Interval, int>();

            CollectionAssert.IsEmpty(collection);
        }

        [Test]
        [Category("Enumerable")]
        public void Enumerable_SingleInterval_AreEqual()
        {
            var interval = SingleInterval();
            var collection = CreateCollection<Interval, int>(interval);

            CollectionAssert.AreEqual(new[] { interval }, collection);
        }

        [Test]
        [Category("Enumerable")]
        public void Enumerable_SingleObject_AreEquivalent()
        {
            var intervals = SingleObject();
            var collection = CreateCollection<Interval, int>(intervals);

            if (collection.AllowsReferenceDuplicates)
                CollectionAssert.AreEquivalent(intervals, collection);
            else
                CollectionAssert.AreEquivalent(new[] { intervals.First() }, collection);
        }

        [Test]
        [Category("Enumerable")]
        public void Enumerable_DuplicateIntervals_AreEquivalent()
        {
            var intervals = DuplicateIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            CollectionAssert.AreEquivalent(collection.AllowsOverlaps ? intervals : new[] { intervals.First() }, collection);
        }

        [Test]
        [Category("Enumerable")]
        public void Enumerable_ManyIntervals_AreEquivalent()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            CollectionAssert.AreEquivalent(collection.AllowsOverlaps ? intervals : NonOverlapping(intervals), collection);
            CollectionAssert.AllItemsAreUnique(collection);
        }

        #endregion

        #region Interval Collection

        #region Properties

        #region Span

        [Test]
        public void Span_EmptyCollection_Exception_FixedSeed()
        {
            updateRandom(-1498148951);
            Span_EmptyCollection_Exception();
        }

        [Test]
        [Category("Span")]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Span_EmptyCollection_Exception()
        {
            const string contractExceptionName = "System.Diagnostics.Contracts.__ContractsRuntime+ContractException";
            var collection = CreateEmptyCollection<IInterval<int>, int>();

            try
            {
                var span = CreateEmptyCollection<Interval, int>().Span;
            }
            catch (Exception e)
            {
                if (e.GetType().FullName != contractExceptionName)
                    throw;

                Assert.Pass();
                return;
            }

            Assert.Fail();
        }

        [Test]
        [Category("Span")]
        public void Span_SingleInterval_IntervalEqualsSpan()
        {
            var interval = SingleInterval();
            var collection = CreateCollection<Interval, int>(interval);
            var span = collection.Span;

            Assert.True(span.IntervalEquals(interval));
            Assert.True(collection.All(span.Contains));
        }

        [Test]
        [Category("Span")]
        public void Span_ManyIntervals_JoinedSpan()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);
            var span = (collection.AllowsOverlaps ? intervals : NonOverlapping(intervals)).Span();

            Assert.True(span.IntervalEquals(collection.Span));
            Assert.True(collection.All(span.Contains));
        }

        [Test]
        [Category("Span")]
        public void Span_NonOverlappingIntervals_JoinedSpan()
        {
            // TODO: Refactor?
            Interval interval1, interval2;
            do
            {
                interval1 = SingleInterval();
                interval2 = SingleInterval();
            } while (interval1.Overlaps(interval2));

            var joinedSpan = interval1.JoinedSpan(interval2);
            var collection = CreateCollection<Interval, int>(
                    interval1,
                    interval2
                );
            var span = collection.Span;

            Assert.True(joinedSpan.IntervalEquals(span));
            Assert.True(collection.All(span.Contains));
        }

        [Test]
        [Category("Span")]
        public void Span_ContainedInterval_ContainerEqualsSpan_FixedSeed()
        {
            updateRandom(-1672943992);
            Span_ContainedInterval_ContainerEqualsSpan();
        }

        [Test]
        [Category("Span")]
        public void Span_ContainedInterval_ContainerEqualsSpan()
        {
            // TODO: Refactor?
            Interval interval1, interval2;
            do
            {
                interval1 = SingleInterval();
                interval2 = SingleInterval();
            } while (!interval1.StrictlyContains(interval2));

            var collection = CreateCollection<Interval, int>(
                    interval1,
                    interval2
                );
            var span = collection.Span;

            Assert.True(interval1.IntervalEquals(span));
        }

        #endregion

        #region Maximum Depth

        [Test]
        [Category("Maximum Depth")]
        public void MaximumDepth_EmptyCollection_Zero()
        {
            var collection = CreateEmptyCollection<Interval, int>();

            Assert.AreEqual(0, collection.MaximumDepth);
        }

        [Test]
        [Category("Maximum Depth")]
        public void MaximumDepth_SingleInterval_One()
        {
            var interval = SingleInterval();
            var collection = CreateCollection<Interval, int>(interval);

            Assert.AreEqual(1, collection.MaximumDepth);
        }

        [Test]
        [Category("Maximum Depth")]
        public void MaximumDepth_SingleObject_CountOrOne()
        {
            var intervals = SingleObject();
            var collection = CreateCollection<Interval, int>(intervals);

            Assert.AreEqual(collection.AllowsReferenceDuplicates ? Count : 1, collection.MaximumDepth);
        }

        [Test]
        [Category("Maximum Depth")]
        public void MaximumDepth_DuplicateIntervals_Count()
        {
            var intervals = DuplicateIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            Assert.AreEqual(collection.AllowsOverlaps ? Count : NonOverlapping(intervals).Count(), collection.MaximumDepth);
        }

        [Test]
        [Category("Maximum Depth")]
        public void MaximumDepth_NonOverlappingIntervals_One()
        {
            var intervals = NonOverlappingIntervals(Count);
            var collection = CreateCollection<Interval, int>(intervals);

            Assert.AreEqual(1, collection.MaximumDepth);
        }

        [Test]
        [Category("Maximum Depth")]
        public void MaximumDepth_BetweenDescreteValues_Two()
        {
            var intervals = new[]
                {
                    new Interval(1, 3, IntervalType.Open),
                    new Interval(2, 4, IntervalType.Open)
                };
            var collection = CreateCollection<Interval, int>(intervals);

            Assert.AreEqual(collection.AllowsOverlaps ? 2 : 1, collection.MaximumDepth);
        }

        [Test]
        [Category("Maximum Depth")]
        public void MaximumDepth_ManyOverlappingIntervals_Four()
        {
            // 0    5   10   15
            //             |
            //          (--]
            //          (--)
            //          |
            //        []
            //        |
            //      [---]
            //     |
            //    |
            //   |
            //  |
            // |
            // [--------------]
            var intervals = new[]
                {
                    new Interval(12),
                    new Interval(9, 12, IntervalType.HighIncluded),
                    new Interval(9, 12, IntervalType.Open),
                    new Interval(9),
                    new Interval(7, 8, IntervalType.Closed),
                    new Interval(7),
                    new Interval(5, 9, IntervalType.Closed),
                    new Interval(4),
                    new Interval(3),
                    new Interval(2),
                    new Interval(1),
                    new Interval(0),
                    new Interval(0, 15, IntervalType.Closed)
                };
            var collection = CreateCollection<Interval, int>(intervals);

            Assert.AreEqual(collection.AllowsOverlaps ? 4 : 1, collection.MaximumDepth);
        }

        [Test]
        [Category("Maximum Depth")]
        public void MaximumDepth_AllContainedIntervals_Count()
        {
            // 0    5   10   15   20
            // 
            //          [-]
            //         [---]
            //        [-----]
            //       [-------]
            //      [---------]
            //     [-----------]
            //    [-------------]
            //   [---------------]
            //  [-----------------]
            // [-------------------]
            var intervals = Enumerable.Range(0, 10).Select(i => new Interval(i, 20 - i, IntervalType.Closed)).ToArray();
            var collection = CreateCollection<Interval, int>(intervals);

            Assert.AreEqual(collection.Count, collection.MaximumDepth);
        }

        #endregion

        #region Allows Overlaps
        // TODO
        #endregion

        #region Allows Reference Duplicates

        protected abstract bool AllowsReferenceDuplicates();

        [Test]
        [Category("Allows Reference Duplicates")]
        public void AllowsReferenceDuplicates_EmptyCollection_DefinedResult()
        {
            var collection = CreateEmptyCollection<Interval, int>();

            Assert.AreEqual(AllowsReferenceDuplicates(), collection.AllowsReferenceDuplicates);
        }

        [Test]
        [Category("Allows Reference Duplicates")]
        public void AllowsReferenceDuplicates_SingleInterval_DefinedResult()
        {
            var interval = SingleInterval();
            var collection = CreateCollection<Interval, int>(interval);

            Assert.AreEqual(AllowsReferenceDuplicates(), collection.AllowsReferenceDuplicates);
        }

        [Test]
        [Category("Allows Reference Duplicates")]
        public void AllowsReferenceDuplicates_ManyIntervals_DefinedResult()
        {
            var interval = ManyIntervals();
            var collection = CreateCollection<Interval, int>(interval);

            Assert.AreEqual(AllowsReferenceDuplicates(), collection.AllowsReferenceDuplicates);
        }

        #endregion

        #region Sorted

        [Test]
        [Category("Sorted")]
        public void Sorted_LCListTrickyCase_Sorted()
        {
            var collection = CreateCollection<Interval, int>(
                new Interval(0, 8),
                new Interval(1, 7),
                new Interval(2, 3),
                new Interval(4, 9),
                new Interval(5, 6)
            );

            Assert.True(collection.Sorted.IsSorted(IntervalExtensions.CreateComparer<Interval, int>()));
        }

        [Test]
        [Category("Sorted")]
        public void Sorted_ManyIntervals_Sorted_FixedSeed()
        {
            updateRandom(36342054);
            Sorted_ManyIntervals_Sorted();
            updateRandom(-1127807792);
            Sorted_ManyIntervals_Sorted();
        }

        [Test]
        [Category("Sorted")]
        public void Sorted_ManyIntervals_Sorted()
        {
            var intervals = Normalize(ManyIntervals());
            var collection = CreateCollection<Interval, int>(intervals);

            Assert.AreEqual(collection.Count, collection.Sorted.Count());
            Assert.True(collection.Sorted.IsSorted(IntervalExtensions.CreateComparer<Interval, int>()));
        }

        #endregion

        #endregion

        #region Find Overlaps

        #region Stabbing

        [Test]
        [Category("Find Overlaps Stabbing")]
        public void FindOverlapsStabbing_EmptyCollection_Empty()
        {
            var query = randomInt();
            var collection = CreateEmptyCollection<Interval, int>();

            CollectionAssert.IsEmpty(collection.FindOverlaps(query));
        }

        [Test]
        [Category("Find Overlap Stabbing")]
        public void FindOverlapsStabbing_SingleInterval_SingleOverlap()
        {
            var interval = SingleInterval();
            int query;
            do query = randomInt(); while (interval.Overlaps(query));
            var collection = CreateCollection<Interval, int>(interval);

            CollectionAssert.IsEmpty(collection.FindOverlaps(query));

            if (interval.LowIncluded)
                CollectionAssert.AreEqual(new[] { interval }, collection.FindOverlaps(interval.Low));
            else
                CollectionAssert.IsEmpty(collection.FindOverlaps(interval.Low));

            if (interval.HighIncluded)
                CollectionAssert.AreEqual(new[] { interval }, collection.FindOverlaps(interval.High));
            else
                CollectionAssert.IsEmpty(collection.FindOverlaps(interval.High));

            CollectionAssert.AreEquivalent(new[] { interval }, collection.FindOverlaps(interval.Low / 2 + interval.High / 2));
        }

        [Test]
        [Category("Find Overlap Stabbing")]
        public void FindOverlapsStabbing_SingleObject_CountOrSingleOverlap()
        {
            var intervals = SingleObject();
            var interval = intervals[0];
            int query;
            do query = randomInt(); while (interval.Overlaps(query));
            var collection = CreateCollection<Interval, int>(intervals);
            var overlaps = collection.AllowsReferenceDuplicates ? intervals : new[] { interval };

            CollectionAssert.IsEmpty(collection.FindOverlaps(query));

            if (interval.LowIncluded)
                CollectionAssert.AreEquivalent(overlaps, collection.FindOverlaps(interval.Low));
            else
                CollectionAssert.IsEmpty(collection.FindOverlaps(interval.Low));

            if (interval.HighIncluded)
                CollectionAssert.AreEquivalent(overlaps, collection.FindOverlaps(interval.High));
            else
                CollectionAssert.IsEmpty(collection.FindOverlaps(interval.High));

            CollectionAssert.AreEquivalent(overlaps, collection.FindOverlaps(interval.Low / 2 + interval.High / 2));
        }

        [Test]
        [Category("Find Overlap Stabbing")]
        public void FindOverlapsStabbing_DuplicateIntervals_CountOverlaps()
        {
            var intervals = DuplicateIntervals();
            var interval = intervals[0];
            int query;
            do query = randomInt(); while (interval.Overlaps(query));
            var collection = CreateCollection<Interval, int>(intervals);

            CollectionAssert.IsEmpty(collection.FindOverlaps(query));

            if (interval.LowIncluded)
                CollectionAssert.AreEquivalent(collection.AllowsOverlaps ? intervals : new[] { intervals.First() }, collection.FindOverlaps(interval.Low));
            else
                CollectionAssert.IsEmpty(collection.FindOverlaps(interval.Low));

            if (interval.HighIncluded)
                CollectionAssert.AreEquivalent(collection.AllowsOverlaps ? intervals : new[] { intervals.First() }, collection.FindOverlaps(interval.High));
            else
                CollectionAssert.IsEmpty(collection.FindOverlaps(interval.High));

            CollectionAssert.AreEquivalent(collection.AllowsOverlaps ? intervals : new[] { intervals.First() }, collection.FindOverlaps(interval.Low / 2 + interval.High / 2));
        }

        [Test]
        [Category("Find Overlap Stabbing")]
        public void FindOverlapsStabbing_ManyIntervals_AtLeastOneOverlap_FixedSeed()
        {
            updateRandom(1641746101);
            FindOverlapsStabbing_ManyIntervals_AtLeastOneOverlap();
        }

        [Test]
        [Category("Find Overlap Stabbing")]
        public void FindOverlapsStabbing_ManyIntervals_AtLeastOneOverlap()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            foreach (var interval in intervals)
            {
                CollectionAssert.AreEquivalent((collection.AllowsOverlaps ? intervals : NonOverlapping(intervals)).Where(x => x.Overlaps(interval.Low)), collection.FindOverlaps(interval.Low));
                CollectionAssert.AreEquivalent((collection.AllowsOverlaps ? intervals : NonOverlapping(intervals)).Where(x => x.Overlaps(interval.High)), collection.FindOverlaps(interval.High));
            }
        }

        [Test]
        [Category("Find Overlaps Stabbing")]
        public void FindOverlapsStabbing_SingleIntervalAllEndpointCombinations_Overlaps()
        {
            var interval = SingleInterval();
            var intervals = Enumerable.Range(0, 4).Select(i => new Interval(interval.Low, interval.High, (IntervalType) i)).ToArray();
            var collection = CreateCollection<Interval, int>(intervals);

            if (!collection.AllowsOverlaps)
                intervals = NonOverlapping(intervals);

            CollectionAssert.AreEquivalent(intervals.Where(x => x.LowIncluded), collection.FindOverlaps(interval.Low));
            CollectionAssert.AreEquivalent(intervals.Where(x => x.HighIncluded), collection.FindOverlaps(interval.High));
            CollectionAssert.AreEquivalent(intervals, collection.FindOverlaps(interval.Low / 2 + interval.High / 2));
        }

        #endregion

        #region Range

        [Test]
        [Category("Find Overlaps Range")]
        public void FindOverlapsRange_EmptyCollection_Empty()
        {
            var query = SingleInterval();
            var collection = CreateEmptyCollection<Interval, int>();

            CollectionAssert.IsEmpty(collection.FindOverlaps(query));
        }

        [Test]
        [Category("Find Overlaps Range")]
        public void FindOverlapsRange_BensTest_FixedSeed()
        {
            updateRandom(-1166356094);
            FindOverlapsRange_BensTest();
        }

        [Test]
        [Category("Find Overlaps Range")]
        public void FindOverlapsRange_BensTest()
        {
            // ****************************************
            // | 0    5   10   15   20   25   30   35 |
            // | |    |    |    |    |    |    |    | |
            // | Container intervals:                 |
            // |                           [E  ]      |
            // |                     [D  ]            |
            // |                [C   ]                |
            // |            [B  ]                     |
            // |      [A  ]                           |
            // |                                      |
            // |                                      |
            // | Test intervals:                      |
            // |                                [   ] |
            // |                               [    ] |
            // |                            [ ]       |
            // |                           [        ] |
            // |                           [   ]      |
            // |                          []          |
            // |                         [ ]          |
            // |                     [         ]      |
            // |           []                         |
            // |           |                          |
            // |          [                ]          |
            // |          [ ]                         |
            // |          []                          |
            // |       [ ]                            |
            // |      [                        ]      |
            // |      [         )                     |
            // |      [   ]                           |
            // | [                                  ] |
            // | [         ]                          |
            // | [    ]                               |
            // | [   ]                                |
            // ****************************************
            var intervals = new[] {
                new Interval(5, 9, true, true),
                new Interval(11, 15, true, true),
                new Interval(15, 20, true, true),
                new Interval(20, 24, true, true),
                new Interval(26, 30, true, true)
            };

            var collection = CreateCollection<Interval, int>(intervals);

            var ranges = new[] {
                    new Interval( 31,  35, IntervalType.Closed),
                    new Interval( 30,  35, IntervalType.Closed),
                    new Interval( 27,  29, IntervalType.Closed),
                    new Interval( 26,  35, IntervalType.Closed),
                    new Interval( 26,  30, IntervalType.Closed),
                    new Interval( 25,  26, IntervalType.Closed),
                    new Interval( 24,  26, IntervalType.Closed),
                    new Interval( 20,  30, IntervalType.Closed),
                    new Interval( 10,  11, IntervalType.Closed),
                    new Interval( 10,  10, IntervalType.Closed),
                    new Interval(  9,  26, IntervalType.Closed),
                    new Interval(  9,  11, IntervalType.Closed),
                    new Interval(  9,  10, IntervalType.Closed),
                    new Interval(  6,   8, IntervalType.Closed),
                    new Interval(  5,  30, IntervalType.Closed),
                    new Interval(  5,  15, IntervalType.LowIncluded),
                    new Interval(  5,   9, IntervalType.Closed),
                    new Interval(  0,  35, IntervalType.Closed),
                    new Interval(  0,  10, IntervalType.Closed),
                    new Interval(  0,   5, IntervalType.Closed),
                    new Interval(  0,   4, IntervalType.Closed)
                };

            if (!collection.AllowsOverlaps)
                intervals = NonOverlapping(intervals);

            foreach (var interval in ranges)
                CollectionAssert.AreEquivalent(intervals.Where(x => x.Overlaps(interval)), collection.FindOverlaps(interval));
        }

        [Test]
        [Category("Find Overlaps Range")]
        public void FindOverlapsRange_ManyIntervals_ChooseOverlapsInCollection()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);
            var interval = collection.Choose();
            var overlaps = collection.FindOverlaps(interval);
            Assert.True(overlaps.Count() > 0);
        }

        [Test]
        [Category("Find Overlaps Range")]
        public void FindOverlapsRange_ManyIntervals_ChooseOverlapsNotInCollection()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);
            IInterval<int> interval;
            do { interval = SingleInterval(); } while (intervals.Any(x => x.Overlaps(interval)));

            CollectionAssert.IsEmpty(collection.FindOverlaps(interval));
        }

        [Test]
        [Category("Find Overlaps Range")]
        public void FindOverlapsRange_ManyIntervals_ManyIntervals_FixedSeed()
        {
            updateRandom(-1585512131);
            FindOverlapsRange_ManyIntervals_ManyIntervals();
        }

        [Test]
        [Category("Find Overlaps Range")]
        public void FindOverlapsRange_ManyIntervals_ManyIntervals()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            foreach (var query in ManyIntervals())
                CollectionAssert.AreEquivalent((collection.AllowsOverlaps ? intervals : NonOverlapping(intervals)).Where(x => x.Overlaps(query)), collection.FindOverlaps(query));
        }

        #endregion

        #endregion

        #region Find Overlap

        #region Stabbing

        [Test]
        [Category("Find Overlap Stabbing")]
        public void FindOverlapStabbing_EmptyCollection_False()
        {
            var query = randomInt();
            Interval interval = null;
            var collection = CreateEmptyCollection<Interval, int>();

            Assert.False(collection.FindOverlap(query, ref interval));
            Assert.IsNull(interval);
        }

        #endregion

        #region Range

        [Test]
        [Category("Find Overlap Range")]
        public void FindOverlapRange_EmptyCollection_False()
        {
            var query = SingleInterval();
            Interval interval = null;
            var collection = CreateEmptyCollection<Interval, int>();

            Assert.False(collection.FindOverlap(query, ref interval));
            Assert.IsNull(interval);
        }

        [Test]
        [Category("Find Overlap Range")]
        public void FindOverlapRange_ManyIntervals_ChooseOverlapsInCollection_FixedSeed()
        {
            updateRandom(-1590477799);
            FindOverlapRange_ManyIntervals_ChooseOverlapsInCollection();
        }

        [Test]
        [Category("Find Overlap Range")]
        public void FindOverlapRange_ManyIntervals_ChooseOverlapsInCollection()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);
            var interval = collection.Choose();
            Interval overlap = null;
            Assert.True(collection.FindOverlap(interval, ref overlap));
            Assert.True(interval.Overlaps(overlap));
        }

        [Test]
        [Category("Find Overlap Range")]
        public void FindOverlapRange_ManyIntervals_ChooseOverlapsNotInCollection()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);
            var interval = SingleInterval();
            Interval overlap = null;
            while (intervals.Any(x => x.Overlaps(interval)))
                interval = SingleInterval();
            Assert.False(collection.FindOverlap(interval, ref overlap));
            Assert.IsNull(overlap);
        }

        #endregion

        #endregion

        #region Count Overlaps

        #region Stabbing

        [Test]
        [Category("Count Overlaps Stabbing")]
        public void CountOverlapsStabbing_EmptyCollection_Zero()
        {
            var query = randomInt();
            var collection = CreateEmptyCollection<Interval, int>();

            Assert.AreEqual(0, collection.CountOverlaps(query));
        }

        #endregion

        #region Range

        [Test]
        [Category("Count Overlaps Range")]
        public void CountOverlapsRange_EmptyCollection_Zero()
        {
            var query = SingleInterval();
            var collection = CreateEmptyCollection<Interval, int>();

            Assert.AreEqual(0, collection.CountOverlaps(query));
        }

        [Test]
        [Category("Count Overlaps Range")]
        public void CountOverlapsRange_ManyIntervals_ChooseOverlapsInCollection()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);
            var interval = collection.Choose();
            Assert.Greater(collection.CountOverlaps(interval), 0);
        }

        [Test]
        [Category("Count Overlaps Range")]
        public void CountOverlapsRange_ManyIntervals_ChooseOverlapsNotInCollection()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            var interval = SingleInterval();
            while (intervals.Any(x => x.Overlaps(interval)))
                interval = SingleInterval();

            Assert.AreEqual(0, collection.CountOverlaps(interval));
        }

        #endregion

        #endregion

        #region Gaps

        #region Gaps

        [Test]
        [Category("Gaps")]
        public void Gaps_EmptyCollection_Empty()
        {
            var collection = CreateEmptyCollection<Interval, int>();

            CollectionAssert.IsEmpty(collection.Gaps);
        }

        [Test]
        [Category("Gaps")]
        public void Gaps_SingleInterval_Empty()
        {
            var interval = SingleInterval();
            var collection = CreateCollection<Interval, int>(interval);

            CollectionAssert.IsEmpty(collection.Gaps);
        }

        [Test]
        [Category("Gaps")]
        public void Gaps_SingleObject_Empty()
        {
            var intervals = SingleObject();
            var collection = CreateCollection<Interval, int>(intervals);

            CollectionAssert.IsEmpty(collection.Gaps);
        }

        [Test]
        [Category("Gaps")]
        public void Gaps_DuplicateIntervals_Empty()
        {
            var intervals = DuplicateIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            CollectionAssert.IsEmpty(collection.Gaps);
        }

        [Test]
        [Category("Gaps")]
        public void Gaps_ManyIntervals_Gaps()
        {
            var intervals = DuplicateIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            var gaps = collection.Gaps;

            foreach (var gap in gaps)
            {
                Assert.False(collection.Any(x => x.Overlaps(gap)));
            }
        }

        [Test]
        [Category("Gaps")]
        public void Gaps_MeetingIntervals_Gaps_FixedSeed()
        {
            updateRandom(-1138109753);
            Gaps_MeetingIntervals_Gaps();
        }

        [Test]
        [Category("Gaps")]
        public void Gaps_MeetingIntervals_Gaps()
        {
            var count = Count;
            count += count % 2 + 1;
            var add = false;
            var intervals = MeetingIntervals(count);

            var expected = new ArrayList<Interval>();
            var input = new ArrayList<Interval>();

            foreach (var interval in intervals)
                ((add = !add) ? input : expected).Add(interval);

            var collection = CreateCollection<Interval, int>(input.ToArray());

            var gapsArray = collection.Gaps.ToArray();
            var expectedArray = expected.ToArray();

            for (int i = 0; i < count / 2; i++)
                Assert.True(expectedArray[i].IntervalEquals(gapsArray[i]));
        }

        [Test]
        [Category("Gaps")]
        public void Gaps_OverlappingIntervals_NoGaps()
        {
            var interval = SingleInterval();
            var interval2 = SingleInterval();
            while (!interval.Overlaps(interval2))
                interval2 = SingleInterval();
            Assert.True(interval.Overlaps(interval2));

            var collection = CreateCollection<Interval, int>(interval, interval2);

            CollectionAssert.IsEmpty(collection.Gaps);
        }

        [Test]
        [Category("Gaps")]
        public void Gaps_MeetingIntervals_NoGaps()
        {
            var interval = new Interval(0, 1, IntervalType.LowIncluded);
            var interval2 = new Interval(1, 2, IntervalType.LowIncluded);

            var collection = CreateCollection<Interval, int>(interval, interval2);

            CollectionAssert.IsEmpty(collection.Gaps);


            interval = new Interval(0, 1, IntervalType.HighIncluded);
            interval2 = new Interval(1, 2, IntervalType.HighIncluded);

            collection = CreateCollection<Interval, int>(interval, interval2);

            CollectionAssert.IsEmpty(collection.Gaps);
        }

        [Test]
        [Category("Gaps")]
        public void Gaps_MeetingIntervals_PointGap()
        {
            var interval = new Interval(0, 1, IntervalType.LowIncluded);
            var interval2 = new Interval(1, 2, IntervalType.HighIncluded);

            var collection = CreateCollection<Interval, int>(interval, interval2);

            var gaps = collection.Gaps;
            Assert.AreEqual(1, gaps.Count());
            Assert.True((new Interval(1)).IntervalEquals(gaps.First()));
        }

        #endregion

        #region Find Gaps

        [Test]
        [Category("Find Gaps")]
        public void FindGaps_EmptyCollection_GapMatchesQuery()
        {
            var query = SingleInterval();
            var collection = CreateEmptyCollection<Interval, int>();

            var gaps = collection.FindGaps(query);

            SingleIntervalEquals(query, gaps);
        }

        [Test]
        [Category("Find Gaps")]
        public void FindGaps_SingleInterval_QueryWithIntervalEmpty()
        {
            var interval = SingleInterval();
            var collection = CreateCollection<Interval, int>(interval);

            CollectionAssert.IsEmpty(collection.FindGaps(interval));
        }

        [Test]
        [Category("Find Gaps")]
        public void FindGaps_SingleInterval_QueryWithNoOverlap()
        {
            var interval = SingleInterval();
            var query = SingleInterval();
            while (interval.Overlaps(query)) query = SingleInterval();
            var collection = CreateCollection<Interval, int>(interval);

            SingleIntervalEquals(query, collection.FindGaps(query));
        }

        [Test]
        [Category("Find Gaps")]
        public void FindGaps_SingleObject_QueryWithIntervalEmpty()
        {
            var intervals = SingleObject();
            var collection = CreateCollection<Interval, int>(intervals);

            CollectionAssert.IsEmpty(collection.FindGaps(intervals.First()));
        }

        [Test]
        [Category("Find Gaps")]
        public void FindGaps_SingleObject_QueryWithNoOverlap()
        {
            var intervals = SingleObject();
            var query = SingleInterval();
            while (intervals.First().Overlaps(query)) query = SingleInterval();
            var collection = CreateCollection<Interval, int>(intervals);

            SingleIntervalEquals(query, collection.FindGaps(query));
        }

        [Test]
        [Category("Find Gaps")]
        public void FindGaps_DuplicateIntervals_QueryWithIntervalEmpty()
        {
            var interval = DuplicateIntervals();
            var collection = CreateCollection<Interval, int>(interval);

            CollectionAssert.IsEmpty(collection.FindGaps(interval.First()));
        }

        [Test]
        [Category("Find Gaps")]
        public void FindGaps_DuplicateIntervals_QueryWithNoOverlap()
        {
            var intervals = DuplicateIntervals();
            var query = SingleInterval();
            while (intervals.First().Overlaps(query)) query = SingleInterval();
            var collection = CreateCollection<Interval, int>(intervals);

            var gaps = collection.FindGaps(query);
            SingleIntervalEquals(query, gaps);
        }

        [Test]
        [Category("Find Gaps")]
        public void FindGaps_ManyIntervals_Gaps()
        {
            var intervals = DuplicateIntervals();
            var collection = CreateCollection<Interval, int>(intervals);
            var span = collection.Span;

            var gaps = collection.FindGaps(span);

            foreach (var gap in gaps)
            {
                Assert.False(collection.Any(x => x.Overlaps(gap)));
                Assert.True(span.Contains(gap));
            }
        }

        [Test]
        [Category("Find Gaps")]
        [Combinatorial]
        public void FindGaps_MeetingIntervals_Gaps(
            [Values(0, 1)] int count,
            [Values(true, false)] bool add)
        {
            var intervals = MeetingIntervals(100 + count);
            var span = intervals.Span();

            var expected = new ArrayList<Interval>();
            var input = new ArrayList<Interval>();

            foreach (var interval in intervals)
                ((add = !add) ? expected : input).Add(interval);

            var collection = CreateCollection<Interval, int>(input.ToArray());

            var gaps = collection.FindGaps(span);

            Assert.False(gaps.Any(x => x.OverlapsAny(collection)));

            Assert.True(expected.Zip(gaps, (x, y) => x.IntervalEquals(y)).All(b => b));
        }

        #endregion

        #region Find Gap
        #endregion

        #endregion

        #region Extensible

        #region Is Read Only

        #endregion

        #region Add

        [Test]
        [Category("Add")]
        public void Add_IsReadOnly_Exception()
        {
            var interval = SingleInterval();
            var collection = CreateEmptyCollection<Interval, int>();

            if (collection.IsReadOnly)
            {
                try
                {
                    collection.Add(interval);
                    Assert.Fail();
                }
                catch (ReadOnlyCollectionException)
                {
                    Assert.Pass();
                }
            }
        }

        [Test]
        [Category("Add")]
        public void Add_SingleObject_FirstAdded()
        {
            var intervals = SingleObject();
            var collection = CreateEmptyCollection<Interval, int>();

            if (!collection.IsReadOnly)
            {
                Assert.True(collection.Add(intervals.First()));

                foreach (var interval in intervals)
                    Assert.True(collection.Add(interval) == collection.AllowsReferenceDuplicates);
            }
        }

        [Test]
        [Category("Add")]
        public void Add_DuplicateIntervals_AllAdded()
        {
            var intervals = DuplicateIntervals();
            var collection = CreateEmptyCollection<Interval, int>();

            if (!collection.IsReadOnly)
            {
                for (var i = 0; i < Count; i++)
                {
                    if (i == 0)
                        Assert.True(collection.Add(intervals[i]));
                    else
                        Assert.AreEqual(collection.AllowsOverlaps, collection.Add(intervals[i]));
                }
            }
        }

        [Test]
        [Category("Add")]
        public void Add_ManyIntervals_AllAdded()
        {
            var intervals = ManyIntervals();
            var collection = CreateEmptyCollection<Interval, int>();

            if (!collection.IsReadOnly)
            {
                for (var i = 0; i < Count; i++)
                {
                    if (i == 0)
                        Assert.True(collection.Add(intervals[i]));
                    else
                        Assert.AreEqual(collection.AllowsOverlaps || !collection.Any(x => x.Overlaps(intervals[i])), collection.Add(intervals[i]));
                }
            }
        }

        #region Events

        [Test]
        [Category("Add Event")]
        public void Add_ManyIntervals_EventThrown()
        {
            var intervals = ManyIntervals();
            var collection = CreateEmptyCollection<Interval, int>();

            if (!collection.IsReadOnly)
            {
                Interval eventInterval = null;
                IIntervalCollection<Interval, int> eventCollection = null;
                collection.ItemsAdded += (sender, args) => eventInterval = args.Item;
                collection.CollectionChanged += sender => eventCollection = (IIntervalCollection<Interval, int>) sender;

                foreach (var interval in (collection.AllowsOverlaps ? intervals : NonOverlapping(intervals)))
                {
                    Assert.True(collection.Add(interval));
                    Assert.AreSame(interval, eventInterval);
                    Assert.AreSame(collection, eventCollection);
                    eventInterval = null;
                    eventCollection = null;

                    Assert.AreEqual(collection.AllowsReferenceDuplicates, collection.Add(interval));
                    if (collection.AllowsReferenceDuplicates)
                    {
                        Assert.AreSame(interval, eventInterval);
                        Assert.AreSame(collection, eventCollection);
                    }
                    else
                    {
                        Assert.IsNull(eventInterval);
                        Assert.IsNull(eventCollection);
                    }
                    eventInterval = null;
                    eventCollection = null;
                }
            }
        }

        #endregion

        #endregion

        #region Add All

        // TODO: Make more generic tests for AddAll

        [Test]
        [Category("Add All")]
        public void AddAll_IsReadOnly_Exception()
        {
            var intervals = new[] { SingleInterval() };
            var collection = CreateEmptyCollection<Interval, int>();

            if (collection.IsReadOnly)
            {
                try
                {
                    collection.AddAll(intervals);
                    Assert.Fail();
                }
                catch (ReadOnlyCollectionException)
                {
                    Assert.Pass();
                }
            }
        }

        #region Events
        #endregion

        #endregion

        #region Remove

        [Test]
        [Category("Remove")]
        public void Remove_IsReadOnly_Exception()
        {
            var interval = SingleInterval();
            var collection = CreateEmptyCollection<Interval, int>();

            if (collection.IsReadOnly)
            {
                try
                {
                    collection.Remove(interval);
                    Assert.Fail();
                }
                catch (ReadOnlyCollectionException)
                {
                    Assert.Pass();
                }
            }
        }

        [Test]
        [Category("Remove")]
        public void Remove_EmptyCollection_False()
        {
            var interval = SingleInterval();
            var collection = CreateEmptyCollection<Interval, int>();

            if (!collection.IsReadOnly)
            {
                Assert.False(collection.Remove(interval));
            }
        }

        [Test]
        [Category("Remove")]
        public void Remove_SingleInterval_Removed()
        {
            var singleInterval = SingleInterval();
            var collection = CreateCollection<Interval, int>(singleInterval);

            if (!collection.IsReadOnly)
            {
                Assert.False(collection.Remove(new Interval(singleInterval)));
                Assert.True(collection.Remove(singleInterval));
                Assert.False(collection.Remove(singleInterval));
            }
        }

        [Test]
        [Category("Remove")]
        public void Remove_SingleObject_Removed_FixedSeed()
        {
            updateRandom(1358768120);
            Remove_SingleObject_Removed();
        }

        [Test]
        [Category("Remove")]
        public void Remove_SingleObject_Removed()
        {
            var intervals = SingleObject();
            var collection = CreateCollection<Interval, int>(intervals);

            if (!collection.IsReadOnly)
            {
                for (var i = 0; i < Count; i++)
                {
                    if (i == 0)
                    {
                        Assert.False(collection.Remove(new Interval(intervals[i])));
                        Assert.True(collection.Remove(intervals[i]));
                    }
                    else
                        Assert.AreEqual(collection.AllowsReferenceDuplicates, collection.Remove(intervals[i]));
                }
            }
        }

        [Test]
        [Category("Remove")]
        public void Remove_DuplicateIntervals_True_FixedSeed()
        {
            updateRandom(1747493494);
            Remove_DuplicateIntervals_True();
            updateRandom(-356355912);
            Remove_DuplicateIntervals_True();
        }

        [Test]
        [Category("Remove")]
        public void Remove_DuplicateIntervals_True()
        {
            var intervals = DuplicateIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            if (!collection.IsReadOnly)
            {
                foreach (var interval in (collection.AllowsOverlaps ? intervals : NonOverlapping(intervals)))
                {
                    Assert.False(collection.Remove(new Interval(interval)));
                    Assert.True(collection.Remove(interval));
                    Assert.False(collection.Remove(interval));
                }
            }
        }

        [Test]
        [Category("Remove")]
        public void Remove_ManyIntervals_True_FixedSeed()
        {
            updateRandom(-284197963);
            Remove_ManyIntervals_True();
            updateRandom(873007789);
            Remove_ManyIntervals_True();
            updateRandom(-1204159134);
            Remove_ManyIntervals_True();
        }

        [Test]
        [Category("Remove")]
        public void Remove_ManyIntervals_True()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            if (!collection.IsReadOnly)
            {
                foreach (var interval in (collection.AllowsOverlaps ? intervals : NonOverlapping(intervals)))
                {
                    Assert.False(collection.Remove(new Interval(interval)));
                    Assert.True(collection.Remove(interval));
                    Assert.False(collection.Remove(interval));
                }
            }
        }

        [Test]
        [Category("Remove")]
        public void Remove_ManyIntervals_RemovingIntervalsNotInCollection()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            if (!collection.IsReadOnly)
            {
                foreach (var interval in ManyIntervals())
                    Assert.False(collection.Remove(interval));
            }
        }

        #region Events

        [Test]
        [Category("Remove Event")]
        public void Remove_ManyIntervals_EventThrown_FixedSeed()
        {
            updateRandom(1352270728);
            Remove_ManyIntervals_EventThrown();
            updateRandom(-904807620);
            Remove_ManyIntervals_EventThrown();
            updateRandom(-356355912);
            Remove_ManyIntervals_EventThrown();
            updateRandom(693772309);
            Remove_ManyIntervals_EventThrown();
            updateRandom(288655792);
            Remove_ManyIntervals_EventThrown();
        }

        [Test]
        [Category("Remove Event")]
        public void Remove_ManyIntervals_EventThrown()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals.Concat(intervals).ToArray());

            if (!collection.IsReadOnly)
            {
                IInterval<int> eventInterval = null;
                IIntervalCollection<Interval, int> eventCollection = null;
                collection.ItemsRemoved += (sender, args) => eventInterval = args.Item;
                collection.CollectionChanged += sender => eventCollection = (IIntervalCollection<Interval, int>) sender;

                foreach (var interval in (collection.AllowsOverlaps ? intervals : NonOverlapping(intervals)))
                {
                    Assert.True(collection.Remove(interval));
                    Assert.AreSame(interval, eventInterval);
                    Assert.AreSame(collection, eventCollection);
                    eventInterval = null;
                    eventCollection = null;

                    Assert.AreEqual(collection.AllowsReferenceDuplicates, collection.Remove(interval));
                    if (collection.AllowsReferenceDuplicates)
                    {
                        Assert.AreSame(interval, eventInterval);
                        Assert.AreSame(collection, eventCollection);
                    }
                    else
                    {
                        Assert.IsNull(eventInterval);
                        Assert.IsNull(eventCollection);
                    }
                    eventInterval = null;
                    eventCollection = null;

                    Assert.False(collection.Remove(interval));
                    Assert.IsNull(eventInterval);
                    Assert.IsNull(eventCollection);
                }
            }
        }

        #endregion

        #endregion

        #region Clear

        [Test]
        [Category("Clear")]
        public void Clear_IsReadOnly_Exception()
        {
            var collection = CreateEmptyCollection<Interval, int>();

            if (collection.IsReadOnly)
            {
                try
                {
                    collection.Clear();
                    Assert.Fail();
                }
                catch (ReadOnlyCollectionException)
                {
                    Assert.Pass();
                }
            }
        }

        [Test]
        [Category("Clear")]
        public void Clear_EmptyCollection_IsEmpty()
        {
            var collection = CreateEmptyCollection<Interval, int>();

            if (!collection.IsReadOnly)
            {
                Assert.True(collection.IsEmpty);
                collection.Clear();
                Assert.True(collection.IsEmpty);
            }
        }

        [Test]
        [Category("Clear")]
        public void Clear_SingleInterval_BecomesEmpty()
        {
            var interval = SingleInterval();
            var collection = CreateCollection<Interval, int>(interval);

            if (!collection.IsReadOnly)
            {
                Assert.False(collection.IsEmpty);
                collection.Clear();
                Assert.True(collection.IsEmpty);
            }
        }

        [Test]
        [Category("Clear")]
        public void Clear_ManyIntervals_BecomesEmpty()
        {
            var interval = ManyIntervals();
            var collection = CreateCollection<Interval, int>(interval);

            if (!collection.IsReadOnly)
            {
                Assert.False(collection.IsEmpty);
                collection.Clear();
                Assert.True(collection.IsEmpty);
            }
        }

        #region Events

        [Test]
        [Category("Clear Event")]
        public void Clear_ManyIntervals_EventThrown()
        {
            var intervals = ManyIntervals();
            var collection = CreateCollection<Interval, int>(intervals);

            if (!collection.IsReadOnly)
            {
                var eventThrown = false;
                IIntervalCollection<Interval, int> eventCollection = null;
                collection.CollectionCleared += (sender, args) => eventThrown = true;
                collection.CollectionChanged += sender => eventCollection = (IIntervalCollection<Interval, int>) sender;

                collection.Clear();
                Assert.True(eventThrown);
                Assert.AreSame(collection, eventCollection);
                Assert.True(collection.IsEmpty);
                eventThrown = false;
                eventCollection = null;

                collection.Clear();
                Assert.False(eventThrown);
                Assert.IsNull(eventCollection);
                Assert.True(collection.IsEmpty);
            }
        }

        #endregion

        #endregion

        #endregion

        #endregion

        #region Example Cases

        #region IBS Article (Extended)

        [Test]
        [Category("Example Cases")]
        public void Stabbing_IntervalBinarySearchTreeArticleExampleExtended_OverlapsAndSpan()
        {
            //*****************************
            //   0     5    10    15    20
            //   |     |     |     |     |
            //             A[----------]
            //    B[-----]
            //   C[-)
            //                      D(---]
            //           E1[----]   
            //           E2[----]   
            //                        F|
            //...--------------------]G
            //        H(-----)
            //           
            //*****************************
            var intervals = new[]
                {
                    new Interval(9, 19, IntervalType.Closed),                   // A
                    new Interval(2, 7, IntervalType.Closed),                    // B
                    new Interval(1, 3, IntervalType.LowIncluded),               // C
                    new Interval(17, 20, IntervalType.HighIncluded),            // D
                    new Interval(8, 12, IntervalType.Closed),                   // E1
                    new Interval(8, 12, IntervalType.Closed),                   // E2
                    new Interval(18),                                           // F
                    new Interval(int.MinValue, 17, IntervalType.HighIncluded),  // G
                    new Interval(5, 10, IntervalType.Open)                      // H
                };

            var collection = CreateCollection<Interval, int>(intervals);

            foreach (var point in intervals.UniqueEndpointValues())
            {
                var expected = (collection.AllowsOverlaps ? intervals : NonOverlapping(intervals)).Where(x => x.Overlaps(point));
                CollectionAssert.AreEquivalent(expected, collection.FindOverlaps(point));
            }

            // TODO: Fix when default behavior has been decided on
            var span = /*collection.AllowsOverlaps ?*/ new Interval(int.MinValue, 20, IntervalType.HighIncluded) /*: new Interval(2, 19, IntervalType.Closed)*/;
            Assert.True(span.IntervalEquals(collection.Span));

            Assert.AreEqual(collection.AllowsOverlaps ? 5 : 1, collection.MaximumDepth);

            if (!collection.IsReadOnly)
            {
                // TODO: Finish. This is just the intervals from the old test being added and removed!
                var additionalIntervals = new[]
                {
                    new Interval(1, 2,  IntervalType.Closed),
                    new Interval(1, 4,  IntervalType.Closed),
                    new Interval(6, 12, IntervalType.Closed),
                    new Interval(5, 21, IntervalType.Closed)
                };

                foreach (var interval in additionalIntervals)
                {
                    collection.Add(interval);
                    collection.Remove(interval);
                }
            }
        }

        #endregion

        #region Large Scale Random

        /*
        [Test]
        [Category("Large Scale")]
        public void Random_CallWholeInterface_FixedSeed()
        {
            updateRandom(267138595);
            Random_CallWholeInterface();
            updateRandom(1065643459);
            Random_CallWholeInterface();
        }
        */

        [Test]
        [Category("Large Scale")]
        public void Random_CallWholeInterface()
        {
            var collection = CreateEmptyCollection<Interval, int>();

            var sum = 0;

            if (!collection.IsReadOnly)
            {
                var count = Random.Next(50, 100);
                var set = new HashSet<Interval>();
                var intervals = ManyIntervals(count);

                for (var i = 0; i < count; i++)
                {
                    var interval = intervals[i];
                    if (collection.Add(interval))
                        set.Add(interval);

                    interval = set.Choose();
                    var span = collection.Span;
                    var mno = collection.MaximumDepth;
                    Assert.AreEqual(collection.FindOverlaps(interval).Count(), collection.CountOverlaps(interval));
                    Assert.AreEqual(collection.FindOverlaps(interval.Low).Count(), collection.CountOverlaps(interval.Low));
                    sum += collection.FindOverlaps(interval).Count();
                    Assert.True(collection.FindOverlap(interval, ref interval));
                    collection.FindOverlap(interval.Low, ref interval);

                    var remove = Random.Next(0, 2);
                    if (remove == 1)
                    {
                        interval = set.Choose();
                        collection.Remove(interval);
                        set.Remove(interval);
                    }
                }

                collection.Clear();
                CollectionAssert.IsEmpty(collection);
            }

            Console.Out.WriteLine("Sum: " + sum);
            Console.Out.WriteLine("Count: " + collection.Count);
        }

        #endregion

        #endregion

        #endregion
    }
}