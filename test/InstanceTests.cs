// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Ruledger.Tests
{
    /// <summary>A walk over a rule set that keeps instances of another as documents of their own.</summary>
    /// <remarks>
    /// A month holds any number of claims, each its own document in the store the month names,
    /// and the ledger holds a month at every key a schema admits. The position is every
    /// document, so that is what a state carries; a move is asked of each document in turn and
    /// says which it is at; and a key nobody could list is a key the walk waits for.
    /// </remarks>
    public class InstanceTests
    {
        private static readonly Dictionary<string, string> Held = new(StringComparer.Ordinal)
        {
            ["claim"] = Vocabulary.Read("claim"),
            ["claim-month"] = Vocabulary.Read("claim-month"),
        };

        private static readonly Dictionary<string, string> Starts = new(StringComparer.Ordinal)
        {
            ["claim-start"] = Vocabulary.State("claim-start"),
        };

        [Fact]
        public void AMonthBeginsAsItsOwnDocumentAlone()
        {
            TestDesignState opening = Month().States[0];

            KeyValuePair<string, string> only = Assert.Single(opening.Documents);
            Assert.Equal("/", only.Key);
            Assert.Equal(opening.State, only.Value);
            Assert.Equal(["create"], opening.Moves.Select(static move => move.Text));
        }

        [Fact]
        public void ACreatedClaimIsADocumentOfThePosition()
        {
            TestDesign design = Month();
            TestDesignState created = design.States.Single(static state => state.By == "create" && state.From == "#0");

            Assert.Equal(["/", "claims/1"], created.Documents.Select(static document => document.Key));
            Assert.Equal("bo", Data(created.Documents[1].Value).GetProperty("approver").GetString());
        }

        [Fact]
        public void AMoveAtAClaimSaysWhichClaim()
        {
            TestDesign design = Month();
            TestDesignState created = design.States.Single(static state => state.By == "create" && state.From == "#0");

            Move entered = created.Moves.Single(static move => move.Text == "claims.enterAmount@1(amount: 2)");
            Assert.Equal([KeyValuePair.Create("claims", (string?)"1")], entered.At);
            Assert.Contains("\"at\"", JsonDocument.Parse(entered.Document).RootElement.GetRawText(), StringComparison.Ordinal);
        }

        [Fact]
        public void ATransitionThatWritesTheMonthAndAClaimIsFollowed()
        {
            // Depth first, the walk goes on creating claims before it comes back to submit one,
            // so a person chooses to submit the claim it begins with first.
            string claim = Vocabulary.State("claim-start").Replace("\"amount\": 0", "\"amount\": 3", StringComparison.Ordinal);
            TestDesign design = TestDesign.Derive(
                FixtureStore.Runtime(),
                Vocabulary.Read("claim-month"),
                Held,
                Starts,
                $$"""[ { "at": "claims/1", "state": {{claim}} } ]""",
                new WalkSettings(States: 30),
                [new TestDesignEdit("#0", "submit", [KeyValuePair.Create("claims", "1")])]);

            Assert.Equal(EditOutcome.Carried, Assert.Single(design.Edits).Outcome);
            TestDesignState submitted = design.States[1];
            Assert.Equal("submit@1", submitted.By);
            JsonElement written = Data(submitted.Documents[1].Value);
            Assert.Equal("review", written.GetProperty("stage").GetString());
            Assert.Equal(written.GetProperty("amount").GetInt32(), Data(submitted.State).GetProperty("used").GetInt32());
        }

        // The month answers for itself whether its use is what its claims say, and the walk
        // writes that answer down wherever it goes: here, everywhere it went, it is.
        [Fact]
        public void TheMonthSaysEverywhereThatItsUseIsWhatItsClaimsSay()
        {
            TestDesign design = Month();

            Assert.All(design.States, static state =>
                Assert.Equal("true", state.Projections.Single(static each => each.Name == "consistent").Value));
        }

        [Fact]
        public void TheSamePositionReachedTwoWaysIsOneState()
        {
            TestDesign design = Month();
            HashSet<string> positions = new(StringComparer.Ordinal);

            Assert.All(design.States, state => Assert.True(positions.Add(string.Join('|', state.Documents.Select(static d => d.Key + "=" + Compact(d.Value))))));
        }

        [Fact]
        public void APositionOfSeveralDocumentsIsWrittenAndReadBack()
        {
            TestDesign design = Month();
            string written = design.ToJson();

            JsonElement root = JsonDocument.Parse(written).RootElement;
            Assert.Equal("ruledger/test-design/v5", root.GetProperty("$schema").GetString());
            Assert.False(root.GetProperty("states")[1].TryGetProperty("state", out _));
            Assert.Equal("claims/1", root.GetProperty("states")[1].GetProperty("documents")[1].GetProperty("at").GetString());

            Assert.Equal(written, TestDesign.FromJson(written).ToJson());
        }

        [Fact]
        public void AMonthDiffedAgainstItselfIsEmpty()
        {
            TestDesign design = Month();

            TestDesignDiff diff = TestDesignDiff.Of(
                FixtureStore.Runtime(), design.ToJson(), Vocabulary.Read("claim-month"), Held, Starts);

            Assert.True(diff.IsEmpty, diff.ToString());
        }

        [Fact]
        public void AWalkBeginsInAPositionOfSeveralDocuments()
        {
            string claim = Vocabulary.State("claim-start").Replace("\"amount\": 0", "\"amount\": 3", StringComparison.Ordinal);
            string start = $$"""[ { "at": "claims/1", "state": {{claim}} } ]""";

            TestDesign design = TestDesign.Derive(
                FixtureStore.Runtime(), Vocabulary.Read("claim-month"), Held, Starts, start, new WalkSettings(States: 30), null);

            Assert.Equal(["/", "claims/1"], design.States[0].Documents.Select(static document => document.Key));
            Assert.Contains("submit@1", design.States[0].Moves.Select(static move => move.Text));
        }

        // Any seven characters is a month, so there is no list of months to walk: the walk says
        // it is waiting for one, and that is not a position with nothing on offer.
        [Fact]
        public void ALedgerWhoseKeysCannotBeListedWaitsForOne()
        {
            TestDesign design = Ledger();

            TestDesignState opening = Assert.Single(design.States);
            Move waiting = Assert.Single(opening.Moves);
            Assert.Equal("months@?", waiting.Text);
            Assert.Equal(["at.months"], waiting.Open);
            Assert.Empty(waiting.Landings);
            Assert.False(opening.IsTerminal);

            KeyAdmission keys = Assert.Single(design.Keys);
            Assert.Equal("months", keys.Collection);
            Assert.Contains("\"minLength\":7", keys.Schema, StringComparison.Ordinal);

            JsonElement move = JsonDocument.Parse(design.ToJson()).RootElement.GetProperty("states")[0].GetProperty("moves")[0];
            Assert.False(move.TryGetProperty("input", out _));
            Assert.Equal(JsonValueKind.Null, move.GetProperty("at").GetProperty("months").ValueKind);
        }

        [Fact]
        public void AMonthAPersonNamedIsWalked()
        {
            TestDesignEdit october = new("#0", "months.create", [KeyValuePair.Create("months", "2026-10")]);

            TestDesign design = Ledger(october);

            Assert.Equal(EditOutcome.Carried, Assert.Single(design.Edits).Outcome);
            Assert.Contains("months.create@2026-10", design.States[0].Moves.Select(static move => move.Text));
            Assert.Equal("months.create@2026-10", design.States[1].By);
            Assert.Equal(["/", "months/2026-10/claims/1"], design.States[1].Documents.Select(static document => document.Key));
            Assert.Contains("months.claims.enterAmount@2026-10/1(amount: 1)", design.States[1].Moves.Select(static move => move.Text));
        }

        [Fact]
        public void AChoiceAtAnInstanceIsCarriedAcrossADiff()
        {
            TestDesignEdit october = new("#0", "months.create", [KeyValuePair.Create("months", "2026-10")]);
            TestDesign design = Ledger(october);

            TestDesignDiff diff = TestDesignDiff.Of(
                FixtureStore.Runtime(), design.ToJson(), Vocabulary.Read("claim-ledger"), Held, Starts);

            Assert.True(diff.IsEmpty, diff.ToString());
            EditResult carried = Assert.Single(diff.After.Edits);
            Assert.Equal(EditOutcome.Carried, carried.Outcome);
            Assert.Equal([KeyValuePair.Create("months", "2026-10")], carried.Edit.At);
        }

        // Where the schema names every key, every month is walked and nothing waits.
        [Fact]
        public void ALedgerWhoseKeysAreListedWalksEveryOne()
        {
            string listed = Vocabulary.Read("claim-ledger").Replace(
                """{ "op": "type.string", "minLength": 7, "maxLength": 7 }""",
                """{ "op": "type.enum", "values": ["2026-10", "2026-11"] }""",
                StringComparison.Ordinal);

            TestDesign design = TestDesign.Derive(FixtureStore.Runtime(), listed, Held, Starts, null, new WalkSettings(States: 30), null);

            Assert.Equal(
                ["months.create@2026-10", "months.create@2026-11"],
                design.States[0].Moves.Select(static move => move.Text));
        }

        // A rule set that keeps none is one document, and the runtime the host gave is left alone.
        [Fact]
        public void ARuleSetThatKeepsNoInstancesWritesWhatItAlwaysWrote()
        {
            TestDesign design = TestDesign.Derive(Vocabulary.Runtime, Vocabulary.Read("approval"), new WalkSettings(States: 300));
            JsonElement opening = JsonDocument.Parse(design.ToJson()).RootElement.GetProperty("states")[0];

            Assert.True(opening.TryGetProperty("state", out _));
            Assert.False(opening.TryGetProperty("documents", out _));
            Assert.Empty(design.Keys);
        }

        // A step reads the month and the claim it is at and nothing else, so one already
        // followed from the same two is not followed again: the walk comes to an end rather
        // than creating claims for as long as it is allowed to.
        [Fact]
        public void AMonthIsWalkedToTheEnd()
        {
            TestDesign design = Month();

            Assert.Equal(0, design.Unreached);
            Assert.True(design.States.Count < 3000);
        }

        [Fact]
        public void AStepAlreadyFollowedSaysWhereItWas()
        {
            TestDesign design = Month();

            (TestDesignState state, Move move) = design.States
                .SelectMany(static state => state.Moves.Select(move => (state, move)))
                .First(static pair => pair.move.Landings.Any(static landing => landing.As is not null));
            Landing landing = Assert.Single(move.Landings);
            Assert.Null(landing.To);
            TestDesignState followed = design.States.Single(each => each.Name == landing.As);
            Assert.Contains(followed.Moves, each => each.Text == move.Text && each.Landings.Single().To is not null);

            JsonElement written = JsonDocument.Parse(design.ToJson()).RootElement.GetProperty("states")
                .EnumerateArray().Single(each => each.GetProperty("name").GetString() == state.Name)
                .GetProperty("moves").EnumerateArray().Single(each => Matches(each, move));
            Assert.Equal(landing.As, written.GetProperty("as").GetString());
            Assert.False(written.TryGetProperty("to", out _));
        }

        private static bool Matches(JsonElement written, Move move) =>
            written.TryGetProperty("input", out JsonElement input) && input.GetString() == move.Input
            && (written.TryGetProperty("at", out JsonElement at) ? at.GetRawText() : "") == (move.At.Count == 0 ? "" : at.GetRawText())
            && TestDesign.FromJson($$"""{"$schema":"ruledger/test-design/v5","ruleSet":"x","settings":{"states":1,"candidates":1,"outcomes":1},"observed":[],"collapsed":[],"unreached":0,"edits":[],"states":[{"name":"#0","state":{},"evaluated":0,"moves":[{{written.GetRawText()}}]}]}""")
                .States[0].Moves[0].Text == move.Text;

        private static TestDesign Month(int states = 3000) =>
            TestDesign.Derive(FixtureStore.Runtime(), Vocabulary.Read("claim-month"), Held, Starts, null, new WalkSettings(States: states), null);

        private static TestDesign Ledger(params TestDesignEdit[] edits) =>
            TestDesign.Derive(FixtureStore.Runtime(), Vocabulary.Read("claim-ledger"), Held, Starts, null, new WalkSettings(States: 30), edits);

        private static JsonElement Data(string state) =>
            JsonDocument.Parse(state).RootElement.GetProperty("data").Clone();

        private static string Compact(string json) => JsonDocument.Parse(json).RootElement.GetRawText().Replace(" ", string.Empty, StringComparison.Ordinal);
    }
}
