// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize;
using Rulealize.Abstraction.Plugin;

namespace Ruledger.Cli
{
    /// <summary>Reading the folder of vocabularies a rule set draws on.</summary>
    /// <remarks>
    /// The same folder <c>rulealize restore</c> fills, read the same way a deployed
    /// application reads one: assemblies on disk, swept and instantiated, no plugin named.
    /// Ruledger fetches nothing itself — a rule set is tried with one tool and measured with
    /// another, and two tools racing to fill one folder is a folder nobody can account for.
    /// </remarks>
    internal static class PluginFolder
    {
        public const string Default = "plugin";

        /// <summary>Loads a folder of vocabularies.</summary>
        /// <param name="folder">Where they are.</param>
        /// <returns>The runtime, or null when why not has already been written to standard error.</returns>
        public static RuleRuntime? Load(string folder)
        {
            if (!Directory.Exists(folder))
            {
                Console.Error.WriteLine($"'{folder}' does not exist. 'rulealize restore <rule set>' fills it.");
                return null;
            }

            try
            {
                RuleRuntime runtime = new RuleRuntime().LoadPluginsFrom(folder);

                if (runtime.Plugins.Length is 0)
                {
                    Console.Error.WriteLine($"'{folder}' holds no vocabulary at all. "
                        + "'rulealize plugins' says why, and 'rulealize restore <rule set>' fills it.");
                    return null;
                }

                return runtime;
            }
            catch (PluginLoadException failure)
            {
                Console.Error.WriteLine($"'{folder}' could not be read: {failure.Message}");
                return null;
            }
        }

        /// <summary>Counts something, in words, so that a line reads as English.</summary>
        public static string Count(int number, string one, string many) =>
            number is 1 ? $"1 {one}" : $"{number} {many}";
    }
}
