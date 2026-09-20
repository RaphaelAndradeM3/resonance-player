using FluentAssertions;
using Resonance.Core.Helpers;
using Xunit;

namespace Resonance.Core.Tests;

public class PathCanonicalizerTests
{
    [Theory]
    [InlineData(@"C:\", @"C:\")]
    [InlineData("C:/", @"C:\")]
    [InlineData(@"c:\", @"C:\")]
    public void Normalize_PreservesDriveRootSemantics(string input, string expected)
    {
        PathCanonicalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\Music\", @"C:\Music")]
    [InlineData(@"\\server\share\", @"\\server\share")]
    public void Normalize_StillTrimsNonDriveRootSeparators(string input, string expected)
    {
        PathCanonicalizer.Normalize(input).Should().Be(expected);
    }
}
