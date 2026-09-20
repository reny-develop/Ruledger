// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Rulealize;
using Rulealize.Abstraction;

namespace Ruledger.Cli
{
    /// <summary>Applies a committed test design to a version of the rule set, which is to say diffs the two.</summary>
    /// <remarks>
    /// <para>
    /// The two are one operation described from two sides. A test design says what its own
    /// rule set decides, so running it against that rule set can only pass; running it
    /// against the next version is where it first says something, and what it says is the
    /// blast radius of the change.
    /// </para>
    /// <para>
    /// Everything is listed. A change to a state, a state that is not walked any more, a
    /// state that was not there before — all of them, with no cap and no sample, because a
    /// diff that showed the first twenty would be a diff that decided which twenty mattered.
    /// The counts come first so that a run whose output is scrolling past still says how
    /// much of it there is.
    /// </para>
    /// </remarks>
    internal static class DiffCommand
    {
        public static int Run(string testDesignPath, string ruleSetPath, string plugins, string? ruleSets, bool write)
        {
            if (!File.Exists(testDesignPath))
            {
                Console.Error.WriteLine($"'{testDesignPath}' does not exist.");
                return Exit.Undone;
            }

            if (!File.Exists(ruleSetPath))
            {
                Console.Error.WriteLine($"'{ruleSetPath}' does not exist.");
                return Exit.Undone;
            }

            if (PluginFolder.Load(plugins) is not RuleRuntime runtime)
            {
                return Exit.Undone;
            }

            string ruleSet = File.ReadAllText(ruleSetPath);

            if (Held.Gather(ruleSetPath, ruleSet, ruleSets) is not IReadOnlyDictionary<string, string> held)
            {
                return Exit.Undone;
            }

            TestDesignDiff diff;
            try
            {
                diff = TestDesignDiff.Of(runtime, File.ReadAllText(testDesignPath), ruleSet, held);
            }
            catch (Exception failure) when (failure is RuleSetBuildException or JsonException or InvalidOperationException)
            {
                Console.Error.WriteLine($"'{testDesignPath}' could not be applied to '{ruleSetPath}':");
                Console.Error.WriteLine($"  {failure.Message}");
                return Exit.Undone;
            }

            Console.WriteLine($"{diff.Before.RuleSet} -> {diff.After.RuleSet}");
            Console.WriteLine(
                $"  {Report.Counted(diff.Common, "state", "states")} in both, "
                + $"{Report.Number(diff.Changed.Count)} decided differently");

            if (diff.Gone.Count > 0 || diff.Appeared.Count > 0)
            {
                Console.WriteLine(
                    $"  {Report.Number(diff.Gone.Count)} only in the test design, "
                    + $"{Report.Number(diff.Appeared.Count)} only in this walk");
            }

            // Said before the lines it changes the reading of. When the two versions do not
            // agree on what a position is compared on, the walks rejoined in different places,
            // and part of what follows is the walk having gone elsewhere rather than the rules
            // having decided differently. The tool says which of the two it cannot tell apart.
            if (!diff.ComparedTheSameWay)
            {
                Console.WriteLine();
                Console.WriteLine("  the two are not compared on the same state:");
                Console.WriteLine($"    was {Report.Observation(diff.Before.Observed, diff.Before.Collapsed)}");
                Console.WriteLine($"    now {Report.Observation(diff.After.Observed, diff.After.Collapsed)}");
                Console.WriteLine("    so where a route rejoined has moved, and some of what follows is that");
            }

            // Named by the number the test design gives it, because that is the document
            // the person has open, and by this walk's number as well where the two differ.
            // How the walk arrives is the name that means the same in both, and it is not
            // what is printed: at reversi's depth one of them is sixty inputs long, which is
            // longer than anything it would help anybody read. The test design carries it.
            Write(diff.Changed.Select(change => $"{Named(change)}: {Said(change)}"));
            Write(diff.Gone.Select(state => $"{state}: not walked any more"));
            Write(diff.Appeared.Select(state => $"{state}: reached now, and was not"));

            if (diff.After.Edits.Count > 0)
            {
                Console.WriteLine();
            }

            bool all = Report.Choices(diff.After);

            if (write)
            {
                File.WriteAllText(testDesignPath, diff.After.ToJson());
                Console.WriteLine($"  -> {testDesignPath}");
            }

            // Compared on different state is a third thing to report, and it counts. The
            // diff below it can be empty and still not mean what an empty diff means: the two
            // walks rejoined in different places, so states held against each other were not
            // always the same states. A build that went green on that would be a build that
            // passed because nobody was told.
            return diff.IsEmpty && all && diff.ComparedTheSameWay ? Exit.Done : Exit.Different;
        }

        private static void Write(IEnumerable<string> lines)
        {
            bool any = false;
            foreach (string line in lines)
            {
                if (!any)
                {
                    Console.WriteLine();
                    any = true;
                }

                Console.WriteLine($"  {line}");
            }
        }

        private static string Named(StateChange change) =>
            string.Equals(change.Before.Name, change.After.Name, StringComparison.Ordinal)
                ? change.Before.ToString()
                : $"{change.Before} (now {change.After.Name})";

        private static string Said(StateChange change)
        {
            List<string> what = [];

            if (change.Lost.Count > 0)
            {
                what.Add("lost " + string.Join(", ", change.Lost));
            }

            if (change.Gained.Count > 0)
            {
                what.Add("gained " + string.Join(", ", change.Gained));
            }

            foreach (MoveChange moved in change.Moved)
            {
                what.Add($"{moved.Before} now -> {Lands(moved.After)}, was -> {Lands(moved.Before)}");
            }

            if (change.EndingMoved)
            {
                what.Add($"{Ending(change.After)}, was {Ending(change.Before)}");
            }

            if (change.TruncationMoved)
            {
                what.Add(change.After.Truncated
                    ? "the search for legal inputs stopped at the limit, and did not before — what is said about "
                        + "this state is part of what there is"
                    : "the search for legal inputs no longer stops at the limit");
            }

            return string.Join("; ", what);
        }

        // Where an input leads, by the number each walk gave it. Two numberings, said as
        // 'now' and as what it was: the same place has a different number in the two test
        // designs whenever the walk rejoined somewhere else, and that is itself the news.
        // A branch the walk stopped in front of is that, and never an empty place.
        private static string Lands(Move move) =>
            move.Landings.Count is 0
                ? "nowhere, the state being final"
                : string.Join(" ", move.Landings.Select(static landing =>
                    landing.To ?? "not reached before the walk stopped"));

        private static string Ending(TestDesignState state) =>
            state.IsTerminal ? "final " + (state.Result ?? "(no result)") : "not final";
    }
}
