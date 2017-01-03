using System;
using System.Collections.Generic;
using System.Linq;

namespace ListDiff
{    
    public class ListDiff
    {        
        private readonly TimeSpan _diffTimeout;
        private readonly int _diffDualThreshold;

        internal ListDiff(TimeSpan timeout, int dualThreshold)
        {
            _diffTimeout = timeout;
            _diffDualThreshold = dualThreshold;
        }

        public static IEnumerable<Diff<T>> Compare<T>(
            IReadOnlyList<T> text1, 
            IReadOnlyList<T> text2,
            TimeSpan? timeSpan = null,
            int diffDualThreshold = 32)
        {
            if (timeSpan == null)
            {
                timeSpan = TimeSpan.FromSeconds(1.0f);
            }

            return new ListDiff(timeSpan.Value, diffDualThreshold).CompareUsingOptions(text1, text2);
        }

        internal List<Diff<T>> CompareUsingOptions<T>(IReadOnlyList<T> text1, IReadOnlyList<T> text2)
        {
            // Check for equality (speedup)
            if (text1.SequenceEqual(text2))
            {
                return new List<Diff<T>> {new Diff<T>(Operation.Equal, text1)};
            }

            // Trim off common prefix (speedup)
            var commonlength = GetCommonPrefix(text1, text2);
            var commonprefix = text1.Substring(0, commonlength);
            text1 = text1.Substring(commonlength);
            text2 = text2.Substring(commonlength);

            // Trim off common suffix (speedup)
            commonlength = GetCommonSuffix(text1, text2);
            var commonsuffix = text1.Substring(text1.Count - commonlength);
            text1 = text1.Substring(0, text1.Count - commonlength);
            text2 = text2.Substring(0, text2.Count - commonlength);

            // Compute the diff on the middle block
            var diffs = Compute(text1, text2);

            // Restore the prefix and suffix
            if (commonprefix.Count != 0)
            {
                diffs.Insert(0, (new Diff<T>(Operation.Equal, commonprefix)));
            }
            if (commonsuffix.Count != 0)
            {
                diffs.Add(new Diff<T>(Operation.Equal, commonsuffix));
            }

            CleanupMerge(diffs);
            return diffs;
        }

        private List<Diff<T>> Compute<T>(IReadOnlyList<T> text1, IReadOnlyList<T> text2)
        {
            var diffs = new List<Diff<T>>();

            if (text1.Count == 0)
            {
                // Just add some Items (speedup)
                diffs.Add(new Diff<T>(Operation.Insert, text2));
                return diffs;
            }

            if (text2.Count == 0)
            {
                // Just delete some Items (speedup)
                diffs.Add(new Diff<T>(Operation.Delete, text1));
                return diffs;
            }

            var longtext = text1.Count > text2.Count ? text1 : text2;
            var shorttext = text1.Count > text2.Count ? text2 : text1;
            var i = longtext.IndexOf(shorttext);
            if (i != -1)
            {
                // Shorter Items is inside the longer Items (speedup)
                var op = (text1.Count > text2.Count) ? Operation.Delete : Operation.Insert;
                diffs.Add(new Diff<T>(op, longtext.Substring(0, i)));
                diffs.Add(new Diff<T>(Operation.Equal, shorttext));
                diffs.Add(new Diff<T>(op, longtext.Substring(i + shorttext.Count)));
                return diffs;
            }
            
            // Check to see if the problem can be split in two.
            var hm = GetHalfMatch(text1, text2);
            if (hm != null)
            {
                // A half-match was found, sort out the return data.
                var text1A = hm[0];
                var text1B = hm[1];
                var text2A = hm[2];
                var text2B = hm[3];
                var midCommon = hm[4];
                // Send both pairs off for separate processing.
                var diffsA = CompareUsingOptions(text1A, text2A);
                var diffsB = CompareUsingOptions(text1B, text2B);
                // Merge the results.
                diffs = diffsA;
                diffs.Add(new Diff<T>(Operation.Equal, midCommon));
                diffs.AddRange(diffsB);
                return diffs;
            }
            
            diffs = GetMap(text1, text2);
            return diffs ?? new List<Diff<T>>
            {
                new Diff<T>(Operation.Delete, text1), new Diff<T>(Operation.Insert, text2)
            };
        }
        
