using System.Collections.Concurrent;

namespace TinyUrl.Tests;

[TestClass]
public sealed class Tests
{
    [TestMethod]
    public async Task AddConcurrently()
    {
        var tiny = new TinyUrl();

        var results = new ConcurrentBag<(bool CreateResult, string? ShortUrl)>();

        void Call(int i)
        {
            var res = tiny.TryCreate(new Uri($"https://example.com/{i}"), out var shortUrl);

            results.Add((res, shortUrl));
        }

        const int Count = 1000000;

        // add multiple urls concurrently
        await Task.WhenAll(Enumerable.Range(0, Count).Select(i => Task.Run(() => Call(i))));

        // check all unique
        Assert.AreEqual(Count, results.Distinct().Count());

        // check all fetched Urls are unique
        Assert.AreEqual(Count, results.Select(t => { tiny.TryGetUrl(t.ShortUrl!, out var u); return u; }).Distinct().Count());

        // check all fetched once 
        var click = results.Select(t => { tiny.TryGetStatistics(t.ShortUrl!, out var c); return c; }).Distinct().ToArray();
        Assert.AreEqual(1, click.Length);
        Assert.AreEqual(1, click[0]);
    }

    [TestMethod]
    public async Task ConcurrentStatstics()
    {
        var tiny = new TinyUrl(1000);

        var uri = new Uri("https://example.com/");

        Assert.IsTrue(tiny.TryCreate(uri, out var shortUrl));

        var urls = new ConcurrentBag<(bool, Uri?)>();

        void Call(int i)
        {
            var r = tiny.TryGetUrl(shortUrl, out var uri);

            urls.Add((r, uri));
        }

        const int Count = 1000000;

        // fetch the same concurrently
        await Task.WhenAll(Enumerable.Range(0, Count).Select(i => Task.Run(() => Call(i))));

        // check statstics is avalable
        Assert.IsTrue(tiny.TryGetStatistics(shortUrl, out var clickCount));

        // check click count is correct
        Assert.AreEqual(Count, clickCount);

        // check all fetches returned correct long url
        var result = urls.Distinct().ToArray();

        Assert.AreEqual(1, result.Length);
        Assert.AreEqual((true, uri), result[0]);
    }

    [TestMethod]
    public void ManuallyAdded()
    {
        var uri = new Uri("https://example.com/");

        // the first short url produced
        Assert.IsTrue(new TinyUrl().TryCreate(uri, out var firstShortUrl));

        var tiny = new TinyUrl();

        // add manualy and check it succeeds
        Assert.IsTrue(tiny.TryPut(uri, firstShortUrl));

        // try to add manualy and check it fails
        Assert.IsFalse(tiny.TryPut(uri, firstShortUrl));

        // add automatic and check it succeeds and does not collide with manually added.
        Assert.IsTrue(tiny.TryCreate(uri, out var shortUrl));
        Assert.AreNotEqual(firstShortUrl, shortUrl);
    }

    [TestMethod]
    public void CheckChardingLimits()
    {
        var uri = new Uri("https://example.com/");

        var tiny = new TinyUrl(0, 1);

        // add manualy and check it succeeds
        Assert.IsTrue(tiny.TryCreate(uri, out _));

        // add manualy again and check it fails
        Assert.IsFalse(tiny.TryCreate(uri, out _));
    }

    [TestMethod]
    public void CheckDelete()
    {
        var uri = new Uri("https://example.com/");

        var tiny = new TinyUrl();

        // add manualy and check it succeeds
        Assert.IsTrue(tiny.TryCreate(uri, out var first));

        // add manualy another and check it succeeds
        Assert.IsTrue(tiny.TryCreate(uri, out var second));

        // check both are retrable
        Assert.IsTrue(tiny.TryGetUrl(first, out _));
        Assert.IsTrue(tiny.TryGetUrl(second, out _));

        // delete and check it succeeds
        Assert.IsTrue(tiny.TryDelete(first));

        // get the first and it fails
        Assert.IsFalse(tiny.TryGetUrl(first, out _));

        // get the second and it succeeds
        Assert.IsTrue(tiny.TryGetUrl(second, out _));

        // delete first that is missing
        Assert.IsFalse(tiny.TryDelete(first));

        // get the first and it fails
        Assert.IsFalse(tiny.TryGetUrl(first, out _));
    }
}
