// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Rulealize;
using Rulealize.Abstraction;

namespace Ruledger.Cli
{
    /// <summary>Walks a rule set and writes the test design down.</summary>
    /// <remarks>
    /// <para>
    /// The test design is a file to be committed, so this writes one rather than printing a
    /// document to be redirected. The file it writes is also the file it reads: a test design
    /// already there is where the choices come from, which makes re-deriving after a change
    /// to the rules the ordinary thing to do rather than the thing that loses an afternoon.
    /// </para>
    /// <para>
    /// A choice that could not be taken is reported and the command says so in its exit code.
    /// It is never dropped back to the input the walk would have taken by itself — that
    /// would be the machine deciding something a person had decided, and doing it in silence.
    /// </para>
    /// </remarks>
    internal static class DeriveCommand
    {
        public static int Run(
            string ruleSetPath,
            string plugins,
            string? ruleSets,
            string? outPath,
            string? fromPath,
            WalkSettings settings)
        {
            if (!File.Exists(ruleSetPath))
            {
                Console.Error.WriteLine($"'{ruleSetPath}' does not exist.");
                return Exit.Undone;
            }

            string written = outPath ?? Beside(ruleSetPath);
            string carryFrom = fromPath ?? written;

            if (fromPath is not null && !File.Exists(fromPath))
            {
                Console.Error.WriteLine($"'{fromPath}' does not exist, and --from names where the choices are.");
                return Exit.Undone;
            }

            if (Choices(carryFrom) is not Carry carry)
            {
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

            TestDesign design;
            try
            {
                design = TestDesign.Derive(runtime, ruleSet, held, settings, carry.Edits);
            }
            catch (Exception failure) when (failure is RuleSetBuildException or JsonException or InvalidOperationException)
            {
                Console.Error.WriteLine($"'{ruleSetPath}' could not be walked:");
                Console.Error.WriteLine($"  {failure.Message}");
                return Exit.Undone;
            }

            Report.Walk(design);

            if (carry.From is not null && carry.Edits.Count > 0)
            {
                Console.WriteLine($"  choices from '{carry.From}'");
            }

            bool all = Report.Choices(design);

            File.WriteAllText(written, design.ToJson());
            Console.WriteLine($"  -> {written}");

            return all ? Exit.Done : Exit.Different;
        }

        /// <summary>Where the test design of a rule set goes when nothing says otherwise.</summary>
        /// <remarks>
        /// Beside the rule set, named after it. The two change together and are read together,
        /// and a default that puts them in one place is what lets a second derive find the
        /// choices of the first without being told where they are.
        /// </remarks>
        public static string Beside(string ruleSetPath)
        {
            string named = Path.GetFileNameWithoutExtension(ruleSetPath) + ".test-design.json";

            // Written back the way the rule set was named on the command line: a document
            // given as 'reversi.json' has its test design at 'reversi.test-design.json', and
            // not at a path with a './' in front that nobody typed.
            return Path.GetDirectoryName(ruleSetPath) is { Length: > 0 } directory
                ? Path.Combine(directory, named)
                : named;
        }

        // A test design already there is read for its choices and nothing else: what it says
        // about the states is what the previous rule set decided, and this is about the one on
        // disk now. The choices are written out as how the walk arrives, so they still name a
        // state after the rule set has changed.
        private static Carry? Choices(string path)
        {
            if (!File.Exists(path))
            {
                return new Carry([], null);
            }

            try
            {
                TestDesign before = TestDesign.FromJson(File.ReadAllText(path));
                return new Carry([.. before.Edits.Select(result => result.Edit)], path);
            }
            catch (Exception failure) when (failure is JsonException or InvalidOperationException)
            {
                Console.Error.WriteLine($"'{path}' is where the choices would come from, and it could not be read:");
                Console.Error.WriteLine($"  {failure.Message}");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Move it aside to derive without them, or name another with --from.");
                return null;
            }
        }

        /// <summary>The choices a walk is to take first, and the test design they were read out of.</summary>
        private sealed record Carry(IReadOnlyList<TestDesignEdit> Edits, string? From);
    }
}
