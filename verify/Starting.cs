// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Verify
{
    /// <summary>A rule set whose initial state is the shape of a case is walked from a case.</summary>
    /// <remarks>
    /// <para>
    /// `signed-request` and the two rule sets it holds are copied from Rulealize's published
    /// rule sets, and `verify/state/signed-request.json` is the example case published beside
    /// them. Each component leaves who may sign and what may be requested empty in its
    /// initial state, on purpose, and says why: a composite cannot write its component's
    /// initial state, so anything written there would be forced on every holder. That is what
    /// these are about. Walked from the rule set, there is nothing to design; walked from the
    /// case, there is the process.
    /// </para>
    /// <para>
    /// `lamp` was written for the second measurement. Nothing else here comes back to where it
    /// began, and coming back is the one place a start written by hand can show whether it
    /// was compared as the state the runtime writes.
    /// </para>
    /// </remarks>
    public class Starting(Xunit.Abstractions.ITestOutputHelper said)
    {
        private static readonly Dictionary<string, string> SignedRequest = new(StringComparer.Ordinal)
        {
            ["Rulealize.RuleSet.Request"] = Fixture.RuleSet("Rulealize.RuleSet.Request"),
            ["Rulealize.RuleSet.Signatures"] = Fixture.RuleSet("Rulealize.RuleSet.Signatures"),
        };

        private static readonly Dictionary<string, string> Lamp = new(StringComparer.Ordinal)
        {
            ["switch"] = Fixture.RuleSet("switch"),
        };

        [Fact]
        public void ARuleSetThatOffersNothingUntilACaseSaysSoIsWalkedFromTheCase()
        {
            TestDesign bare = TestDesign.Derive(Fixture.Runtime, Fixture.RuleSet("signed-request"), SignedRequest);
            TestDesign walked = TestDesign.Derive(
                Fixture.Runtime, Fixture.RuleSet("signed-request"), SignedRequest, Fixture.State("signed-request"), null, null);

            said.WriteLine($"signed-request from its initial state: {bare.States.Count} state, "
                + $"{bare.States[0].Moves.Count} moves");
            said.WriteLine($"signed-request from the published case: {walked.States.Count} states, "
                + $"{Fixture.Rejoins(walked)} rejoins, {walked.Unreached} unreached, "
                + $"endings {string.Join('/', Fixture.Results(walked))}");

            Assert.Single(bare.States);
            Assert.Empty(bare.States[0].Moves);
            Assert.Null(bare.Start);

            Assert.Equal(35, walked.States.Count);
            Assert.Equal(6, Fixture.Rejoins(walked));
            Assert.Equal(0, walked.Unreached);
            Assert.Equal(["denied", "granted"], Fixture.Results(walked));
            Assert.NotNull(walked.Start);
        }

        // The runtime names the rule set a component's state belongs to; the start written by
        // hand does not, and Rulealize reads it as the same state. Compared by its shape, the
        // walk would not have known it either: `sw.off` from #1 lands where the walk began and
        // would have been a third state instead of a return to #0.
        [Fact]
        public void AStartWrittenByHandIsTheStateTheWalkComesBackTo()
        {
            TestDesign bare = TestDesign.Derive(Fixture.Runtime, Fixture.RuleSet("lamp"), Lamp);
            TestDesign walked = TestDesign.Derive(
                Fixture.Runtime, Fixture.RuleSet("lamp"), Lamp, Fixture.State("lamp"), null, null);

            said.WriteLine($"lamp from its initial state: {bare.States.Count} states, {Fixture.Rejoins(bare)} rejoins");
            said.WriteLine($"lamp from a start written by hand: {walked.States.Count} states, {Fixture.Rejoins(walked)} rejoins");

            Assert.Equal(2, bare.States.Count);
            Assert.Equal(2, walked.States.Count);
            Assert.Equal(1, Fixture.Rejoins(walked));
            Assert.Equal("#0", walked.States[1].Moves.Single().Landings.Single().To);
        }
    }
}
