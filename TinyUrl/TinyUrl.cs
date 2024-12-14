using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace TinyUrl;

/// <summary>
/// A tiny URL implementation.
/// </summary>
public class TinyUrl
{
    readonly ConcurrentDictionary<string, UriInfo> urls = new();

    int current;
    readonly int max;
    readonly string alphabet;
    readonly int maxLength; // max length of tiny url string

    /// <summary>
    /// Creates an instance of the <see cref="TinyUrl"/> class.
    /// </summary>
    /// <param name="start">Starting offset. To allow to use sharding.</param>
    /// <param name="max">Max offset.</param>
    /// <param name="alphabet">Alphabet for tiny URLs.</param>
    public TinyUrl(int start = 0, int max = int.MaxValue, string alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(max, start);
        ArgumentException.ThrowIfNullOrEmpty(alphabet);
        ArgumentOutOfRangeException.ThrowIfNotEqual(alphabet.Length, alphabet.Distinct().Count(), nameof(alphabet));

        current = start;
        this.max = max;
        this.alphabet = alphabet;
        maxLength = (int)(Math.Log(uint.MaxValue, alphabet.Length) + 1);
    }

    /// <summary>
    /// Tries to put the <paramref name="uri"/> wth assocated <paramref name="shortUrl"/>.
    /// </summary>
    /// <param name="uri">A <see cref="Uri"/> to put.</param>
    /// <param name="shortUrl">A short URL to assocate wth.</param>
    /// <returns><see langword="true"/> if the <paramref name="uri"/> was successfully put and <see langword="false"/> if the <paramref name="shortUrl"/> is already used.</returns>
    public bool TryPut(Uri uri, string shortUrl)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentException.ThrowIfNullOrEmpty(shortUrl);

        if (!shortUrl.All(alphabet.Contains))
        {
            throw new ArgumentOutOfRangeException(nameof(shortUrl), "contains character out of alphabet.");
        }

        return urls.TryAdd(shortUrl, new(uri));
    }

    /// <summary>
    /// Tries to create a new short url for the <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">A <see cref="Uri"/> to put.</param>
    /// <returns><see langword="true"/> if the <paramref name="uri"/> was successfully created and put and <see langword="false"/> if upper bound is reached.</returns>
    public bool TryCreate(Uri uri, [NotNullWhen(true)] out string? shortUrl)
    {
        shortUrl = null;

        if (current >= 0 && current <= max)
        {
            var info = new UriInfo(uri);

            Span<char> span = stackalloc char[maxLength];

            do
            {
                var cur = Interlocked.Increment(ref current);
                if (cur < 0 || cur > max)
                {
                    return false;
                }

                var id = Fnv1Hash(cur);

                var currentCharIndex = 0;
                if (id == 0)
                {
                    span[0] = alphabet[0];

                    currentCharIndex = 1;
                }
                else
                {
                    while (id > 0)
                    {
                        var (quotient, remainder) = uint.DivRem(id, (uint)alphabet.Length);
                        id = quotient;
                        span[currentCharIndex++] = alphabet[(int)remainder];
                    }
                }

                shortUrl = new string(span[..currentCharIndex]);
            }
            while (!urls.TryAdd(shortUrl, info));

            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to delete <paramref name="shortUrl"/>. 
    /// </summary>
    /// <param name="shortUrl">A short URL.</param>
    /// <returns><see langword="true"/> if the <paramref name="shortUrl"/> was successfully removed and <see langword="false"/> if no URL is associated with <paramref name="shortUrl"/>.</returns>
    public bool TryDelete(string shortUrl) =>
        urls.TryRemove(shortUrl, out _);

    /// <summary>
    /// Tries to get the <see cref="Uri"/> for a short URL.
    /// </summary>
    /// <param name="shortUrl">A short url.</param>
    /// <param name="uri">The <see cref="Uri"/> associated with the <paramref name="shortUrl"/>.</param>
    /// <returns><see langword="true"/> if the associated <see cref="Uri"/> was successfully retrieved and <see langword="false"/> if no URL is associated with <paramref name="shortUrl"/>.</returns>
    public bool TryGetUrl(string shortUrl, [NotNullWhen(true)] out Uri? uri)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(shortUrl);

        if (urls.TryGetValue(shortUrl, out var info))
        {
            info.Increment();
            uri = info.Uri;

            return true;
        }

        uri = null;

        return false;
    }

    /// <summary>
    /// Tries to get statistics for a short URL.
    /// </summary>
    /// <param name="shortUrl">A short url.</param>
    /// <param name="clickCount">"Click" (retreve) count.</param>
    /// <returns><see langword="true"/> if the statistic <paramref name="shortUrl"/> was successfully retrieved and <see langword="false"/> if no URL is associated with <paramref name="shortUrl"/>.</returns>
    public bool TryGetStatistics(string shortUrl, out int clickCount)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(shortUrl);

        if (urls.TryGetValue(shortUrl, out var info))
        {
            clickCount = info.ClickCount;

            return true;
        }

        clickCount = 0;

        return false;
    }

    // a hash to make short urls "random" and make calculaton of the next/prevous harder.
    // more cryptographycally strong hash can be used but performance will degrade
    // alphabet randomzaton can help without a perf hit but can be discovered
    static uint Fnv1Hash(int value)
    {
        const uint prime = 16777619;
        const uint offset = 2166136261;

        var v = unchecked((uint)value);

        var hash = offset;
        for (var i = 0; i < sizeof(uint); i++)
        {
            var dataByte = v & 0xFF;
            v >>= 4;

            hash *= prime;

            hash ^= dataByte;
        }

        return hash;
    }

    class UriInfo(Uri uri)
    {
        volatile int clickCount;

        internal int Increment() =>
            Interlocked.Increment(ref clickCount);

        public Uri Uri { get; } = uri;

        public int ClickCount { get => clickCount; }
    }
}