        internal List<Diff<T>> GetMap<T>(IReadOnlyList<T> text1, IReadOnlyList<T> text2)
        {
            var msEnd = DateTime.Now + _diffTimeout;
            // Cache the Items lengths to prevent multiple calls.
            var text1Length = text1.Count;
            var text2Length = text2.Count;
            var maxD = text1Length + text2Length - 1;
            var doubleEnd = _diffDualThreshold * 2 < maxD;
            var vMap1 = new List<HashSet<long>>();
            var vMap2 = new List<HashSet<long>>();
            var v1 = new Dictionary<int, int>();
            var v2 = new Dictionary<int, int>();
            v1.Add(1, 0);
            v2.Add(1, 0);
            var footstep = 0L;  // Used to track overlapping paths.
            var footsteps = new Dictionary<long, int>();
            var done = false;
            // If the total number of characters is odd, then the front path will
            // collide with the reverse path.
            var front = ((text1Length + text2Length) % 2 == 1);
            for (var d = 0; d < maxD; d++)
            {
                // Bail out if timeout reached.
                if (DateTime.Now > msEnd)
                {
                    return null;
                }

                // Walk the front path one step.
                vMap1.Add(new HashSet<long>());  // Adds at index 'd'.
                int x;
                int y;
                for (var k = -d; k <= d; k += 2)
                {
                    if (k == -d || k != d && v1[k - 1] < v1[k + 1])
                    {
                        x = v1[k + 1];
                    }
                    else
                    {
                        x = v1[k - 1] + 1;
                    }
                    y = x - k;
                    if (doubleEnd)
                    {
                        footstep = GetFootprint(x, y);
                        if (front && (footsteps.ContainsKey(footstep)))
                        {
                            done = true;
                        }
                        if (!front)
                        {
                            footsteps.Add(footstep, d);
                        }
                    }
                    while (!done && x < text1Length && y < text2Length
                           && text1.EqualsAt(x, text2, y))
                    {
                        x++;
                        y++;
                        if (doubleEnd)
                        {
                            footstep = GetFootprint(x, y);
                            if (front && (footsteps.ContainsKey(footstep)))
                            {
                                done = true;
                            }
                            if (!front)
                            {
                                footsteps.Add(footstep, d);
                            }
                        }
                    }
                    if (v1.ContainsKey(k))
                    {
                        v1[k] = x;
                    }
                    else
                    {
                        v1.Add(k, x);
                    }
                    vMap1[d].Add(GetFootprint(x, y));
                    if (x == text1Length && y == text2Length)
                    {
                        // Reached the end in single-path mode.
                        return DiffPath1(vMap1, text1, text2);
                    }
                    else if (done)
                    {
                        // Front path ran over reverse path.
                        vMap2 = vMap2.GetRange(0, footsteps[footstep] + 1);
                        List<Diff<T>> a = DiffPath1(vMap1, text1.Substring(0, x),
                                                        text2.Substring(0, y));
                        a.AddRange(DiffPath2(vMap2, text1.Substring(x), text2.Substring(y)));
                        return a;
                    }
                }

                if (doubleEnd)
                {
                    // Walk the reverse path one step.
                    vMap2.Add(new HashSet<long>());  // Adds at index 'd'.
                    for (var k = -d; k <= d; k += 2)
                    {
                        if (k == -d || k != d && v2[k - 1] < v2[k + 1])
                        {
                            x = v2[k + 1];
                        }
                        else
                        {
                            x = v2[k - 1] + 1;
                        }
                        y = x - k;
                        footstep = GetFootprint(text1Length - x, text2Length - y);
                        if (!front && (footsteps.ContainsKey(footstep)))
                        {
                            done = true;
                        }
                        if (front)
                        {
                            footsteps.Add(footstep, d);
                        }
                        while (!done && x < text1Length && y < text2Length
                               && text1.EqualsAt(text1Length - x - 1, text2, text2Length - y - 1))
                        {
                            x++;
                            y++;
                            footstep = GetFootprint(text1Length - x, text2Length - y);
                            if (!front && (footsteps.ContainsKey(footstep)))
                            {
                                done = true;
                            }
                            if (front)
                            {
                                footsteps.Add(footstep, d);
                            }
                        }
                        if (v2.ContainsKey(k))
                        {
                            v2[k] = x;
                        }
                        else
                        {
                            v2.Add(k, x);
                        }
                        vMap2[d].Add(GetFootprint(x, y));
                        if (done)
                        {
                            // Reverse path ran over front path.
                            vMap1 = vMap1.GetRange(0, footsteps[footstep] + 1);
                            List<Diff<T>> a
                                = DiffPath1(vMap1, text1.Substring(0, text1Length - x),
                                             text2.Substring(0, text2Length - y));
                            a.AddRange(DiffPath2(vMap2, text1.Substring(text1Length - x),
                                                text2.Substring(text2Length - y)));
                            return a;
                        }
                    }
                }
            }
            // Number of diffs equals number of characters, no commonality at all.
            return null;
        }

