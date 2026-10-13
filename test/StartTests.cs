// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Rulealize;

namespace Ruledger.Tests
{
    /// <summary>A walk begun in a state of a case rather than where the rule set begins.</summary>
    /// <remarks>
    /// What that comes out as on a rule set whose initial state offers nothing is in
    /// <c>verify/Starting.cs</c>. These ask the smaller questions: that the start travels in
    /// the test design and comes back out of it, that a diff walks the new version from it,
    /// and that a start which is not a state of the rule set is refused.
    /// </remarks>
    public class StartTests
    {
        private static readonly Dictionary<string, string> Switch = new(StringComparer.Ordinal)
        {
            ["switch"] = Vocabulary.Read("switch"),
        };

        [Fact]
        public void TheStartIsWrittenIntoTheDesignAndReadBackOutOfIt()
        {
            TestDesign design = Derive(Vocabulary.State("lamp"));
            string written = design.ToJson();
            JsonElement root = JsonDocument.Parse(written).RootElement;

            Assert.Equal("ruledger/test-design/v4", root.GetProperty("$schema").GetString());
            Assert.Equal("lamp@1.0.0", root.GetProperty("start").GetProperty("ruleSet").GetString());
            Assert.False(root.GetProperty("start").GetProperty("data").GetProperty("sw").GetProperty("data").GetProperty("on").GetBoolean());

            TestDesign read = TestDesign.FromJson(written);
            Assert.NotNull(read.Start);
            Assert.Equal(written, read.ToJson());
        }

        // Left out rather than written as the initial state, so a design of the rule set's own
        // beginning says what it always said, and one without a start is one that began there.
        [Fact]
        public void AWalkFromTheRuleSetsOwnBeginningWritesNoStart()
        {
            TestDesign design = TestDesign.Derive(Vocabulary.Runtime, Vocabulary.Read("lamp"), Switch);

            Assert.Null(design.Start);
            Assert.False(JsonDocument.Parse(design.ToJson()).RootElement.TryGetProperty("start", out _));
        }

        // Every v2 document began where the rule set begins, so it is read as a v3 one that did.
        [Fact]
        public void AV2DesignIsReadAsOneThatBeganWhereTheRuleSetBegins()
        {
            string v2 = TestDesign.Derive(Vocabulary.Runtime, Vocabulary.Read("approval"), new WalkSettings(States: 300))
                .ToJson()
                .Replace("ruledger/test-design/v4", "ruledger/test-design/v2", StringComparison.Ordinal);

            TestDesign read = TestDesign.FromJson(v2);

            Assert.Null(read.Start);
            Assert.Equal(4, read.States.Count);
        }

        // A diff walks the new version from where the design began; anything else would be
        // two cases held against each other and reported as the rules having changed.
        [Fact]
        public void ApplyingADesignWalksTheNewVersionFromItsStart()
        {
            TestDesign design = Derive(Vocabulary.State("lamp"));

            TestDesignDiff diff = TestDesignDiff.Of(Vocabulary.Runtime, design.ToJson(), Vocabulary.Read("lamp"), Switch);

            Assert.True(diff.IsEmpty);
            Assert.NotNull(diff.After.Start);
            Assert.Equal(Canonical(diff.Before.Start!), Canonical(diff.After.Start!));
        }

        // A state is named by how the walk arrived from #0, and #0 is wherever it began, so two
        // cases share every name and none of the places. Applying a design cannot land here —
        // the new walk begins where the design did — and holding two designs against each
        // other can.
        [Fact]
        public void TwoDesignsBegunInDifferentStatesAreNotCompared()
        {
            TestDesign off = Derive(Vocabulary.State("lamp"));
            TestDesign on = Derive("""{ "$schema": "rulealize/state/v1", "data": { "sw": { "data": { "on": true } } } }""");

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
                () => TestDesignDiff.Between(off, on));

            Assert.Contains("began in different states", refused.Message, StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(
                () => TestDesignDiff.Between(TestDesign.Derive(Vocabulary.Runtime, Vocabulary.Read("lamp"), Switch), off));
        }

        // The same case written another way is the same start: compared as what it says.
        [Fact]
        public void TheSameStartWrittenAnotherWayIsTheSameStart()
        {
            TestDesign one = Derive(Vocabulary.State("lamp"));
            TestDesign other = Derive("""{"data":{"sw":{"data":{"on":false}}},"ruleSet":"lamp@1.0.0","$schema":"rulealize/state/v1"}""");

            Assert.True(TestDesignDiff.Between(one, other).IsEmpty);
        }

        // Handed a start and choices together, the walk carries each choice it can and reports
        // each it cannot, as it does when the rule set changes. Nothing is refused wholesale.
        [Fact]
        public void ChoicesHandedInWithAStartAreCarriedOneByOne()
        {
            TestDesign design = TestDesign.Derive(
                Vocabulary.Runtime,
                Vocabulary.Read("lamp"),
                Switch,
                """{ "$schema": "rulealize/state/v1", "data": { "sw": { "data": { "on": true } } } }""",
                null,
                [
                    new TestDesignEdit("#0", "sw.off", new Dictionary<string, string>()),
                    new TestDesignEdit("#0", "sw.on", new Dictionary<string, string>()),
                ]);

            Assert.Equal([EditOutcome.Carried, EditOutcome.NotLegal], design.Edits.Select(static edit => edit.Outcome));
        }

        [Fact]
        public void AStartThatIsNotAStateOfTheRuleSetIsRefused()
        {
            Assert.Throws<RuleDocumentException>(
                () => Derive("""{ "$schema": "rulealize/state/v1", "data": { "sw": { "data": { "on": "yes" } } } }"""));
            Assert.Throws<RuleDocumentException>(() => Derive(Vocabulary.State("signed-request")));
        }

        private static TestDesign Derive(string start) =>
            TestDesign.Derive(Vocabulary.Runtime, Vocabulary.Read("lamp"), Switch, start, null, null);

        private static string Canonical(string document) =>
            JsonSerializer.Serialize(JsonDocument.Parse(document).RootElement);
    }
}
