// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Tests
{
    /// <summary>What the scan makes of a rule set written to put one question to it.</summary>
    /// <remarks>
    /// The measured rule sets and what the scan says about each of them are in
    /// <c>verify/</c>. Here the rule sets are written for the question: the half of the
    /// answer that cannot be seen by reading guards alone, and whose move it is.
    /// </remarks>
    public class ObservationScanTests
    {
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
        // reads would merge two states the test design goes on printing two movers for.
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

        // What a projection says is written down for every state, so a field only a
        // projection reads decides whether two positions are one state as a guard's would.
        [Fact]
        public void AFieldOnlyAProjectionReadsIsKept()
        {
            const string ruleSet = """
                {
                  "id": "label", "version": "1.0.0",
                  "state": {
                    "schema": { "stage": {}, "title": {} },
                    "initial": { "stage": "open", "title": "" }
                  },
                  "inputs": {
                    "close": {
                      "when": { "op": "cmp.eq", "left": "$stage", "right": "open" },
                      "effects": [ { "op": "state.set", "path": "stage", "value": "closed" } ]
                    }
                  },
                  "projections": { "heading": "$title" }
                }
                """;

            ObservationScan scan = ObservationScan.Of(ruleSet);

            Assert.Equal(["stage", "title"], scan.Observed);
            Assert.Empty(scan.Collapsed);
        }

        // A composite answers for its own projections and offers none of a component's, so
        // the one document it stands for would not have them, and a field only a component's
        // projection reads is not the composite's to keep.
        [Fact]
        public void AComponentsProjectionIsNotTheCompositesToObserve()
        {
            const string component = """
                {
                  "id": "door", "version": "1.0.0",
                  "state": {
                    "schema": { "open": {}, "colour": {} },
                    "initial": { "open": false, "colour": "red" }
                  },
                  "inputs": {
                    "open": {
                      "when": { "op": "cmp.eq", "left": "$open", "right": false },
                      "effects": [ { "op": "state.set", "path": "open", "value": true } ]
                    }
                  },
                  "projections": { "paint": "$colour" }
                }
                """;
            const string composite = """
                { "id": "house", "version": "1.0.0", "uses": [ { "ruleSet": "door", "version": "^1.0", "as": "front" } ] }
                """;

            Dictionary<string, string> components = new(StringComparer.Ordinal) { ["door"] = component };

            Assert.Equal(["open", "colour"], ObservationScan.Of(component).Observed);
            Assert.Equal(["front.open"], ObservationScan.Of(composite, components).Observed);
            Assert.Equal(["front.colour"], ObservationScan.Of(composite, components).Collapsed);
        }
    }
}
