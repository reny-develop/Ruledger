// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize;

namespace Ruledger.Tests
{
    // The plugin packages land beside this assembly, so the runtime finds them the way a
    // deployed application does: by scanning a folder, naming no plugin type at all.
    internal static class Vocabulary
    {
        private static readonly Lazy<RuleRuntime> Loaded =
            new(static () => new RuleRuntime().LoadPluginsFrom(AppContext.BaseDirectory));

        public static RuleRuntime Runtime => Loaded.Value;

        public static string Read(string ruleSet) =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ruleset", ruleSet + ".json"));
    }
}
