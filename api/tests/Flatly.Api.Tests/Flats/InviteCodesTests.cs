using Flatly.Api.Modules.Flats.Invites;
using Xunit;

namespace Flatly.Api.Tests.Flats;

public class InviteCodesTests
{
    [Fact]
    public void AGeneratedCodeHasEightCharactersFromTheUnambiguousAlphabet()
    {
        var code = InviteCodes.Generate();

        Assert.Equal(InviteCodes.Length, code.Length);
        Assert.All(code, character => Assert.Contains(character, InviteCodes.Alphabet));
    }

    [Fact]
    public void TheAlphabetLeavesOutTheLookAlikeCharacters()
    {
        Assert.DoesNotContain('0', InviteCodes.Alphabet);
        Assert.DoesNotContain('O', InviteCodes.Alphabet);
        Assert.DoesNotContain('1', InviteCodes.Alphabet);
        Assert.DoesNotContain('I', InviteCodes.Alphabet);
    }

    [Fact]
    public void GeneratedCodesDoNotRepeatInALargeSample()
    {
        var codes = Enumerable.Range(0, 5000).Select(_ => InviteCodes.Generate()).ToHashSet();

        Assert.Equal(5000, codes.Count);
    }

    [Fact]
    public void CodesLastSevenDays()
    {
        Assert.Equal(TimeSpan.FromDays(7), InviteCodes.Lifetime);
    }

    [Theory]
    [InlineData("  ab3dk9xq ", "AB3DK9XQ")]
    [InlineData("AB3DK9XQ", "AB3DK9XQ")]
    public void NormalizeTrimsAndUppercasesTheCode(string typed, string expected)
    {
        Assert.Equal(expected, InviteCodes.Normalize(typed));
    }
}
