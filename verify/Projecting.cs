// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Verify
{
    /// <summary>What a projection says about a state, written down and held to.</summary>
    /// <remarks>
    /// A host reads a rule set's projections and shows them, so a change to one is a change
    /// whoever uses the application sees, and for a rule set with no screen it can be the
    /// only one. <c>tally</c> and its two later versions are each one line apart on a
    /// projection and on nothing else; before projections were observed, both diffs came back
    /// empty.
    /// </remarks>
    public class Projecting(Xunit.Abstractions.ITestOutputHelper said)
    {
        // `best` is read by a projection and by nothing else. Counted as an observation, it
        // keeps apart the positions that answer `beatBest` differently: 77 states, every mark
        // at every count. Left out, the walk is 11 states compared on the count alone, and
        // all 11 were first reached before anything was marked — so the test design wrote a
        // fault for `beatBest` everywhere and the answer a mark gives nowhere.
        [Fact]
        public void AFieldOnlyAProjectionReadsDecidesWhichStateItIs()
        {
            string text = Fixture.RuleSet("tally");
            string unread = text.Replace("\"projections\"", "\"unread\"", StringComparison.Ordinal);
            WalkSettings settings = new(States: 3000);

            TestDesign design = TestDesign.Derive(Fixture.Runtime, text, null, settings, ObservationScan.Of(text));
            TestDesign blind = TestDesign.Derive(Fixture.Runtime, text, null, settings, ObservationScan.Of(unread));

            said.WriteLine($"tally: compared on {string.Join(", ", design.Observed)}, {design.States.Count} states, "
                + $"beatBest answered in {Answered(design)}; with projections not counted, compared on "
                + $"{string.Join(", ", blind.Observed)}, {blind.States.Count} states, beatBest answered in {Answered(blind)}");

            Assert.Equal(["counted", "best"], design.Observed);
            Assert.Equal(77, design.States.Count);
            Assert.Equal(66, Answered(design));

            Assert.Equal(["counted"], blind.Observed);
            Assert.Equal(11, blind.States.Count);
            Assert.Equal(0, Answered(blind));
        }

        // A projection moves nothing, so a fault in one stops nothing: the walk goes on, and
        // the fault is written where it happened, in the runtime's words.
        [Fact]
        public void AProjectionThatFaultsIsWrittenAndTheWalkGoesOn()
        {
            TestDesign design = Fixture.Walk("tally", 3000);
            Projection[] faults =
            [
                .. design.States.SelectMany(state => state.Projections).Where(projection => projection.Fault is not null),
            ];

            said.WriteLine($"tally at 3000 states: {design.States.Count} states, {faults.Length} of them with "
                + $"{string.Join('/', faults.Select(fault => fault.Name).Distinct())} faulted — "
                + $"\"{faults[0].Fault}\"");

            Assert.Equal(77, design.States.Count);
            Assert.Equal(0, design.Unreached);
            Assert.Equal(11, faults.Length);
            Assert.All(faults, fault => Assert.Equal("beatBest", fault.Name));
            Assert.All(design.States, state => Assert.Equal(2, state.Projections.Count));
        }

        // The change the request this answers was about: ten became three. Every state in
        // both, and the 56 whose count is three to nine say `atLeastTen` is true where it said
        // false. No input, landing or ending moved, and a diff says that and nothing else.
        [Fact]
        public void MovingWhatAProjectionSaysIsDecidedDifferently()
        {
            TestDesignDiff diff = Fixture.Apply(Fixture.Walk("tally", 3000), "tally-three");

            said.WriteLine($"tally -> tally-three: {diff.Common} states in both, {diff.Changed.Count} decided "
                + $"differently, {diff.Gone.Count} gone, {diff.Appeared.Count} new; the first: {diff.Changed[0]}");

            Assert.Equal(77, diff.Common);
            Assert.Empty(diff.Gone);
            Assert.Empty(diff.Appeared);
            Assert.Equal(56, diff.Changed.Count);
            Assert.All(diff.Changed, change =>
            {
                Assert.Empty(change.Lost);
                Assert.Empty(change.Gained);
                Assert.Empty(change.Moved);
                Assert.False(change.EndingMoved);

                (Projection was, Projection now) = Assert.Single(change.ProjectionsMoved);
                Assert.Equal("atLeastTen", was.Name);
                Assert.Equal("false", was.Value);
                Assert.Equal("true", now.Value);
            });
        }

        // A host reads a projection by name, so a rename is one field gone and another come,
        // whatever either says. It is said once, as the names, and not once per state.
        [Fact]
        public void RenamingAProjectionIsSaidOnce()
        {
            TestDesignDiff diff = Fixture.Apply(Fixture.Walk("tally", 3000), "tally-renamed");

            said.WriteLine($"tally -> tally-renamed: {diff.Common} states in both, {diff.Changed.Count} decided "
                + $"differently; gone {string.Join(", ", diff.ProjectionsGone)}, new "
                + $"{string.Join(", ", diff.ProjectionsAppeared)}");

            Assert.Equal(77, diff.Common);
            Assert.Empty(diff.Changed);
            Assert.Equal(["atLeastTen"], diff.ProjectionsGone);
            Assert.Equal(["atLeastThree"], diff.ProjectionsAppeared);
            Assert.False(diff.IsEmpty);
        }

        private static int Answered(TestDesign design) =>
            design.States.Count(state => state.Projections.Any(projection => projection.Name == "beatBest" && projection.Fault is null));
    }
}