        internal static List<Diff<T>> DiffPath1<T>(IReadOnlyList<HashSet<long>> vMap,
                                        IReadOnlyList<T> text1, IReadOnlyList<T> text2)
        {
            var path = new LinkedList<Diff<T>>();
            var x = text1.Count;
            var y = text2.Count;
            Operation? lastOp = null;
            for (var d = vMap.Count - 2; d >= 0; d--)
            {
                while (true)
                {
                    if (vMap[d].Contains(GetFootprint(x - 1, y)))
                    {
                        x--;
                        if (lastOp == Operation.Delete)
                        {
                            path.First().Items = text1.Substring(x, 1).Concat(path.First().Items).ToList();
                        }
                        else
                        {
                            path.AddFirst(new Diff<T>(Operation.Delete,
                                                   text1.Substring(x, 1)));
                        }
                        lastOp = Operation.Delete;
                        break;
                    }

                    if (vMap[d].Contains(GetFootprint(x, y - 1)))
                    {
                        y--;
                        if (lastOp == Operation.Insert)
                        {
                            path.First().Items = text2.Substring(y, 1).Concat(path.First().Items).ToList();
                        }
                        else
                        {
                            path.AddFirst(new Diff<T>(Operation.Insert,
                                text2.Substring(y, 1)));
                        }
                        lastOp = Operation.Insert;
                        break;
                    }

                    x--;
                    y--;
                    if (lastOp == Operation.Equal)
                    {
                        path.First().Items = text1.Substring(x, 1).Concat(path.First().Items).ToList();
                    }
                    else
                    {
                        path.AddFirst(new Diff<T>(Operation.Equal, text1.Substring(x, 1)));
                    }
                    lastOp = Operation.Equal;
                }
            }
            return path.ToList();
        }

