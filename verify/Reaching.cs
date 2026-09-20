// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Rulealize;
using Rulealize.Abstraction;

namespace Ruledger.Verify
{
    /// <summary>How far a walk gets, and what it has to do to observe an ending.</summary>
    /// <remarks>
    /// Two claims about the budget. The first is that walking one route to the end is not a
    /// preference: spreading out by depth instead collects openings and finishes no game, so
    /// no result is ever observed. The second is that raising the budget does not always
    /// observe more — what a walk did not reach is what a walk did not reach, and reading it
    /// as what is not there is the misreading the whole method was written against.
    /// </remarks>
    public class Reaching(Xunit.Abstractions.ITestOutputHelper said)
    {
        // The control, and it is walked the wrong way on purpose, so it is walked here rather
        // than by Ruledger: spreading out by depth is not a setting the tool offers, and the
        // reason it is not is what this measures. Two positions are the same position when
        // they agree on every observed field, which for reversi is the whole state — nothing
        // in it collapses — so comparing the documents is comparing exactly those fields.
        [Fact]
        public void WalkingOneRouteToTheEndIsWhatObservesAnEnding()
        {
            const int budget = 3000;

            Assert.Empty(ObservationScan.Of(Fixture.RuleSet("reversi")).Collapsed);

            (int depth, int endings) = Broadest("reversi", budget);
            TestDesign design = Fixture.Walk("reversi", budget);
            int deepest = Deepest(design);
            int reached = design.States.Count(state => state.IsTerminal);

            said.WriteLine($"reversi at budget {budget}: spreading out by depth reaches depth {depth} "
                + $"and {endings} endings; one route to the end reaches depth {deepest} and {reached} endings");

            Assert.Equal(0, endings);
            Assert.True(depth < 10, $"spreading out by depth got to {depth}");
            Assert.Equal(62, deepest);
            Assert.Equal(849, reached);
        }

        // Chess is drawn out far enough that a budget raised five-fold reaches five times as
        // many states and not one more kind of ending. Which is the point: `draw` being the
        // only result here says the walk did not reach a mate, and says nothing at all about
        // whether chess has one.
        [Fact]
        public void RaisingTheBudgetDoesNotAlwaysObserveMore()
        {
            TestDesign shorter = Fixture.Walk("chess", 300);
            TestDesign longer = Fixture.Walk("chess", 1500);

            said.WriteLine($"chess at budget 300: {shorter.States.Count(state => state.IsTerminal)} endings, "
                + $"results {string.Join('/', Fixture.Results(shorter))}");
            said.WriteLine($"chess at budget 1500: {longer.States.Count(state => state.IsTerminal)} endings, "
                + $"results {string.Join('/', Fixture.Results(longer))}");

            Assert.Equal(["draw"], Fixture.Results(shorter));
            Assert.Equal(["draw"], Fixture.Results(longer));
        }

        // How deep the walk got, counted off the state each one was first reached from.
        private static int Deepest(TestDesign design)
        {
            Dictionary<string, int> depth = new(StringComparer.Ordinal);
            int deepest = 0;

            foreach (TestDesignState state in design.States)
            {
                int at = state.From is null ? 0 : depth[state.From] + 1;
                depth[state.Name] = at;
                deepest = Math.Max(deepest, at);
            }

            return deepest;
        }

        // The same rule set, the same budget, and every state at one depth taken before any
        // state at the next.
        private static (int Depth, int Endings) Broadest(string ruleSet, int budget)
        {
            RuleContext rules = Fixture.Runtime.CreateContext(Fixture.RuleSet(ruleSet), null);

            HashSet<string> seen = new(StringComparer.Ordinal) { Position(rules.InitialState) };
            Queue<(string State, int Depth)> pending = new();
            pending.Enqueue((rules.InitialState, 0));

            int deepest = 0;
            int endings = 0;

            while (pending.Count > 0 && seen.Count < budget)
            {
                (string state, int depth) = pending.Dequeue();
                deepest = Math.Max(deepest, depth);

                TerminalStatus terminal = rules.GetTerminalStatus(state);
                if (terminal.IsTerminal)
                {
                    endings++;
                    continue;
                }

                foreach (ValidInput input in rules.GetValidInputs(state, 10000))
                {
                    foreach (Outcome outcome in rules.GetOutcomes(input.ToInputDocument(rules.RuleSet), state, 64))
                    {
                        if (seen.Count >= budget)
                        {
                            break;
                        }

                        if (seen.Add(Position(outcome.Result.State)))
                        {
                            pending.Enqueue((outcome.Result.State, depth + 1));
                        }
                    }
                }
            }

            return (deepest, endings);
        }

        private static string Position(string state)
        {
            using JsonDocument document = JsonDocument.Parse(state);
            return document.RootElement.GetProperty("data").GetRawText();
        }
    }
}
