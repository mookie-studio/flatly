using Flatly.Api.Modules.Chores.Rotation;
using Xunit;

namespace Flatly.Api.Tests.Chores;

public class WeightedRotationTests
{
    private static readonly Guid Ana = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid Ben = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid Carla = Guid.Parse("00000000-0000-0000-0000-00000000000c");

    private static readonly DateTimeOffset Monday = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Thursday = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    public static TheoryData<string, RotationCandidate[], Guid?> Cases => new()
    {
        {
            "no candidates gives no assignee",
            [],
            null
        },
        {
            "equal weights and history: the member who held the chore least recently goes first",
            [
                new RotationCandidate(Ana, 5, Thursday),
                new RotationCandidate(Ben, 5, Monday),
            ],
            Ben
        },
        {
            "equal weights, one never held the chore: that member goes first",
            [
                new RotationCandidate(Ana, 5, Monday),
                new RotationCandidate(Ben, 5, null),
            ],
            Ben
        },
        {
            "uneven weights: the lowest accumulated weight wins even if it held the chore most recently",
            [
                new RotationCandidate(Ana, 8, Monday),
                new RotationCandidate(Ben, 3, Thursday),
                new RotationCandidate(Carla, 6, null),
            ],
            Ben
        },
        {
            "a new member joining with no weight and no history takes the next turn",
            [
                new RotationCandidate(Ana, 4, Monday),
                new RotationCandidate(Ben, 7, Monday),
                new RotationCandidate(Carla, 0, null),
            ],
            Carla
        },
        {
            "a member leaving is dropped from the candidates, so the remaining members decide",
            [
                new RotationCandidate(Carla, 9, Thursday),
            ],
            Carla
        },
        {
            "full tie on weight and history: the lowest member id is picked, deterministically",
            [
                new RotationCandidate(Carla, 5, Monday),
                new RotationCandidate(Ana, 5, Monday),
                new RotationCandidate(Ben, 5, Monday),
            ],
            Ana
        },
        {
            "two members who never held the chore and tie on weight: the lowest member id is picked",
            [
                new RotationCandidate(Ben, 0, null),
                new RotationCandidate(Ana, 0, null),
            ],
            Ana
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void NextAssigneeFollowsTheRotationRules(string scenario, RotationCandidate[] candidates, Guid? expected)
    {
        var actual = WeightedRotation.NextAssignee(candidates);

        Assert.True(expected == actual, $"Scenario: {scenario}. Expected {expected}, got {actual}.");
    }

    [Fact]
    public void NextAssigneeRejectsMissingCandidateList()
    {
        Assert.Throws<ArgumentNullException>(() => WeightedRotation.NextAssignee(null!));
    }
}