        internal static List<Diff<T>> DiffPath2<T>(IReadOnlyList<HashSet<long>> vMap,
                                        IReadOnlyList<T> text1, IReadOnlyList<T> text2)
        {
            var path = new LinkedList<Diff<T>>();
            var x = text1.Count;
            var y = text2.Count;
            Operation? lastOp = null;
            for (var d = vMap.Count - 2; d >= 0; d--)
            {
                while (true)
                {
                    if (vMap[d].Contains(GetFootprint(x - 1, y)))
                    {
                        x--;
                        if (lastOp == Operation.Delete)
                        {
                            path.Last().Items = path.Last().Items.Concat(text1.Substring(text1.Count - x - 1, 1)).ToList();
                        }
                        else
                        {
                            path.AddLast(new Diff<T>(Operation.Delete,
                                text1.Substring(text1.Count - x - 1, 1)));
                        }
                        lastOp = Operation.Delete;
                        break;
                    }

                    if (vMap[d].Contains(GetFootprint(x, y - 1)))
                    {
                        y--;
                        if (lastOp == Operation.Insert)
                        {
                            path.Last().Items = path.Last().Items.Concat(text2.Substring(text2.Count - y - 1, 1)).ToList();
                        }
                        else
                        {
                            path.AddLast(new Diff<T>(Operation.Insert,
                                text2.Substring(text2.Count - y - 1, 1)));
                        }
                        lastOp = Operation.Insert;
                        break;
                    }
                    x--;
                    y--;
                    //assert (text1.charAt(text1.Count - x - 1)
                    //        == text2.charAt(text2.Count - y - 1))
                    //      : "No diagonal.  Can't happen. (DiffPath2)";
                    if (lastOp == Operation.Equal)
                    {
                        path.Last().Items = path.Last().Items.Concat(text1.Substring(text1.Count - x - 1, 1)).ToList();
                    }
                    else
                    {
                        path.AddLast(new Diff<T>(Operation.Equal,
                            text1.Substring(text1.Count - x - 1, 1)));
                    }
                    lastOp = Operation.Equal;
                }
            }
            return path.ToList();
        }

        internal static long GetFootprint(int x, int y)
        {
            // The maximum size for a long is 9,223,372,036,854,775,807
            // The maximum size for an int is 2,147,483,647
            // Two ints fit nicely in one long.
            long result = x;
            result = result << 32;
            result += y;
            return result;
        }
        
        internal static int GetCommonPrefix<T>(IReadOnlyList<T> text1, IReadOnlyList<T> text2)
        {
            // Performance analysis: http://neil.fraser.name/news/2007/10/09/
            var n = Math.Min(text1.Count, text2.Count);
            for (var i = 0; i < n; i++)
            {
                if (!text1.EqualsAt(i, text2, i))
                {
                    return i;
                }
            }
            return n;
        }

        internal static int GetCommonSuffix<T>(IReadOnlyList<T> text1, IReadOnlyList<T> text2)
        {
            // Performance analysis: http://neil.fraser.name/news/2007/10/09/
            var text1Length = text1.Count;
            var text2Length = text2.Count;
            var n = Math.Min(text1.Count, text2.Count);
            for (var i = 1; i <= n; i++)
            {
                if (!text1.EqualsAt(text1Length - i, text2, text2Length - i))
                {
                    return i - 1;
                }
            }
            return n;
        }

        internal static IReadOnlyList<T>[] GetHalfMatch<T>(IReadOnlyList<T> text1, IReadOnlyList<T> text2)
        {
            var longtext = text1.Count > text2.Count ? text1 : text2;
            var shorttext = text1.Count > text2.Count ? text2 : text1;
            if (longtext.Count < 10 || shorttext.Count < 1)
            {
                return null;  // Pointless.
            }

            // First check if the second quarter is the seed for a half-match.
            var hm1 = GetHalfMatchI(longtext, shorttext,
                                           (longtext.Count + 3) / 4);
            // Check again based on the third quarter.
            var hm2 = GetHalfMatchI(longtext, shorttext,
                                           (longtext.Count + 1) / 2);
            IReadOnlyList<T>[] hm;
            if (hm1 == null && hm2 == null)
            {
                return null;
            }
            else if (hm2 == null)
            {
                hm = hm1;
            }
            else if (hm1 == null)
            {
                hm = hm2;
            }
            else
            {
                // Both matched.  Select the longest.
                hm = hm1[4].Count > hm2[4].Count ? hm1 : hm2;
            }

            // A half-match was found, sort out the return data.
            return text1.Count > text2.Count ? hm : new [] { hm[2], hm[3], hm[0], hm[1], hm[4] };
        }

