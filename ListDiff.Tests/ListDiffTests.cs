using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ListDiff.Tests
{
    [TestClass]
    public class ListDiffTests
    {
        [TestMethod]        
        public void diff_commonPrefixTest()
        {            
            Assert.AreEqual(0, ListDiff.GetCommonPrefix("abc".ToCharArray(), "xyz".ToCharArray()));
            Assert.AreEqual(4, ListDiff.GetCommonPrefix("1234abcdef".ToCharArray(), "1234xyz".ToCharArray()));
            Assert.AreEqual(4, ListDiff.GetCommonPrefix("1234".ToCharArray(), "1234xyz".ToCharArray()));
        }

        [TestMethod]
        public void diff_commonSuffixTest()
        {
            
            Assert.AreEqual(0, ListDiff.GetCommonSuffix("abc".ToCharArray(), "xyz".ToCharArray()));
            Assert.AreEqual(4, ListDiff.GetCommonSuffix("abcdef1234".ToCharArray(), "xyz1234".ToCharArray()));
            Assert.AreEqual(4, ListDiff.GetCommonSuffix("1234".ToCharArray(), "xyz1234".ToCharArray()));
        }

        [TestMethod]
        public void diff_halfmatchTest()
        {
            Assert.IsNull(ListDiff.GetHalfMatch("1234567890".ToCharArray(), "abcdef".ToCharArray()));

            Func<string, string, string[]> test =
                (a, b) => ListDiff.GetHalfMatch(a.ToCharArray(), b.ToCharArray()).Select(r => new string(r.ToArray())).ToArray();

            CollectionAssert.AreEqual(new[] { "12", "90", "a", "z", "345678" }, test("1234567890", "a345678z"));

            CollectionAssert.AreEqual(new[] { "a", "z", "12", "90", "345678" }, test("a345678z", "1234567890"));

            CollectionAssert.AreEqual(new[] { "12123", "123121", "a", "z", "1234123451234" }, test("121231234123451234123121", "a1234123451234z"));

            CollectionAssert.AreEqual(new[] { "", "-=-=-=-=-=", "x", "", "x-=-=-=-=-=-=-=" }, test("x-=-=-=-=-=-=-=-=-=-=-=-=", "xx-=-=-=-=-=-=-="));

            CollectionAssert.AreEqual(new[] { "-=-=-=-=-=", "", "", "y", "-=-=-=-=-=-=-=y" }, test("-=-=-=-=-=-=-=-=-=-=-=-=y", "-=-=-=-=-=-=-=yy"));
        }
        
        [TestMethod]
        public void diff_cleanupMergeTest()
        {
            // Cleanup a messy diff.
            var diffs = new List<Diff<char>>();
            ListDiff.CleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff<char>>(), diffs);

            Func<Operation, string, Diff<char>> makeDiff = (o, s) => new Diff<char>(o, s.ToCharArray());

            diffs = new List<Diff<char>> { makeDiff(Operation.Equal, "a"), makeDiff(Operation.Delete, "b"), makeDiff(Operation.Insert, "c") };
            ListDiff.CleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff<char>> { makeDiff(Operation.Equal, "a"), makeDiff(Operation.Delete, "b"), makeDiff(Operation.Insert, "c") }, diffs);

            diffs = new List<Diff<char>> { makeDiff(Operation.Equal, "a"), makeDiff(Operation.Equal, "b"), makeDiff(Operation.Equal, "c") };
            ListDiff.CleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff<char>> { makeDiff(Operation.Equal, "abc") }, diffs);

            diffs = new List<Diff<char>> { makeDiff(Operation.Delete, "a"), makeDiff(Operation.Delete, "b"), makeDiff(Operation.Delete, "c") };
            ListDiff.CleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff<char>> { makeDiff(Operation.Delete, "abc") }, diffs);

            diffs = new List<Diff<char>> { makeDiff(Operation.Insert, "a"), makeDiff(Operation.Insert, "b"), makeDiff(Operation.Insert, "c") };
            ListDiff.CleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff<char>> { makeDiff(Operation.Insert, "abc") }, diffs);

            diffs = new List<Diff<char>> { makeDiff(Operation.Delete, "a"), makeDiff(Operation.Insert, "b"), makeDiff(Operation.Delete, "c"), makeDiff(Operation.Insert, "d"), makeDiff(Operation.Equal, "e"), makeDiff(Operation.Equal, "f") };
            ListDiff.CleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff<char>> { makeDiff(Operation.Delete, "ac"), makeDiff(Operation.Insert, "bd"), makeDiff(Operation.Equal, "ef") }, diffs);

            diffs = new List<Diff<char>> { makeDiff(Operation.Delete, "a"), makeDiff(Operation.Insert, "abc"), makeDiff(Operation.Delete, "dc") };
            ListDiff.CleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff<char>> { makeDiff(Operation.Equal, "a"), makeDiff(Operation.Delete, "d"), makeDiff(Operation.Insert, "b"), makeDiff(Operation.Equal, "c") }, diffs);

            diffs = new List<Diff<char>> { makeDiff(Operation.Equal, "a"), makeDiff(Operation.Insert, "ba"), makeDiff(Operation.Equal, "c") };
            ListDiff.CleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff<char>> { makeDiff(Operation.Insert, "ab"), makeDiff(Operation.Equal, "ac") }, diffs);

            diffs = new List<Diff<char>> { makeDiff(Operation.Equal, "c"), makeDiff(Operation.Insert, "ab"), makeDiff(Operation.Equal, "a") };
            ListDiff.CleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff<char>> { makeDiff(Operation.Equal, "ca"), makeDiff(Operation.Insert, "ba") }, diffs);

            diffs = new List<Diff<char>> { makeDiff(Operation.Equal, "a"), makeDiff(Operation.Delete, "b"), makeDiff(Operation.Equal, "c"), makeDiff(Operation.Delete, "ac"), makeDiff(Operation.Equal, "x") };
            ListDiff.CleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff<char>> { makeDiff(Operation.Delete, "abc"), makeDiff(Operation.Equal, "acx") }, diffs);

            diffs = new List<Diff<char>> { makeDiff(Operation.Equal, "x"), makeDiff(Operation.Delete, "ca"), makeDiff(Operation.Equal, "c"), makeDiff(Operation.Delete, "b"), makeDiff(Operation.Equal, "a") };
            ListDiff.CleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff<char>> { makeDiff(Operation.Equal, "xca"), makeDiff(Operation.Delete, "cba") }, diffs);
        }

        [TestMethod]
        public void diff_pathTest()
        {
            // First, check footprints are different.
            Assert.IsTrue(ListDiff.GetFootprint(1, 10) != ListDiff.GetFootprint(10, 1), "diff_footprint:");

            // Single letters.
            // Trace a path from back to front.
            HashSet<long> rowSet;
            var vMap = new List<HashSet<long>>();
            {
                rowSet = new HashSet<long> {ListDiff.GetFootprint(0, 0)};
                vMap.Add(rowSet);
                rowSet = new HashSet<long> {ListDiff.GetFootprint(0, 1), ListDiff.GetFootprint(1, 0)};
                vMap.Add(rowSet);
                rowSet = new HashSet<long>
                {
                    ListDiff.GetFootprint(0, 2),
                    ListDiff.GetFootprint(2, 0),
                    ListDiff.GetFootprint(2, 2)
                };
                vMap.Add(rowSet);
                rowSet = new HashSet<long>
                {
                    ListDiff.GetFootprint(0, 3),
                    ListDiff.GetFootprint(2, 3),
                    ListDiff.GetFootprint(3, 0),
                    ListDiff.GetFootprint(4, 3)
                };
                vMap.Add(rowSet);
                rowSet = new HashSet<long>
                {
                    ListDiff.GetFootprint(0, 4),
                    ListDiff.GetFootprint(2, 4),
                    ListDiff.GetFootprint(4, 0),
                    ListDiff.GetFootprint(4, 4),
                    ListDiff.GetFootprint(5, 3)
                };
                vMap.Add(rowSet);
                rowSet = new HashSet<long>
                {
                    ListDiff.GetFootprint(0, 5),
                    ListDiff.GetFootprint(2, 5),
                    ListDiff.GetFootprint(4, 5),
                    ListDiff.GetFootprint(5, 0),
                    ListDiff.GetFootprint(6, 3),
                    ListDiff.GetFootprint(6, 5)
                };
                vMap.Add(rowSet);
                rowSet = new HashSet<long>
                {
                    ListDiff.GetFootprint(0, 6),
                    ListDiff.GetFootprint(2, 6),
                    ListDiff.GetFootprint(4, 6),
                    ListDiff.GetFootprint(6, 6),
                    ListDiff.GetFootprint(7, 5)
                };
                vMap.Add(rowSet);
            }

            Func<Operation, string, Diff<char>> makeDiff = (o, s) => new Diff<char>(o, s.ToCharArray());

            List<Diff<char>> diffs = new List<Diff<char>>{
                makeDiff(Operation.Insert, "W"),
                makeDiff(Operation.Delete, "A"),
                makeDiff(Operation.Equal, "1"),
                makeDiff(Operation.Delete, "B"),
                makeDiff(Operation.Equal, "2"),
                makeDiff(Operation.Insert, "X"),
                makeDiff(Operation.Delete, "C"),
                makeDiff(Operation.Equal, "3"),
                makeDiff(Operation.Delete, "D")};
            CollectionAssert.AreEqual(diffs, ListDiff.DiffPath1(vMap, "A1B2C3D".ToCharArray(), "W12X3".ToCharArray()), "diff_path1: Single letters.");

            // Trace a path from front to back.
            vMap.RemoveAt(vMap.Count - 1);
            diffs = new List<Diff<char>>{
                makeDiff(Operation.Equal, "4"),
                makeDiff(Operation.Delete, "E"),
                makeDiff(Operation.Insert, "Y"),
                makeDiff(Operation.Equal, "5"),
                makeDiff(Operation.Delete, "F"),
                makeDiff(Operation.Equal, "6"),
                makeDiff(Operation.Delete, "G"),
                makeDiff(Operation.Insert, "Z")};
            CollectionAssert.AreEqual(diffs, ListDiff.DiffPath2(vMap, "4E5F6G".ToCharArray(), "4Y56Z".ToCharArray()), "diff_path2: Single letters.");

            // Double letters.
            // Trace a path from back to front.
            vMap = new List<HashSet<long>>();
            {
                rowSet = new HashSet<long> {ListDiff.GetFootprint(0, 0)};
                vMap.Add(rowSet);
                rowSet = new HashSet<long> {ListDiff.GetFootprint(0, 1), ListDiff.GetFootprint(1, 0)};
                vMap.Add(rowSet);
                rowSet = new HashSet<long>
                {
                    ListDiff.GetFootprint(0, 2),
                    ListDiff.GetFootprint(1, 1),
                    ListDiff.GetFootprint(2, 0)
                };
                vMap.Add(rowSet);
                rowSet = new HashSet<long>
                {
                    ListDiff.GetFootprint(0, 3),
                    ListDiff.GetFootprint(1, 2),
                    ListDiff.GetFootprint(2, 1),
                    ListDiff.GetFootprint(3, 0)
                };
                vMap.Add(rowSet);
                rowSet = new HashSet<long>
                {
                    ListDiff.GetFootprint(0, 4),
                    ListDiff.GetFootprint(1, 3),
                    ListDiff.GetFootprint(3, 1),
                    ListDiff.GetFootprint(4, 0),
                    ListDiff.GetFootprint(4, 4)
                };
                vMap.Add(rowSet);
            }
            diffs = new List<Diff<char>>{
                makeDiff(Operation.Insert, "WX"),
                makeDiff(Operation.Delete, "AB"),
                makeDiff(Operation.Equal, "12")};
            CollectionAssert.AreEqual(diffs, ListDiff.DiffPath1(vMap, "AB12".ToCharArray(), "WX12".ToCharArray()), "diff_path1: Double letters.");

            // Trace a path from front to back.
            vMap = new List<HashSet<long>>();
            {
                rowSet = new HashSet<long> {ListDiff.GetFootprint(0, 0)};
                vMap.Add(rowSet);
                rowSet = new HashSet<long> {ListDiff.GetFootprint(0, 1), ListDiff.GetFootprint(1, 0)};
                vMap.Add(rowSet);
                rowSet = new HashSet<long>
                {
                    ListDiff.GetFootprint(1, 1),
                    ListDiff.GetFootprint(2, 0),
                    ListDiff.GetFootprint(2, 4)
                };
                vMap.Add(rowSet);
                rowSet = new HashSet<long>
                {
                    ListDiff.GetFootprint(2, 1),
                    ListDiff.GetFootprint(2, 5),
                    ListDiff.GetFootprint(3, 0),
                    ListDiff.GetFootprint(3, 4)
                };
                vMap.Add(rowSet);
                rowSet = new HashSet<long>
                {
                    ListDiff.GetFootprint(2, 6),
                    ListDiff.GetFootprint(3, 5),
                    ListDiff.GetFootprint(4, 4)
                };
                vMap.Add(rowSet);
            }
            diffs = new List<Diff<char>>{
                makeDiff(Operation.Delete, "CD"),
                makeDiff(Operation.Equal, "34"),
                makeDiff(Operation.Insert, "YZ")};
            CollectionAssert.AreEqual(diffs, ListDiff.DiffPath2(vMap, "CD34".ToCharArray(), "34YZ".ToCharArray()), "diff_path2: Double letters.");
        }

        [TestMethod]
        public void diff_mainTest()
        {
            Func<Operation, string, Diff<char>> makeDiff = (o, s) => new Diff<char>(o, s.ToCharArray());

            TimeSpan? timeout = null;
            var threshold = 32;

            // ReSharper disable once AccessToModifiedClosure
            Func<string, string, List<Diff<char>>> compare = (x, y) => ListDiff.Compare(x.ToCharArray(), y.ToCharArray(), timeout, threshold).ToList();
            
            // Perform a trivial diff.
            List<Diff<char>> diffs = new List<Diff<char>> { makeDiff(Operation.Equal, "abc") };
            CollectionAssert.AreEqual(diffs, compare("abc", "abc"), "Compare: Null case.");

            diffs = new List<Diff<char>> { makeDiff(Operation.Equal, "ab"), makeDiff(Operation.Insert, "123"), makeDiff(Operation.Equal, "c") };
            CollectionAssert.AreEqual(diffs, compare("abc", "ab123c"), "Compare: Simple Insertion.");

            diffs = new List<Diff<char>> { makeDiff(Operation.Equal, "a"), makeDiff(Operation.Delete, "123"), makeDiff(Operation.Equal, "bc") };
            CollectionAssert.AreEqual(diffs, compare("a123bc", "abc"), "Compare: Simple deletion.");

            diffs = new List<Diff<char>> { makeDiff(Operation.Equal, "a"), makeDiff(Operation.Insert, "123"), makeDiff(Operation.Equal, "b"), makeDiff(Operation.Insert, "456"), makeDiff(Operation.Equal, "c") };
            CollectionAssert.AreEqual(diffs, compare("abc", "a123b456c"), "Compare: Two insertions.");

            diffs = new List<Diff<char>> { makeDiff(Operation.Equal, "a"), makeDiff(Operation.Delete, "123"), makeDiff(Operation.Equal, "b"), makeDiff(Operation.Delete, "456"), makeDiff(Operation.Equal, "c") };
            CollectionAssert.AreEqual(diffs, compare("a123b456c", "abc"), "Compare: Two deletions.");

            timeout = TimeSpan.FromDays(10);

            diffs = new List<Diff<char>> { makeDiff(Operation.Delete, "a"), makeDiff(Operation.Insert, "b") };
            CollectionAssert.AreEqual(diffs, compare("a", "b"), "Compare: Simple case #1.");

            diffs = new List<Diff<char>> { makeDiff(Operation.Delete, "Apple"), makeDiff(Operation.Insert, "Banana"), makeDiff(Operation.Equal, "s are a"), makeDiff(Operation.Insert, "lso"), makeDiff(Operation.Equal, " fruit.") };
            CollectionAssert.AreEqual(diffs, compare("Apples are a fruit.", "Bananas are also fruit."), "Compare: Simple case #2.");

            diffs = new List<Diff<char>> { makeDiff(Operation.Delete, "a"), makeDiff(Operation.Insert, "\u0680"), makeDiff(Operation.Equal, "x"), makeDiff(Operation.Delete, "\t"), makeDiff(Operation.Insert, new string(new[] { (char)0 })) };
            CollectionAssert.AreEqual(diffs, compare("ax\t", "\u0680x" + (char)0), "Compare: Simple case #3.");

            diffs = new List<Diff<char>> { makeDiff(Operation.Delete, "1"), makeDiff(Operation.Equal, "a"), makeDiff(Operation.Delete, "y"), makeDiff(Operation.Equal, "b"), makeDiff(Operation.Delete, "2"), makeDiff(Operation.Insert, "xab") };
            CollectionAssert.AreEqual(diffs, compare("1ayb2", "abxab"), "Compare: Overlap #1.");

            diffs = new List<Diff<char>> { makeDiff(Operation.Insert, "xaxcx"), makeDiff(Operation.Equal, "abc"), makeDiff(Operation.Delete, "y") };
            CollectionAssert.AreEqual(diffs, compare("abcy", "xaxcxabc"), "Compare: Overlap #2.");

            // Sub-optimal double-ended diff.
            threshold = 2;
            diffs = new List<Diff<char>> { makeDiff(Operation.Insert, "x"), makeDiff(Operation.Equal, "a"), makeDiff(Operation.Delete, "b"), makeDiff(Operation.Insert, "x"), makeDiff(Operation.Equal, "c"), makeDiff(Operation.Delete, "y"), makeDiff(Operation.Insert, "xabc") };
            CollectionAssert.AreEqual(diffs, compare("abcy", "xaxcxabc"), "Compare: Overlap #3.");

            threshold = 32;
            timeout = TimeSpan.FromSeconds(0.001);
            string a = "`Twas brillig, and the slithy toves\nDid gyre and gimble in the wabe:\nAll mimsy were the borogoves,\nAnd the mome raths outgrabe.\n";
            string b = "I am the very model of a modern major general,\nI've information vegetable, animal, and mineral,\nI know the kings of England, and I quote the fights historical,\nFrom Marathon to Waterloo, in order categorical.\n";
            // Increase the text lengths by 1024 times to ensure a timeout.
            for (int x = 0; x < 10; x++)
            {
                a = a + a;
                b = b + b;
            }
            Assert.IsNull(new ListDiff(timeout.Value, threshold).GetMap(a.ToCharArray(), b.ToCharArray()), "Compare: Timeout.");
        }        
    }
}
