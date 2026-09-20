// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Ruledger.Cli
{
    /// <summary>Says what a position is compared on, and what is dropped before comparing.</summary>
    /// <remarks>
    /// <para>
    /// The answer a walk works out for itself, asked on its own. It is worth asking on its
    /// own because it decides how far a walk rejoins, and a rule set that keeps a history
    /// does not rejoin at all if the history is compared: the field being on the dropped
    /// side is the difference between a walk that comes back to a position and one that
    /// never does.
    /// </para>
    /// <para>
    /// This reads the document and never runs it, so it is the one command that answers
    /// about a rule set whose vocabularies are not to hand. No plugin folder is loaded.
    /// </para>
    /// </remarks>
    internal static class ObserveCommand
    {
        public static int Run(string ruleSetPath, string? ruleSets)
        {
            if (!File.Exists(ruleSetPath))
            {
                Console.Error.WriteLine($"'{ruleSetPath}' does not exist.");
                return Exit.Undone;
            }

            string ruleSet = File.ReadAllText(ruleSetPath);

            if (Held.Gather(ruleSetPath, ruleSet, ruleSets) is not IReadOnlyDictionary<string, string> held)
            {
                return Exit.Undone;
            }

            ObservationScan scan;
            try
            {
                scan = ObservationScan.Of(ruleSet, held);
            }
            catch (Exception failure) when (failure is JsonException or InvalidOperationException)
            {
                Console.Error.WriteLine($"'{ruleSetPath}' could not be read:");
                Console.Error.WriteLine($"  {failure.Message}");
                return Exit.Undone;
            }

            Console.WriteLine(ruleSetPath);
            Console.WriteLine($"  compared on: {Listed(scan.Observed)}");
            Console.WriteLine($"  dropped:     {Listed(scan.Collapsed)}");

            return Exit.Done;
        }

        // A rule set every field of which is observed drops nothing, and one whose state
        // nothing reads is compared on nothing. Both are answers; neither is an empty line.
        private static string Listed(IReadOnlyList<string> fields) =>
            fields.Count is 0 ? "(nothing)" : string.Join(", ", fields);
    }
}