        private static IReadOnlyList<T>[] GetHalfMatchI<T>(IReadOnlyList<T> longtext, IReadOnlyList<T> shorttext, int i)
        {
            // Start with a 1/4 length Substring at position i as a seed.
            var seed = longtext.Substring(i, longtext.Count / 4);
            var j = -1;
            IReadOnlyList<T> bestCommon = EmptyList<T>.Instance;
            IReadOnlyList<T> bestLongtextA = EmptyList<T>.Instance, bestLongtextB = EmptyList<T>.Instance;
            IReadOnlyList<T> bestShorttextA = EmptyList<T>.Instance, bestShorttextB = EmptyList<T>.Instance;
            while (j < shorttext.Count && (j = shorttext.IndexOf(seed, j + 1)) != -1)
            {
                var prefixLength = GetCommonPrefix(longtext.Substring(i),
                                                     shorttext.Substring(j));
                var suffixLength = GetCommonSuffix(longtext.Substring(0, i),
                                                     shorttext.Substring(0, j));
                if (bestCommon.Count < suffixLength + prefixLength)
                {
                    bestCommon = shorttext.Substring(j - suffixLength, suffixLength).Concat(
                        shorttext.Substring(j, prefixLength)).ToList();
                    bestLongtextA = longtext.Substring(0, i - suffixLength);
                    bestLongtextB = longtext.Substring(i + prefixLength);
                    bestShorttextA = shorttext.Substring(0, j - suffixLength);
                    bestShorttextB = shorttext.Substring(j + prefixLength);
                }
            }
            if (bestCommon.Count >= longtext.Count / 2)
            {
                return new[]{bestLongtextA, bestLongtextB,
                          bestShorttextA, bestShorttextB, bestCommon};
            }
            return null;
        }
        
