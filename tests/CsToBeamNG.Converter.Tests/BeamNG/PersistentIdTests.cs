using System;
using CsToBeamNG.Converter.BeamNG;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class PersistentIdTests
{
    private static readonly Guid _dnsNamespace = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
    private static readonly Guid _urlNamespace = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

    // Expected values from Python's uuid.uuid5, an independent implementation of RFC 9562.
    [Theory]
    [InlineData("python.org", "886313e1-3b8a-5372-9b90-0c9aee199e5d")]
    [InlineData("", "4ebd0208-8328-5d69-8c44-ec50939c0967")]
    [InlineData("São Paulo", "c493334d-b283-5d5e-9a7f-ddfba9e19074")]
    public void MatchesTheReferenceImplementation(string name, string expected) =>
        Assert.Equal(expected, PersistentId.Format(PersistentId.CreateVersion5(_dnsNamespace, name)));

    [Fact]
    public void MatchesTheReferenceImplementationInAnotherNamespace() =>
        Assert.Equal(
            "dd2c1780-811a-5296-81c5-178a0ef488bc",
            PersistentId.Format(PersistentId.CreateVersion5(_urlNamespace, "https://example.com/")));

    [Fact]
    public void DerivesObjectIdsFromTheLevelAndTheKey()
    {
        // uuid.uuid5(UUID('7f355653-9e13-40ad-9d43-4394bb6f9725'), 'test_level:box') in Python.
        Assert.Equal("3aba5b9f-cb36-5f97-b2ca-8861f92576b2", PersistentId.ForObject("test_level", "box"));
        Assert.NotEqual(PersistentId.ForObject("test_level", "box"), PersistentId.ForObject("other_level", "box"));
        Assert.NotEqual(PersistentId.ForObject("test_level", "box"), PersistentId.ForObject("test_level", "Box"));
    }

    [Fact]
    public void SetsTheVersionAndVariantBits()
    {
        string id = PersistentId.ForObject("test_level", "anything");

        Assert.Equal('5', id[14]);
        Assert.Contains(id[19], "89ab");
        Assert.Equal(id.ToLowerInvariant(), id);
    }

    [Fact]
    public void DoesntDependOnTheCulture()
    {
        string expected = PersistentId.ForObject("test_level", "key:İ");
        using (new CultureScope("tr-TR"))
        {
            Assert.Equal(expected, PersistentId.ForObject("test_level", "key:İ"));
        }
    }

    [Fact]
    public void RejectsInvalidLevelNames() => Assert.Throws<ArgumentException>(() => PersistentId.ForObject("Bad Name", "key"));
}
