using System.Net;
using System.Text;
using FluentAssertions;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// A target that purges its Cloudflare zone after a publish, against a local stand-in for
/// Cloudflare's API: exactly the addresses sent are purged, 30 a request, with the target's token;
/// a purge that fails is kept in the history without failing the publish; a target without a zone
/// asks nothing; and a zone without a public URL or that is not a zone id is refused.
/// </summary>
public sealed class AssetPublishCloudflareTests : IDisposable
{
    private const string ZONE = "0123456789abcdef0123456789abcdef";
    private const string TOKEN = "cf-token-secret";
    private const string PUBLIC_URL = "https://assets.example.com/";

    private readonly AssetPublishHotel _hotel = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _hotel.Dispose();

    [Fact]
    public async Task A_publish_purges_exactly_the_addresses_it_sent_thirty_a_request()
    {
        for (var i = 0; i < 31; i++)
            await _hotel.PutBundleAsync($"chair{i:00}", Bytes($"chair{i}", 10 + i), Ct);

        var target = await AddTargetAsync(ZONE, TOKEN);

        target.CloudflareZoneId.Should().Be(ZONE);
        target.HasCloudflareToken.Should().BeTrue();

        var job = await _hotel.PublishAsync(target.Id, Ct);

        job.Status.Should().Be(AssetJobStatus.Done);
        job.Result.Should().Contain("31 purged from Cloudflare");

        var requests = _hotel.Cloudflare.Requests.ToList();

        requests.Should().HaveCount(2);
        requests
            .Should()
            .AllSatisfy(x =>
            {
                x.Url.Should().Be($"{AssetPublishHotel.CLOUDFLARE_API}/zones/{ZONE}/purge_cache");
                x.Authorization.Should().Be($"Bearer {TOKEN}");
            });
        requests.Select(x => x.Files.Length).Should().BeEquivalentTo([30, 1]);
        requests
            .SelectMany(x => x.Files)
            .Should()
            .BeEquivalentTo(
                Enumerable
                    .Range(0, 31)
                    .Select(i => $"https://assets.example.com/bundled/furniture/chair{i:00}.nitro")
            );

        var entry = (await _hotel.Publishing.GetHistoryAsync(target.Id, Ct))!.Single();

        entry.Purged.Should().Be(31);
        entry.PurgeError.Should().BeNull();

        // The next publish sends one changed bundle, and purges it alone.
        await _hotel.PutBundleAsync("chair07", Bytes("changed", 99), Ct);
        _hotel.Cloudflare.Requests.Clear();

        await _hotel.PublishAsync(target.Id, Ct);

        _hotel
            .Cloudflare.Requests.Single()
            .Files.Should()
            .Equal("https://assets.example.com/bundled/furniture/chair07.nitro");

        // A publish that sends nothing asks nothing.
        _hotel.Cloudflare.Requests.Clear();

        await _hotel.PublishAsync(target.Id, Ct);

        _hotel.Cloudflare.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_purge_that_fails_is_kept_in_the_history_and_the_publish_still_counts()
    {
        await _hotel.PutBundleAsync("chair", Bytes("chair", 300), Ct);
        var target = await AddTargetAsync(ZONE, TOKEN);

        _hotel.Cloudflare.Status = HttpStatusCode.Forbidden;
        _hotel.Cloudflare.Error = "Authentication error";

        var job = await _hotel.PublishAsync(target.Id, Ct);

        job.Status.Should().Be(AssetJobStatus.Done);
        job.Result.Should().Contain("Cloudflare purge failed: HTTP 403: Authentication error");
        job.Log.Should().Contain(x => x.Contains("Cloudflare did not purge 1 addresses"));
        job.Log.Should().NotContain(x => x.Contains(TOKEN));

        var entry = (await _hotel.Publishing.GetHistoryAsync(target.Id, Ct))!.Single();

        entry.Uploaded.Should().Be(1);
        entry.Error.Should().BeNull();
        entry.Purged.Should().Be(0);
        entry.PurgeError.Should().Be("HTTP 403: Authentication error (10000)");
        (await _hotel.RecordedAsync(target.Id, Ct))
            .Should()
            .ContainKey("bundled/furniture/chair.nitro");
    }

    [Fact]
    public async Task A_target_without_a_zone_or_a_token_purges_nothing()
    {
        await _hotel.PutBundleAsync("chair", Bytes("chair", 300), Ct);
        var none = await AddTargetAsync("", null);
        var noToken = await AddTargetAsync(ZONE, null, Path.Combine(_hotel.TargetFolder, "b"));

        await _hotel.PublishAsync(none.Id, Ct);
        await _hotel.PublishAsync(noToken.Id, Ct);

        _hotel.Cloudflare.Requests.Should().BeEmpty();
        (await _hotel.Publishing.GetHistoryAsync(none.Id, Ct))!.Single().Purged.Should().Be(0);
    }

    [Fact]
    public async Task A_saved_token_is_kept_when_left_out_and_removed_when_emptied()
    {
        var target = await AddTargetAsync(ZONE, TOKEN);
        var kept = await _hotel.Publishing.UpdateTargetAsync(target.Id, Edit(ZONE, null), Ct);

        kept!.HasCloudflareToken.Should().BeTrue();

        var removed = await _hotel.Publishing.UpdateTargetAsync(target.Id, Edit(ZONE, ""), Ct);

        removed!.HasCloudflareToken.Should().BeFalse();
    }

    [Theory]
    [InlineData("not-a-zone", PUBLIC_URL)]
    [InlineData("0123456789abcdef0123456789abcdeg", PUBLIC_URL)]
    [InlineData(ZONE, "")]
    public async Task A_zone_that_could_not_be_purged_is_refused(string zone, string publicUrl)
    {
        var refused = () =>
            _hotel.Publishing.CreateTargetAsync(
                Edit(zone, TOKEN) with
                {
                    PublicUrl = publicUrl,
                },
                Ct
            );

        await refused.Should().ThrowAsync<ArgumentException>();
    }

    private Task<AssetPublishTargetSnapshot> AddTargetAsync(
        string zone,
        string? token,
        string? folder = null
    ) => _hotel.Publishing.CreateTargetAsync(Edit(zone, token, folder), Ct);

    private AssetPublishTargetEdit Edit(string zone, string? token, string? folder = null) =>
        new()
        {
            Name = "Web root",
            Protocol = AssetPublishProtocol.Folder,
            RemotePath = folder ?? _hotel.TargetFolder,
            PublicUrl = PUBLIC_URL,
            CloudflareZoneId = zone,
            CloudflareToken = token,
        };

    private static byte[] Bytes(string seed, int size)
    {
        var bytes = new byte[size];
        var source = Encoding.UTF8.GetBytes(seed);

        for (var i = 0; i < size; i++)
            bytes[i] = source[i % source.Length];

        return bytes;
    }
}
