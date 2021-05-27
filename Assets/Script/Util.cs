using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace upc
{
    public class Defer
        : IDisposable
    {
        private readonly Action work;

        public Defer(Action workAtDispose)
        {
            work = workAtDispose;
        }

        public void Dispose()
        {
            work();
        }
    }

    /// <summary>
    ///     Dispose 될때까지의 시간을 logger에 표시
    /// </summary>
    /// <example>
    ///     using (new ElapsedTimeLogger("log message for disposing")) { ... }
    /// </example>
    public class ElapsedTimeLogger : IDisposable
    {
        public string Title { get; set; }

        public ElapsedTimeLogger(string title = "")
        {
            ++indent; // TODO: thread-safety
            Title = title;
            stopwatch.Start();
        }

        private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private bool disposedValue;
        private static int indent = 0;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    stopwatch.Stop();
                    var tabs = new string('\t', --indent);
                    Debug.Log($"{tabs}{Title} (elapsed {stopwatch.Elapsed.ToHumanTimeString()})");
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public static class SystemExt
    {
        public static string ToHumanTimeString(this TimeSpan span, int significantDigits = 3)
        { // https://www.extensionmethod.net/csharp/timespan/timespan-tohumantimestring
            var format = "G" + significantDigits;
            return span.TotalMilliseconds < 1000 ? span.TotalMilliseconds.ToString(format) + " milliseconds"
                : (span.TotalSeconds < 60 ? span.TotalSeconds.ToString(format) + " seconds"
                    : (span.TotalMinutes < 60 ? span.TotalMinutes.ToString(format) + " minutes"
                        : (span.TotalHours < 24 ? span.TotalHours.ToString(format) + " hours"
                                                : span.TotalDays.ToString(format) + " days")));
        }

        public static void Fill<T>(this T[] array, T value, int startIndex, int count)
        {
            if (array.Length < startIndex + count) throw new IndexOutOfRangeException();
            while (count-- > 0)
            {
                array[startIndex + count] = value;
            }
        }

        public static void Ellipsoid(this short[] src, Vector3Int dim, Vector3Int pos, Vector3Int radius, short val = 1)
        { // see UnityExt.FillEllipsoid
            Debug.Assert(src.Length == dim.x * dim.y * dim.z);

            var rxSqr = radius.x * radius.x;
            var rySqr = radius.y * radius.y;
            var rzSqr = radius.z * radius.z;
            var rSqr = rxSqr * rySqr * rzSqr;

            for (var x = pos.x - radius.x; x < pos.x + radius.x + 1; ++x)
            {
                if (x < 0 || x >= dim.x) continue;
                var xSqr = (pos.x - x) * (pos.x - x);
                for (var y = pos.y - radius.y; y < pos.y + radius.y + 1; ++y)
                {
                    if (y < 0 || y >= dim.y) continue;
                    var ySqr = (pos.y - y) * (pos.y - y);
                    for (var z = pos.z - radius.z; z < pos.z + radius.z + 1; ++z)
                    {
                        if (z < 0 || z >= dim.z) continue;
                        var zSqr = (pos.z - z) * (pos.z - z);
                        if (xSqr * rySqr * rzSqr + ySqr * rxSqr * rzSqr + zSqr * rxSqr * rySqr < rSqr)
                        {
                            src[x + y * dim.x + z * dim.x * dim.y] = val;
                        }
                    }
                }
            }
        }

        public static void FillEllipsoid(this short[] src, Vector3Int dim, short val, Vector3Int pos, Vector3Int radius)
        {
            Ellipsoid(src, dim, pos, radius, val);
        }

        public static void Ellipse(this short[] src, Vector3Int dim, Vector3Int pos, Vector3Int radius, short val = 1)
        {
            Debug.Assert(src.Length == dim.x * dim.y * dim.z);

            var rxSqr = radius.x == 0 ? 1 : radius.x * radius.x;
            var rySqr = radius.y == 0 ? 1 : radius.y * radius.y;
            var rzSqr = radius.z == 0 ? 1 : radius.z * radius.z;
            var rSqr = rxSqr * rySqr * rzSqr;

            for (var x = pos.x - radius.x; x < pos.x + radius.x + 1; ++x)
            {
                if (x < 0 || x >= dim.x) continue;
                var xSqr = (pos.x - x) * (pos.x - x);
                for (var y = pos.y - radius.y; y < pos.y + radius.y + 1; ++y)
                {
                    if (y < 0 || y >= dim.y) continue;
                    var ySqr = (pos.y - y) * (pos.y - y);
                    for (var z = pos.z - radius.z; z < pos.z + radius.z + 1; ++z)
                    {
                        if (z < 0 || z >= dim.z) continue;
                        var zSqr = (pos.z - z) * (pos.z - z);
                        if (xSqr * rySqr * rzSqr + ySqr * rxSqr * rzSqr + zSqr * rxSqr * rySqr < rSqr)
                        {
                            src[x + y * dim.x + z * dim.x * dim.y] = val;
                        }
                    }
                }
            }
        }

        public static void FillEllipse(this short[] src, Vector3Int dim, short val, Vector3Int pos, Vector3Int radius)
        {
            Ellipse(src, dim, pos, radius, val);
        }

        public static short Clamp(this short v, short min, short max)
        {
            if (v > max) v = max;
            if (v < min) v = min;
            return v;
        }

        public static (short min, short max) MinMax(this IEnumerable<short> v)
        {
            (short min, short max) = (short.MaxValue, short.MinValue);
            foreach (var i in v)
            {
                if (min > i) min = i;
                if (max < i) max = i;
            }
            return (min, max);
        }

        public static string Sha1(this string text)
        {
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var bytes = sha1.ComputeHash(System.Text.Encoding.Default.GetBytes(text));

                // https://gist.github.com/kristopherjohnson/3021045
                var sb = new System.Text.StringBuilder();
                foreach (byte b in bytes)
                {
                    var hex = b.ToString("x2");
                    sb.Append(hex);
                }
                return sb.ToString();
            }
        }

        public static DateTime? ParseToDateTime(this string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            return DateTime.Parse(text);
        }

        public static async Task Start(this System.Threading.Thread thread, System.Threading.CancellationToken ct, Action onProgress = null)
        {
            thread.Start();
            while (thread.IsAlive)
            {
                onProgress?.Invoke();
                if (ct.IsCancellationRequested)
                {
                    thread.Abort();
                    ct.ThrowIfCancellationRequested();
                }
                await Task.Delay(100);
            }
        }

        public static T[,,] To3D<T>(this T[] source, int w, int h)
        {
            int d = source.Length / w * h;
            Debug.Assert(source.Length == w * h * d);
            T[,,] buff3D = new T[h, w, d];
            Buffer.BlockCopy(source, 0, buff3D, 0, h * w);
            return buff3D;
        }
    }
}