using FluentAssertions;
using Resonance.Core.Helpers;
using Xunit;

namespace Resonance.Core.Tests;

public class RootOverlapValidatorTests
{
    [Fact]
    public void Evaluate_WhenNoExistingRoots_ReturnsValid()
    {
        var result = RootOverlapValidator.Evaluate(@"C:\Music", []);

        result.Action.Should().Be(RootOverlapAction.Valid);
        result.ConflictingPaths.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_WhenDisjointDirectories_ReturnsValid()
    {
        var existing = new[] { @"C:\Music", @"D:\Audio" };
        var result = RootOverlapValidator.Evaluate(@"E:\Soundtracks", existing);

        result.Action.Should().Be(RootOverlapAction.Valid);
        result.ConflictingPaths.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_WhenCandidateIsSubdirectory_ReturnsReject()
    {
        var existing = new[] { @"C:\Music" };
        var result = RootOverlapValidator.Evaluate(@"C:\Music\Rock\Prog", existing);

        result.Action.Should().Be(RootOverlapAction.RejectSubfolderAlreadyCovered);
        result.ConflictingPaths.Should().ContainSingle().Which.Should().Be(@"C:\Music");
    }

    [Fact]
    public void Evaluate_WhenCandidateHasSlashDifferences_NormalizesAndRejects()
    {
        var existing = new[] { @"C:\Music" };
        var result = RootOverlapValidator.Evaluate("c:/music/classical/", existing);

        result.Action.Should().Be(RootOverlapAction.RejectSubfolderAlreadyCovered);
        result.ConflictingPaths.Should().ContainSingle().Which.Should().Be(@"C:\Music");
    }

    [Fact]
    public void Evaluate_WhenCandidateSharesPrefixButNotSubfolder_ReturnsValid()
    {
        var existing = new[] { @"C:\Music" };
        var result = RootOverlapValidator.Evaluate(@"C:\MusicExtra", existing);

        result.Action.Should().Be(RootOverlapAction.Valid);
        result.ConflictingPaths.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_WhenCandidateIsIdentical_ReturnsReject()
    {
        var existing = new[] { @"C:\Music" };
        var result = RootOverlapValidator.Evaluate("C:/music/", existing);

        result.Action.Should().Be(RootOverlapAction.RejectSubfolderAlreadyCovered);
        result.ConflictingPaths.Should().ContainSingle().Which.Should().Be(@"C:\Music");
    }

    [Fact]
    public void Evaluate_WhenCandidateIsParentOfExistingSubfolders_ReturnsConsolidateParent()
    {
        var existing = new[] { @"C:\Music\Rock", @"C:\Music\Jazz\Bebop", @"D:\Other" };
        var result = RootOverlapValidator.Evaluate(@"C:\Music", existing);

        result.Action.Should().Be(RootOverlapAction.ConsolidateParent);
        result.ConflictingPaths.Should().HaveCount(2);
        result.ConflictingPaths.Should().Contain(@"C:\Music\Rock");
        result.ConflictingPaths.Should().Contain(@"C:\Music\Jazz\Bebop");
    }

    [Fact]
    public void Evaluate_ComplexConsolidation_AbsorbsMultipleDescendantsAcrossBranches()
    {
        var existing = new[]
        {
            @"D:\Audio\Lossless\FLAC\Rock",
            @"D:\Audio\Lossless\ALAC\Jazz",
            @"D:\Audio\Lossless\MP3\Pop",
            @"D:\Audio\Soundtracks\OST",
            @"E:\ExternalDrive\Audio"
        };

        var result = RootOverlapValidator.Evaluate(@"D:\Audio\Lossless", existing);

        result.Action.Should().Be(RootOverlapAction.ConsolidateParent);
        result.ConflictingPaths.Should().HaveCount(3);
        result.ConflictingPaths.Should().Contain(@"D:\Audio\Lossless\FLAC\Rock");
        result.ConflictingPaths.Should().Contain(@"D:\Audio\Lossless\ALAC\Jazz");
        result.ConflictingPaths.Should().Contain(@"D:\Audio\Lossless\MP3\Pop");
        result.ConflictingPaths.Should().NotContain(@"D:\Audio\Soundtracks\OST");
        result.ConflictingPaths.Should().NotContain(@"E:\ExternalDrive\Audio");
    }

    [Fact]
    public void Evaluate_CaseInsensitiveMatching_CorrectlyRejectsSubfolder()
    {
        var existing = new[] { @"c:\music\collection" };
        var result = RootOverlapValidator.Evaluate(@"C:\MUSIC\COLLECTION\HEAVY_METAL", existing);

        result.Action.Should().Be(RootOverlapAction.RejectSubfolderAlreadyCovered);
        result.ConflictingPaths.Should().ContainSingle();
    }

    [Theory]
    [InlineData(@"C:\Music\Rock", @"C:\Music", true)]
    [InlineData(@"C:\Music\Rock\Classic", @"C:\Music", true)]
    [InlineData(@"C:\MusicExtra", @"C:\Music", false)]
    [InlineData(@"C:\Music", @"C:\Music", false)]
    [InlineData(@"D:\Music", @"C:\Music", false)]
    [InlineData(@"C:\Music\Rock", @"c:\music", true)]
    [InlineData(@"c:\music\rock", @"C:\MUSIC", true)]
    public void IsSubdirectoryOf_EvaluatesCorrectly(string child, string parent, bool expected)
    {
        RootOverlapValidator.IsSubdirectoryOf(child, parent).Should().Be(expected);
    }
}