        internal static void CleanupMerge<T>(List<Diff<T>> diffs)
        {
            while (true)
            {
                diffs.Add(new Diff<T>(Operation.Equal, EmptyList<T>.Instance)); // Add a dummy entry at the end.
                var pointer = 0;
                var countDelete = 0;
                var countInsert = 0;
                IReadOnlyList<T> textDelete = EmptyList<T>.Instance;
                IReadOnlyList<T> textInsert = EmptyList<T>.Instance;
                while (pointer < diffs.Count)
                {
                    switch (diffs[pointer].Operation)
                    {
                        case Operation.Insert:
                            countInsert++;
                            textInsert = textInsert.Concat(diffs[pointer].Items).ToList();
                            pointer++;
                            break;
                        case Operation.Delete:
                            countDelete++;
                            textDelete = textDelete.Concat(diffs[pointer].Items).ToList();
                            pointer++;
                            break;
                        case Operation.Equal:
                            // Upon reaching an equality, check for prior redundancies.
                            if (countDelete != 0 || countInsert != 0)
                            {
                                if (countDelete != 0 && countInsert != 0)
                                {
                                    // Factor out any common prefixies.
                                    var commonlength = GetCommonPrefix(textInsert, textDelete);
                                    if (commonlength != 0)
                                    {
                                        if ((pointer - countDelete - countInsert) > 0 && diffs[pointer - countDelete - countInsert - 1].Operation == Operation.Equal)
                                        {
                                            var diff = diffs[pointer - countDelete - countInsert - 1];
                                            diff.Items = diff.Items.Concat(textInsert.Substring(0, commonlength)).ToList();
                                        }
                                        else
                                        {
                                            diffs.Insert(0, new Diff<T>(Operation.Equal, textInsert.Substring(0, commonlength)));
                                            pointer++;
                                        }
                                        textInsert = textInsert.Substring(commonlength);
                                        textDelete = textDelete.Substring(commonlength);
                                    }
                                    // Factor out any common suffixies.
                                    commonlength = GetCommonSuffix(textInsert, textDelete);
                                    if (commonlength != 0)
                                    {
                                        diffs[pointer].Items = textInsert.Substring(textInsert.Count - commonlength).Concat(diffs[pointer].Items).ToList();
                                        textInsert = textInsert.Substring(0, textInsert.Count - commonlength);
                                        textDelete = textDelete.Substring(0, textDelete.Count - commonlength);
                                    }
                                }
                                // Delete the offending records and add the merged ones.
                                if (countDelete == 0)
                                {
                                    diffs.Splice(pointer - countDelete - countInsert, countDelete + countInsert, new Diff<T>(Operation.Insert, textInsert));
                                }
                                else if (countInsert == 0)
                                {
                                    diffs.Splice(pointer - countDelete - countInsert, countDelete + countInsert, new Diff<T>(Operation.Delete, textDelete));
                                }
                                else
                                {
                                    diffs.Splice(pointer - countDelete - countInsert, countDelete + countInsert, new Diff<T>(Operation.Delete, textDelete), new Diff<T>(Operation.Insert, textInsert));
                                }
                                pointer = pointer - countDelete - countInsert + (countDelete != 0 ? 1 : 0) + (countInsert != 0 ? 1 : 0) + 1;
                            }
                            else if (pointer != 0 && diffs[pointer - 1].Operation == Operation.Equal)
                            {
                                // Merge this equality with the previous one.
                                diffs[pointer - 1].Items = diffs[pointer - 1].Items.Concat(diffs[pointer].Items).ToList();
                                diffs.RemoveAt(pointer);
                            }
                            else
                            {
                                pointer++;
                            }
                            countInsert = 0;
                            countDelete = 0;
                            textDelete = EmptyList<T>.Instance;
                            textInsert = EmptyList<T>.Instance;
                            break;
                    }
                }
                if (diffs[diffs.Count - 1].Items.Count == 0)
                {
                    diffs.RemoveAt(diffs.Count - 1); // Remove the dummy entry at the end.
                }

                // Second pass: look for single edits surrounded on both sides by equalities
                // which can be shifted sideways to eliminate an equality.
                // e.g: A<ins>BA</ins>C -> <ins>AB</ins>AC
                var changes = false;
                pointer = 1;
                // Intentionally ignore the first and last element (don't need checking).
                while (pointer < (diffs.Count - 1))
                {
                    if (diffs[pointer - 1].Operation == Operation.Equal && diffs[pointer + 1].Operation == Operation.Equal)
                    {
                        // This is a single edit surrounded by equalities.
                        if (diffs[pointer].Items.EndsWith(diffs[pointer - 1].Items))
                        {
                            // Shift the edit over the previous equality.
                            diffs[pointer].Items = diffs[pointer - 1].Items.Concat(diffs[pointer].Items.Substring(0, diffs[pointer].Items.Count - diffs[pointer - 1].Items.Count)).ToList();
                            diffs[pointer + 1].Items = diffs[pointer - 1].Items.Concat(diffs[pointer + 1].Items).ToList();
                            diffs.Splice(pointer - 1, 1);
                            changes = true;
                        }
                        else if (diffs[pointer].Items.StartsWith(diffs[pointer + 1].Items))
                        {
                            // Shift the edit over the next equality.
                            diffs[pointer - 1].Items = diffs[pointer - 1].Items.Concat(diffs[pointer + 1].Items).ToList();
                            diffs[pointer].Items = diffs[pointer].Items.Substring(diffs[pointer + 1].Items.Count).Concat(diffs[pointer + 1].Items).ToList();
                            diffs.Splice(pointer + 1, 1);
                            changes = true;
                        }
                    }
                    pointer++;
                }
                // If shifts were made, the diff needs reordering and another shift sweep.
                if (changes)
                {
                    continue;
                }
                break;
            }
        }
    }
}