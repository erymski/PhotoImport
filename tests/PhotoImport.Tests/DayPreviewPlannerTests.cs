using PhotoImport.Models;
using PhotoImport.Services;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public sealed class DayPreviewPlannerTests
{
    [Fact]
    public void CompletedDaySelectsOnlyLaterCalendarDates()
    {
        var inventory = PreviewFixture.Inventory(
            PreviewFixture.Photo("\\old.jpg", 10),
            PreviewFixture.Photo("\\equal.jpg", 20),
            PreviewFixture.Photo("\\new.jpg", 30)
        );
        var capture = PreviewFixture.Report(
            PreviewFixture.Capture("\\old.jpg", "2026:10:09 23:59:59", 10),
            PreviewFixture.Capture("\\equal.jpg", "2026:10:10 00:00:00", 20),
            PreviewFixture.Capture("\\new.jpg", "2026:10:11 00:00:00", 30)
        );
        var plan = DayPreviewPlanner.Build(
            inventory,
            [capture],
            PreviewFixture.Checkpoint(),
            default
        );
        Assert.True(plan.IsComplete);
        Assert.Equal(
            new[] { "older", "alreadyImported", "eligible" },
            plan.Rows.Select(r => r.Decision)
        );
        Assert.Equal(
            "\\new.jpg",
            Assert.Single(plan.Rows, r => r.Decision == "eligible").SourcePath
        );
        Assert.Equal(1, plan.SelectedCount);
        Assert.Equal(30m, plan.SelectedKnownBytes);
    }

    [Fact]
    public void TrackedBoundarySelectsNewSameDayFilesAndDoesNotConsultPcCopies()
    {
        var known = PreviewFixture.Photo("\\known.jpg");
        var fresh = PreviewFixture.Photo("\\fresh.jpg");
        var key = BoundaryKey.Create("phone", known, new(2026, 10, 10));
        var plan = DayPreviewPlanner.Build(
            PreviewFixture.Inventory(known, fresh),
            [
                PreviewFixture.Report(
                    PreviewFixture.Capture("\\known.jpg", "2026:10:10 10:00:00"),
                    PreviewFixture.Capture("\\fresh.jpg", "2026:10:10 11:00:00")
                ),
            ],
            PreviewFixture.Checkpoint("tracked", key),
            default
        );
        Assert.Equal(new[] { "alreadyImported", "eligible" }, plan.Rows.Select(r => r.Decision));
        Assert.True(plan.IsComplete);
    }

    [Fact]
    public void EmptyTrackedBoundaryIncludesTheWholeDayAndFingerprintDependsOnSource()
    {
        var photo = PreviewFixture.Photo();
        var plan = DayPreviewPlanner.Build(
            PreviewFixture.Inventory(photo),
            [PreviewFixture.Report(PreviewFixture.Capture(original: "2026:10:10 12:00:00"))],
            PreviewFixture.Checkpoint("tracked"),
            default
        );
        Assert.Equal("eligible", Assert.Single(plan.Rows).Decision);
        var key = BoundaryKey.Create("phone", photo, new(2026, 10, 10));
        Assert.Equal(64, key.Length);
        Assert.Equal(key, BoundaryKey.Create("phone", photo, new(2026, 10, 10)));
        Assert.NotEqual(
            key,
            BoundaryKey.Create("phone", photo with { SizeBytes = 101 }, new(2026, 10, 10))
        );
        Assert.NotEqual(
            key,
            BoundaryKey.Create(
                "phone",
                photo with
                {
                    SourcePath = "\\renamed.jpg",
                },
                new(2026, 10, 10)
            )
        );
        Assert.NotEqual(key, BoundaryKey.Create("other", photo, new(2026, 10, 10)));
    }

    [Theory]
    [InlineData("wrongDevice")]
    [InlineData("unknownSchema")]
    [InlineData("checkpointMode")]
    [InlineData("closedWithKeys")]
    public void InvalidWholeInputContractsAreRejected(string problem)
    {
        var capture = PreviewFixture.Report(PreviewFixture.Capture());
        var checkpoint = PreviewFixture.Checkpoint();
        if (problem == "wrongDevice")
            capture = capture with { Device = FakePhotoDevice.Info("other") };
        if (problem == "unknownSchema")
            capture = capture with { SchemaVersion = 99 };
        if (problem == "checkpointMode")
            checkpoint = checkpoint with { BoundaryMode = "guess" };
        if (problem == "closedWithKeys")
            checkpoint = checkpoint with { BoundaryKeys = [new string('A', 64)] };
        Assert.Throws<PreviewInputException>(() =>
            DayPreviewPlanner.Build(
                PreviewFixture.Inventory(PreviewFixture.Photo()),
                [capture],
                checkpoint,
                default
            )
        );
    }

    [Theory]
    [InlineData("size")]
    [InlineData("missingProvenance")]
    [InlineData("legacy")]
    [InlineData("persistentId")]
    [InlineData("inconsistentDay")]
    [InlineData("missingDate")]
    [InlineData("incompleteObservation")]
    public void BadPerObjectEvidenceStaysUnresolved(string problem)
    {
        var file = PreviewFixture.Capture();
        var report = PreviewFixture.Report(file);
        if (problem == "size")
            file = file with { Source = file.Source! with { SizeBytes = 101 } };
        if (problem == "missingProvenance")
            file = file with { Source = null };
        if (problem == "persistentId")
            file = file with { Source = file.Source! with { PersistentObjectId = "changed" } };
        if (problem == "inconsistentDay")
            file = file with
            {
                Observation = file.Observation with { CaptureDay = new(2026, 10, 12) },
            };
        if (problem == "missingDate")
            file = file with
            {
                Observation = file.Observation with
                {
                    CaptureDay = null,
                    CaptureDayStatus = "missing",
                },
            };
        if (problem == "incompleteObservation")
            file = file with { Observation = file.Observation with { Outcome = "incomplete" } };
        report = report with { Files = [file], SchemaVersion = problem == "legacy" ? null : 2 };
        var plan = DayPreviewPlanner.Build(
            PreviewFixture.Inventory(PreviewFixture.Photo()),
            [report],
            PreviewFixture.Checkpoint(),
            default
        );
        Assert.False(plan.IsComplete);
        Assert.Equal("unresolved", Assert.Single(plan.Rows).Decision);
        Assert.NotEmpty(plan.Rows[0].Reason);
    }

    [Fact]
    public void UnsupportedPngAndUnknownSizesAreNotDropped()
    {
        var plan = DayPreviewPlanner.Build(
            PreviewFixture.Inventory(
                PreviewFixture.Photo("\\a.png"),
                PreviewFixture.Photo("\\unknown.jpg", null)
            ),
            [PreviewFixture.Report()],
            PreviewFixture.Checkpoint(),
            default
        );
        Assert.Equal(2, plan.Rows.Count);
        Assert.Equal(2, plan.UnresolvedCount);
        Assert.Equal(1, plan.UnknownSizeCount);
        Assert.False(plan.IsComplete);
    }

    [Fact]
    public void DuplicateInventoryAndConflictingCapturesAreNotSilentlyChosen()
    {
        var file = PreviewFixture.Capture();
        var other = PreviewFixture.Capture(original: "2026:10:12 12:00:00");
        var plan = DayPreviewPlanner.Build(
            PreviewFixture.Inventory(PreviewFixture.Photo()),
            [PreviewFixture.Report(file, other)],
            PreviewFixture.Checkpoint(),
            default
        );
        Assert.Equal("unresolved", Assert.Single(plan.Rows).Decision);
        plan = DayPreviewPlanner.Build(
            PreviewFixture.Inventory(PreviewFixture.Photo(), PreviewFixture.Photo()),
            [PreviewFixture.Report(file)],
            PreviewFixture.Checkpoint(),
            default
        );
        Assert.Equal(2, plan.UnresolvedCount);
    }

    [Fact]
    public void IdenticalCapturesCoalesceButOrphansAndPartialInputsRemainVisible()
    {
        var file = PreviewFixture.Capture();
        var inventory = PreviewFixture.Inventory(PreviewFixture.Photo());
        Assert.True(
            DayPreviewPlanner
                .Build(
                    inventory,
                    [PreviewFixture.Report(file, file)],
                    PreviewFixture.Checkpoint(),
                    default
                )
                .IsComplete
        );
        var orphan = PreviewFixture.Capture("\\orphan.jpg");
        var plan = DayPreviewPlanner.Build(
            inventory,
            [PreviewFixture.Report(file, orphan)],
            PreviewFixture.Checkpoint(),
            default
        );
        Assert.False(plan.IsComplete);
        Assert.NotEmpty(plan.InputProblems);
        Assert.Single(plan.Rows);
        plan = DayPreviewPlanner.Build(
            inventory with
            {
                CompletionStatus = "incomplete",
            },
            [PreviewFixture.Report(file)],
            PreviewFixture.Checkpoint(),
            default
        );
        Assert.False(plan.IsComplete);
        Assert.Equal(1, plan.SelectedCount);
    }

    [Fact]
    public void PreCancellationDoesNotInventRows()
    {
        var plan = DayPreviewPlanner.Build(
            PreviewFixture.Inventory(PreviewFixture.Photo()),
            [PreviewFixture.Report(PreviewFixture.Capture())],
            PreviewFixture.Checkpoint(),
            new CancellationToken(true)
        );
        Assert.Equal("cancelled", plan.CompletionStatus);
        Assert.Empty(plan.Rows);
        Assert.Equal(1, plan.TotalPhotoCount);
    }

    [Fact]
    public void CancellationBetweenRowsRetainsOnlyProcessedResults()
    {
        using var cts = new CancellationTokenSource();
        var first = PreviewFixture.Capture();
        first = first with
        {
            Observation = first.Observation with
            {
                CaptureDirectories = new CancelOnEnumeration(
                    first.Observation.CaptureDirectories,
                    cts.Cancel
                ),
            },
        };
        var plan = DayPreviewPlanner.Build(
            PreviewFixture.Inventory(PreviewFixture.Photo(), PreviewFixture.Photo("\\b.jpg")),
            [PreviewFixture.Report(first, PreviewFixture.Capture("\\b.jpg"))],
            PreviewFixture.Checkpoint(),
            cts.Token
        );
        Assert.Equal("cancelled", plan.CompletionStatus);
        Assert.Equal(1, plan.ProcessedCount);
        Assert.Equal(2, plan.TotalPhotoCount);
        Assert.Equal("\\a.jpg", Assert.Single(plan.Rows).SourcePath);
    }

    private sealed class CancelOnEnumeration(
        IReadOnlyList<ExifCaptureDirectory> values,
        Action cancel
    ) : IReadOnlyList<ExifCaptureDirectory>
    {
        public int Count => values.Count;
        public ExifCaptureDirectory this[int index] => values[index];

        public IEnumerator<ExifCaptureDirectory> GetEnumerator()
        {
            foreach (var item in values)
                yield return item;
            cancel();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
