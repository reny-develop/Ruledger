// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize;
using Rulealize.Abstraction.Plugin;
using Rulealize.Abstraction.Store;

namespace Ruledger.Tests
{
    /// <summary>The store <c>claim-month</c> and <c>claim-ledger</c> name, which nothing ever opens.</summary>
    /// <remarks>
    /// A store plugin declares what an opened store is and does no I/O, and that is all a walk
    /// needs of it: the rule sets are built against it, and the documents are kept in a
    /// <see cref="MemoryStateStore"/> in its place. Opening one fails, so a walk that reached
    /// for the real store would say so.
    /// </remarks>
    internal sealed class FixtureStore : IStateStore
    {
        public const string Plugin = "Ruledger.Fixture.Store";

        /// <summary>The published vocabularies, scanned from beside this assembly, and this store's plugin.</summary>
        /// <remarks>A runtime of its own each time, because a walk of a rule set that keeps instances gives it a store.</remarks>
        public static RuleRuntime Runtime() =>
            new RuleRuntime().LoadPluginsFrom(AppContext.BaseDirectory).AddPlugin(new FixtureStorePlugin());

        public Task<IStateStoreSession> OpenSessionAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Nothing should open the store a rule set names while it is walked.");
    }

    /// <summary>Declares <see cref="FixtureStore"/>, which is all a store plugin has to do.</summary>
    /// <remarks>Internal, so a folder sweep passes over it and only <see cref="FixtureStore.Runtime"/> adds it.</remarks>
    internal sealed class FixtureStorePlugin : IRulealizePlugin
    {
        public PluginManifest Manifest { get; } = new(FixtureStore.Plugin, new Version(1, 0, 0), "fixture");

        public void Register(IPluginRegistry registry) => registry.AddStore<FixtureStore>();
    }
}
