using System.Collections.Generic;
using System.Linq;

namespace ListDiff
{    
    internal static class ListExtensions
    {
        public static List<T> Splice<T>(this List<T> input, int start, int count, params T[] objects)
        {
            var deletedRange = input.GetRange(start, count);
            input.RemoveRange(start, count);
            input.InsertRange(start, objects);
            return deletedRange;
        }

        public static bool StartsWith<T>(this IReadOnlyList<T> target, IReadOnlyList<T> other)
        {
            return target.Count >= other.Count && target.Take(other.Count).SequenceEqual(other);
        }

        public static bool EndsWith<T>(this IReadOnlyList<T> target, IReadOnlyList<T> other)
        {
            return target.Count >= other.Count && target.Skip(target.Count - other.Count).Take(other.Count).SequenceEqual(other);
        }

        public static IReadOnlyList<T> Substring<T>(this IReadOnlyList<T> target, int start, int length = -1)
        {
            if (length == -1)
            {
                length = target.Count - start;
            }
            var list = target as List<T>;
            return list?.GetRange(start, length) ?? target.Skip(start).Take(length).ToList();
        }

        private static bool CompareRange<T>(IReadOnlyList<T> listA, int offsetA, IReadOnlyList<T> listB, int offsetB, int count)
        {
            for (var j = 0; j < count; j++)
            {
                if (!EqualityComparer<T>.Default.Equals(listA[offsetA + j], listB[offsetB + j]))
                {
                    return false;
                }
            }

            return true;
        }

        public static int IndexOf<T>(this IReadOnlyList<T> target, IReadOnlyList<T> other, int start = 0)
        {
            var end = target.Count - other.Count;
            for (var i = start; i < end; i++)
            {
                if (CompareRange(target, i, other, 0, other.Count))
                {
                    return i;
                }                
            }

            return -1;
        }

        public static bool EqualsAt<T>(this IReadOnlyList<T> target, int targetPos, IReadOnlyList<T> other, int otherPos)
        {
            return EqualityComparer<T>.Default.Equals(target[targetPos], other[otherPos]);            
        }        
    }
}