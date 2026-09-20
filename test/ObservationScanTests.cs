// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Tests
{
    public class ObservationScanTests
    {
        public static TheoryData<string> Measured =>
            ["reversi", "roster", "chess", "shogi", "blackjack", "deploy"];

        [Theory]
        [MemberData(nameof(Measured))]
        public void EveryFieldIsEitherObservedOrCollapsed(string ruleSet)
        {
            ObservationScan scan = Scan(ruleSet);

            Assert.DoesNotContain(scan.Observed, scan.Collapsed.Contains);
            Assert.NotEmpty(scan.Observed);
        }

        // An audit trail nobody reads is the case the scan exists for: it is written by every
        // input, so a walk that told states apart by it would never rejoin.
        [Theory]
        [InlineData("roster")]
        [InlineData("deploy")]
        public void AnAuditTrailCollapses(string ruleSet) =>
            Assert.Contains("log", Scan(ruleSet).Collapsed);

        // Nothing here is carried that an observation does not reach.
        [Theory]
        [InlineData("reversi")]
        [InlineData("chess")]
        [InlineData("shogi")]
        public void ABoardGameObservesAllOfIt(string ruleSet) =>
            Assert.Empty(Scan(ruleSet).Collapsed);

        // The scan reacts to what a field is used for, not to what it is called. Renaming the
        // audit trail leaves it on the same side.
        [Fact]
        public void TheAnswerIsNotDrawnFromNames()
        {
            string renamed = Read("roster").Replace("\"log\"", "\"history\"", StringComparison.Ordinal);

            Assert.Contains("history", ObservationScan.Of(renamed).Collapsed);
        }

        // Reading it from anywhere observable is enough to keep it, which is the half that
        // cannot be seen by looking at the guards alone.
        [Fact]
        public void AFieldAnEffectFeedsIntoAnObservedOneIsKept()
        {
            const string ruleSet = """
                {
                  "id": "audit", "version": "1.0.0",
                  "state": {
                    "schema": { "stage": {}, "seen": {}, "log": {} },
                    "initial": { "stage": "draft", "seen": 0, "log": [] }
                  },
                  "inputs": {
                    "step": {
                      "when": { "op": "cmp.eq", "left": "$stage", "right": "draft" },
                      "effects": [
                        { "op": "state.set", "path": "seen", "value": { "op": "seq.count", "source": "$log" } },
                        { "op": "state.set", "path": "log", "value": { "op": "seq.of", "of": ["$stage"] } }
                      ]
                    }
                  },
                  "terminal": { "when": { "op": "cmp.gt", "left": "$seen", "right": 3 }, "result": "done" }
                }
                """;

            ObservationScan scan = ObservationScan.Of(ruleSet);

            Assert.Equal(["stage", "seen", "log"], scan.Observed);
            Assert.Empty(scan.Collapsed);
        }

        // A legal input is its name, its arguments and whose it is, so whatever decides the
        // mover is observed like whatever decides legality. Collapsing a field only `actor`
        // reads would merge two states the design goes on printing two movers for.
        [Fact]
        public void WhoseMoveItIsIsPartOfWhatIsLegal()
        {
            const string ruleSet = """
                {
                  "id": "relay", "version": "1.0.0",
                  "state": {
                    "schema": { "stage": {}, "holder": {} },
                    "initial": { "stage": "open", "holder": "ann" }
                  },
                  "inputs": {
                    "pass": {
                      "actor": "$holder",
                      "when": { "op": "cmp.eq", "left": "$stage", "right": "open" },
                      "effects": [ { "op": "state.set", "path": "holder", "value": "bo" } ]
                    }
                  },
                  "terminal": { "when": { "op": "cmp.eq", "left": "$stage", "right": "done" }, "result": "done" }
                }
                """;

            ObservationScan scan = ObservationScan.Of(ruleSet);

            Assert.Equal(["stage", "holder"], scan.Observed);
            Assert.Empty(scan.Collapsed);
        }

        private static ObservationScan Scan(string ruleSet) => ObservationScan.Of(Read(ruleSet));

        private static string Read(string ruleSet) =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ruleset", ruleSet + ".json"));
    }
}
