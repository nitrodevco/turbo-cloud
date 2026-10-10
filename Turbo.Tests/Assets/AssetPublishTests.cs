using System.Text;
using FluentAssertions;
using Turbo.Gamedata.Assets.Publishing;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// Publishing bundles to a folder target, end to end through the real store, jobs and publish
/// service: the first publish sends everything and records it, the next sends only what changed,
/// "delete removed" takes away what the hotel dropped, a dry run only counts, and each publish is
/// kept in the target's history.
/// </summary>
public sealed class AssetPublishTests : IDisposable
{
    private readonly AssetPublishHotel _hotel = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _hotel.Dispose();

    [Fact]
    public async Task The_first_publish_sends_every_bundle_and_records_it()
    {
        await _hotel.PutBundleAsync("chair", Bytes("chair", 300), Ct);
        await _hotel.PutBundleAsync("table", Bytes("table", 900), Ct);
        await _hotel.PutBundleAsync("lamp", Bytes("lamp", 50), Ct);
        var target = await _hotel.AddFolderTargetAsync(Ct);

        target.Pending.Should().Be(3);

        var job = await _hotel.PublishAsync(target.Id, Ct);

        job.Status.Should().Be(AssetJobStatus.Done);
        job.Kind.Should().Be(AssetJobKind.Publish);
        job.Title.Should().Be("Publish to Web root");
        job.Failed.Should().Be(0);

        foreach (var (name, size) in new[] { ("chair", 300), ("table", 900), ("lamp", 50) })
            (await File.ReadAllBytesAsync(_hotel.TargetFileOf(name), Ct))
                .Should()
                .Equal(Bytes(name, size));

        Directory
            .EnumerateFiles(_hotel.TargetFolder, "*.part", SearchOption.AllDirectories)
            .Should()
            .BeEmpty();
        (await _hotel.RecordedAsync(target.Id, Ct))
            .Keys.Should()
            .BeEquivalentTo(
                "bundled/furniture/chair.nitro",
                "bundled/furniture/table.nitro",
                "bundled/furniture/lamp.nitro"
            );
        (await _hotel.Publishing.GetTargetAsync(target.Id, Ct))!.Pending.Should().Be(0);

        var entry = (await _hotel.Publishing.GetHistoryAsync(target.Id, Ct))!.Single();

        entry.Uploaded.Should().Be(3);
        entry.Skipped.Should().Be(0);
        entry.Bytes.Should().Be(1250);
        entry.DryRun.Should().BeFalse();
        entry.PlayerId.Should().Be(AssetPublishHotel.STAFF.Value);
        entry.Error.Should().BeNull();
        entry.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_second_publish_sends_nothing_and_a_changed_bundle_alone_is_sent_again()
    {
        await _hotel.PutBundleAsync("chair", Bytes("chair", 300), Ct);
        await _hotel.PutBundleAsync("table", Bytes("table", 900), Ct);
        var target = await _hotel.AddFolderTargetAsync(Ct);
        await _hotel.PublishAsync(target.Id, Ct);

        // Changed on the target behind the hotel's back: a publish that sent it again would undo this.
        await File.WriteAllTextAsync(_hotel.TargetFileOf("table"), "left alone", Ct);

        var again = await _hotel.PublishAsync(target.Id, Ct);

        again.Status.Should().Be(AssetJobStatus.Done);
        (await File.ReadAllTextAsync(_hotel.TargetFileOf("table"), Ct)).Should().Be("left alone");

        await _hotel.PutBundleAsync("chair", Bytes("chair-v2", 400), Ct);
        (await _hotel.Publishing.GetTargetAsync(target.Id, Ct))!.Pending.Should().Be(1);

        await _hotel.PublishAsync(target.Id, Ct);

        (await File.ReadAllBytesAsync(_hotel.TargetFileOf("chair"), Ct))
            .Should()
            .Equal(Bytes("chair-v2", 400));
        (await File.ReadAllTextAsync(_hotel.TargetFileOf("table"), Ct)).Should().Be("left alone");

        var history = (await _hotel.Publishing.GetHistoryAsync(target.Id, Ct))!;

        history.Select(x => (x.Uploaded, x.Skipped)).Should().Equal((1, 1), (0, 2), (2, 0));
    }

    [Fact]
    public async Task Delete_removed_takes_away_what_the_hotel_dropped_and_only_when_asked()
    {
        await _hotel.PutBundleAsync("chair", Bytes("chair", 300), Ct);
        await _hotel.PutBundleAsync("table", Bytes("table", 900), Ct);
        var target = await _hotel.AddFolderTargetAsync(Ct);
        await _hotel.PublishAsync(target.Id, Ct);

        await _hotel.DropBundleAsync("table", Ct);
        await _hotel.PublishAsync(target.Id, Ct);

        File.Exists(_hotel.TargetFileOf("table")).Should().BeTrue();

        var job = await _hotel.PublishAsync(target.Id, Ct, deleteRemoved: true);

        job.Status.Should().Be(AssetJobStatus.Done);
        File.Exists(_hotel.TargetFileOf("table")).Should().BeFalse();
        File.Exists(_hotel.TargetFileOf("chair")).Should().BeTrue();
        (await _hotel.RecordedAsync(target.Id, Ct))
            .Keys.Should()
            .Equal("bundled/furniture/chair.nitro");
        (await _hotel.Publishing.GetHistoryAsync(target.Id, Ct))![0].Deleted.Should().Be(1);
    }

    [Fact]
    public async Task A_dry_run_counts_what_would_be_sent_and_sends_nothing()
    {
        await _hotel.PutBundleAsync("chair", Bytes("chair", 300), Ct);
        await _hotel.PutBundleAsync("table", Bytes("table", 900), Ct);
        var target = await _hotel.AddFolderTargetAsync(Ct);

        var job = await _hotel.PublishAsync(target.Id, Ct, dryRun: true);

        job.Status.Should().Be(AssetJobStatus.Done);
        job.Result.Should().StartWith("2 to send").And.Contain("0 already there, 0 to delete");
        job.Log.Should().ContainSingle().Which.Should().Be(job.Result);
        Directory.EnumerateFileSystemEntries(_hotel.TargetFolder).Should().BeEmpty();
        (await _hotel.RecordedAsync(target.Id, Ct)).Should().BeEmpty();
        (await _hotel.Publishing.GetTargetAsync(target.Id, Ct))!.Pending.Should().Be(2);

        var entry = (await _hotel.Publishing.GetHistoryAsync(target.Id, Ct))!.Single();

        entry.DryRun.Should().BeTrue();
        entry.Uploaded.Should().Be(0);
        entry.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_file_that_fails_is_logged_and_counted_while_the_rest_are_sent()
    {
        await _hotel.PutBundleAsync("chair", Bytes("chair", 300), Ct);
        await _hotel.PutBundleAsync("table", Bytes("table", 900), Ct);
        // Its row says it has a file; the folder doesn't.
        File.Delete(_hotel.Store.FullPathOf(AssetBundleKind.Furniture, "table"));
        var target = await _hotel.AddFolderTargetAsync(Ct);

        var job = await _hotel.PublishAsync(target.Id, Ct);

        job.Status.Should().Be(AssetJobStatus.Done);
        job.Failed.Should().Be(1);
        job.Log.Should().ContainSingle(x => x.StartsWith("bundled/furniture/table.nitro"));
        File.Exists(_hotel.TargetFileOf("chair")).Should().BeTrue();
        (await _hotel.RecordedAsync(target.Id, Ct))
            .Keys.Should()
            .Equal("bundled/furniture/chair.nitro");

        var entry = (await _hotel.Publishing.GetHistoryAsync(target.Id, Ct))!.Single();

        entry.Uploaded.Should().Be(1);
        entry.Error.Should().Contain("1 files");
    }

    [Fact]
    public async Task A_target_that_cannot_be_reached_fails_the_publish_and_says_why_in_its_history()
    {
        await _hotel.PutBundleAsync("chair", Bytes("chair", 300), Ct);
        var missing = Path.Combine(_hotel.TargetFolder, "not-there");
        var target = await _hotel.AddFolderTargetAsync(Ct, missing);

        var job = await _hotel.PublishAsync(target.Id, Ct);

        job.Status.Should().Be(AssetJobStatus.Failed);
        job.Error.Should().Contain(missing);

        var entry = (await _hotel.Publishing.GetHistoryAsync(target.Id, Ct))!.Single();

        entry.Error.Should().Contain(missing);
        entry.FinishedAt.Should().NotBeNull();
        (await _hotel.Publishing.GetTargetAsync(target.Id, Ct))!
            .LastPublish!.Id.Should()
            .Be(entry.Id);
    }

    [Fact]
    public async Task Testing_a_target_connects_and_lists_its_folder_or_says_why_not()
    {
        var target = await _hotel.AddFolderTargetAsync(Ct);
        var missing = await _hotel.AddFolderTargetAsync(
            Ct,
            Path.Combine(_hotel.TargetFolder, "not-there")
        );

        (await _hotel.Publishing.TestAsync(target.Id, Ct))!.Ok.Should().BeTrue();

        var refused = (await _hotel.Publishing.TestAsync(missing.Id, Ct))!;

        refused.Ok.Should().BeFalse();
        refused.Message.Should().Contain("not-there");
    }

    [Fact]
    public async Task A_password_is_kept_when_left_out_removed_when_emptied_and_never_shown()
    {
        var target = await _hotel.Publishing.CreateTargetAsync(Ftp("secret"), Ct);

        target.HasPassword.Should().BeTrue();

        (await _hotel.Publishing.UpdateTargetAsync(target.Id, Ftp(null), Ct))!
            .HasPassword.Should()
            .BeTrue();
        (await _hotel.Publishing.UpdateTargetAsync(target.Id, Ftp(""), Ct))!
            .HasPassword.Should()
            .BeFalse();
    }

    [Theory]
    [InlineData("", AssetPublishProtocol.Folder, "", "C:/web")]
    [InlineData(
        "A name far too long for a publish target to have, it keeps going on and on",
        AssetPublishProtocol.Folder,
        "",
        "C:/web"
    )]
    [InlineData("Live", AssetPublishProtocol.Ftp, "", "/web")]
    [InlineData("Live", AssetPublishProtocol.Folder, "", "relative/web")]
    [InlineData("Live", (AssetPublishProtocol)9, "host", "/web")]
    public async Task A_target_that_could_not_work_is_refused_with_why(
        string name,
        AssetPublishProtocol protocol,
        string host,
        string remotePath
    )
    {
        var create = () =>
            _hotel.Publishing.CreateTargetAsync(
                new AssetPublishTargetEdit
                {
                    Name = name,
                    Protocol = protocol,
                    Host = host,
                    RemotePath = remotePath,
                },
                Ct
            );

        (await create.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().NotBeEmpty();
        (await _hotel.Publishing.ListTargetsAsync(Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Forgetting_the_host_key_and_deleting_a_target_answer_false_for_one_that_is_not_there()
    {
        (await _hotel.Publishing.ForgetHostKeyAsync(404, Ct)).Should().BeFalse();
        (await _hotel.Publishing.DeleteTargetAsync(404, Ct)).Should().BeFalse();
        (await _hotel.Publishing.GetHistoryAsync(404, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task A_deleted_target_takes_its_records_and_history_with_it()
    {
        await _hotel.PutBundleAsync("chair", Bytes("chair", 300), Ct);
        var target = await _hotel.AddFolderTargetAsync(Ct);
        await _hotel.PublishAsync(target.Id, Ct);

        (await _hotel.Publishing.DeleteTargetAsync(target.Id, Ct)).Should().BeTrue();

        (await _hotel.Publishing.ListTargetsAsync(Ct)).Should().BeEmpty();
        (await _hotel.RecordedAsync(target.Id, Ct)).Should().BeEmpty();
    }

    [Fact]
    public void Remote_paths_join_under_the_target_folder_with_forward_slashes()
    {
        RemotePaths.Join("/var/www/", "bundled/pet").Should().Be("/var/www/bundled/pet");
        RemotePaths.Join("", "bundled/pet").Should().Be("bundled/pet");
        RemotePaths.Join("/", "bundled").Should().Be("/bundled");
        RemotePaths.Join("/var/www", "").Should().Be("/var/www");
        RemotePaths.Join("", "").Should().Be(".");
        RemotePaths
            .Ancestry("/var/www/bundled")
            .Should()
            .Equal("/var", "/var/www", "/var/www/bundled");
        RemotePaths.Ancestry("www/bundled").Should().Equal("www", "www/bundled");
    }

    private static AssetPublishTargetEdit Ftp(string? password) =>
        new()
        {
            Name = "Live",
            Protocol = AssetPublishProtocol.Ftp,
            Host = "ftp.example.com",
            User = "assets",
            Password = password,
            RemotePath = "/web",
        };

    /// <summary>A bundle's bytes: its name repeated to the size.</summary>
    private static byte[] Bytes(string name, int size)
    {
        var bytes = new byte[size];
        var source = Encoding.ASCII.GetBytes(name);

        for (var i = 0; i < size; i++)
            bytes[i] = source[i % source.Length];

        return bytes;
    }
}
