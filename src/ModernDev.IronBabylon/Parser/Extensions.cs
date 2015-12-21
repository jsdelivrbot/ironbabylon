using System.Collections.Generic;
using System.Linq;
using static System.Convert;

namespace ModernDev.IronBabylon
{
    public static class Extensions
    {
        public static T As<T>(this object @this) => (T) @this;

        public static string Slice(this string source, int start, int end)
        {
            if (start < 0)
            {
                start = source.Length + start;
            }

            if (end < 0)
            {
                end = source.Length + end;
            }

            var len = end - start;

            return start >= source.Length ? string.Empty : source.Substring(start, len);
        }

        public static string Slice(this string source, int start) => source.Slice(start, source.Length - 1);

        /// <summary>
        /// Returns the characters in a string beginning at the specified location through the specified number of characters.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="start">Location at which to begin extracting characters.
        /// If a negative number is given, it is treated as source.Length + start where source.Length is the length of the string
        /// (for example, if start is -3 it is treated as sourceLength - 3.)</param>
        /// <param name="length">The number of characters to extract.</param>
        /// <returns></returns>
        public static string Substr(this string source, int start, int length)
        {
            if (start < 0)
            {
                start = source.Length + start;
            }

            if (length < 0 || length == 0)
            {
                return string.Empty;
            }

            if (length >= source.Length || length > source.Length - start)
            {
                length = source.Length - start;
            }

            return source.Substring(start, length);
        }

        public static string Substr(this string source, int start)
        {
            return source.Substr(start, source.Length);
        }

        public static T Pop<T>(this IList<T> list)
        {
            var r = list.Last();

            list.RemoveAt(list.Count - 1);

            return r;
        }

        public static List<T> Splice<T>(this List<T> source, int index, int count)
        {
            var items = source.GetRange(index, count);

            source.RemoveRange(index, count);

            return items;
        }

        /// <summary>
        /// Get the array slice between the two indexes.
        /// ... Inclusive for start index, exclusive for end index.
        /// </summary>
        public static List<T> Slice<T>(this List<T> source, int start, int end)
        {
            // Handles negative ends.
            if (end < 0)
            {
                end = source.Count + end;
            }

            end = end > source.Count ? source.Count : end;

            var len = end - start;

            // Return new array.
            var res = new List<T>();

            for (var i = 0; i < len; i++)
            {
                //res[i] = source[i + start];
                res.Add(source[i + start]);
            }
            return res;
        }

        public static List<T> Slice<T>(this List<T> source, int start) => source.Slice(start, source.Count);

        public static bool ToBool(this int num) => ToBoolean(num);

        public static bool ToBool(this int? num) => ToBoolean(num);

        public static bool ToBool(this object obj)
            => obj is string ? !string.IsNullOrEmpty((string) obj) : ToBoolean(obj);

        public static int CharCodeAt(this string source, int position, int defaultReturn = int.MaxValue)
            => position >= source.Length ? defaultReturn : source[position];
    }
}
