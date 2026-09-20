// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace Ruledger.Cli
{
    /// <summary>What every command says about a walk, said the same way by each of them.</summary>
    /// <remarks>
    /// <para>
    /// Three of these lines exist because leaving them out would be dishonest rather than
    /// brief. Where a walk stopped for want of states is not nothing being there; a choice that
    /// could not be carried is not a choice the machine may make instead; and two versions
    /// compared on different state are two walks that rejoined in different places, which is
    /// not the rules deciding differently. Each is reported where it happened.
    /// </para>
    /// <para>
    /// No number stands in for how much of a rule set was covered. The only quantity here
    /// that could be read as one is how many landings the walk had no states left for, and it
    /// is a count of places rather than a share of anything.
    /// </para>
    /// </remarks>
    internal static class Report
    {
        /// <summary>Writes what a walk found: how far it got, how it ended, and what it compared on.</summary>
        public static void Walk(TestDesign design)
        {
            Console.WriteLine(design.RuleSet);
            Console.WriteLine($"  {States(design)}");

            if (design.Unreached > 0)
            {
                Console.WriteLine(
                    $"  {Number(design.Unreached)} {(design.Unreached is 1 ? "landing" : "landings")} "
                    + "the walk had no states left for");
            }

            Console.WriteLine($"  {Observation(design.Observed, design.Collapsed)}");
            Console.WriteLine(
                $"  at most {Number(design.Settings.States)} states, "
                + $"{Number(design.Settings.Candidates)} candidates in a state, "
                + $"{Number(design.Settings.Outcomes)} outcomes for an input");
        }

        /// <summary>Writes every choice a person made and what became of it.</summary>
        /// <returns>True when all of them were taken, which includes there having been none.</returns>
        /// <remarks>
        /// Every one of them, and the ones that were lost with the way they were lost. A tool
        /// that reported only the ones it managed would be a tool that quietly chose for
        /// somebody, and there is no code path here that does.
        /// </remarks>
        public static bool Choices(TestDesign design)
        {
            if (design.Edits.Count is 0)
            {
                return true;
            }

            int carried = design.Edits.Count(edit => edit.Outcome == EditOutcome.Carried);
            if (carried == design.Edits.Count)
            {
                Console.WriteLine($"  {Counted(carried, "choice", "choices")}, all carried");
                return true;
            }

            Console.WriteLine($"  {carried} of {design.Edits.Count} choices carried");

            foreach (EditResult result in design.Edits.Where(edit => edit.Outcome != EditOutcome.Carried))
            {
                Console.WriteLine($"    {Lost(result)}");
            }

            return false;
        }

        /// <summary>Writes a number the same way wherever it lands.</summary>
        public static string Number(int number) => number.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>Counts something, in words, so that a line reads as English.</summary>
        public static string Counted(int number, string one, string many) =>
            number is 1 ? $"1 {one}" : $"{Number(number)} {many}";

        /// <summary>Writes what a position is compared on, and what is dropped before comparing.</summary>
        public static string Observation(IReadOnlyList<string> observed, IReadOnlyList<string> collapsed)
        {
            string on = observed.Count is 0 ? "nothing" : string.Join(", ", observed);
            return collapsed.Count is 0
                ? $"compared on {on}"
                : $"compared on {on} — {string.Join(", ", collapsed)} dropped";
        }

        private static string States(TestDesign design)
        {
            List<string> endings =
            [
                .. design.States
                    .Where(state => state.IsTerminal)
                    .GroupBy(state => state.Result ?? "(no result)", StringComparer.Ordinal)
                    .OrderBy(ending => ending.Key, StringComparer.Ordinal)
                    .Select(ending => $"{ending.Key} {Number(ending.Count())}"),
            ];

            int final = design.States.Count(state => state.IsTerminal);

            return endings.Count is 0
                ? $"{Number(design.States.Count)} states, none of them final"
                : $"{Number(design.States.Count)} states, {Number(final)} final: {string.Join(", ", endings)}";
        }

        // Which of the two ways it went missing, said of each one. They are different enough
        // to be worth telling apart: an input that is not legal any more is a decision the new
        // rules made about a state that is still there, and a state the walk did not reach is
        // where it stopped rather than what the rules say.
        //
        // The input comes first and the state second, because the state is named by how the
        // walk arrives at it and that name is as long as the walk is deep. Twenty steps in it
        // runs to hundreds of characters, and a line whose first hundreds of characters are
        // the same as the line above it is a line nobody reads to the end of.
        private static string Lost(EditResult result) => result.Outcome switch
        {
            EditOutcome.NotLegal =>
                $"{Input(result.Edit)} is not legal in {result.Name ?? result.Edit.State}",
            _ => $"{Input(result.Edit)} was not chosen: {result.Edit.State} was not reached before the walk stopped",
        };

        private static string Input(TestDesignEdit edit) =>
            edit.Arguments.Count is 0
                ? edit.Input
                : $"{edit.Input}({string.Join(", ", edit.Arguments.Select(argument => $"{argument.Key}: {argument.Value}"))})";
    }
}
