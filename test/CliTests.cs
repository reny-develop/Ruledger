// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Ruledger.Cli;

namespace Ruledger.Tests
{
    /// <summary>What the tool prints and what it exits with.</summary>
    /// <remarks>
    /// The exit code and the lines are the whole of the interface v1 publishes, so both are
    /// held to here. The two that are acceptance conditions rather than conveniences are the
    /// third exit code — a choice that could not be carried says so on the way out, and a
    /// build that runs this cannot pass without noticing — and the absence of any path that
    /// takes the machine's own pick when a person's could not be taken.
    /// </remarks>
    [Collection("console")]
    public class CliTests : IDisposable
    {
        private readonly string folder = Directory.CreateTempSubdirectory("ruledger-cli").FullName;

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            try
            {
                Directory.Delete(this.folder, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Left for the operating system, the way a temporary directory is.
            }
        }

        [Fact]
        public void DeriveWritesTheTestDesignBesideTheRuleSet()
        {
            string ruleSet = Copy("approval");

            (int code, string said) = Run(() => DeriveCommand.Run(
                ruleSet, Plugins, null, null, null, new WalkSettings(Budget: 3000)));

            Assert.Equal(0, code);
            Assert.True(File.Exists(Path.Combine(this.folder, "approval.test-design.json")));
            Assert.Contains("approval@1.0.0", said, StringComparison.Ordinal);
            Assert.Contains("4 states, 2 final: approved 1, rejected 1", said, StringComparison.Ordinal);
        }

        // Twice over, byte for byte. Acceptance condition 5 through the tool rather than
        // through the library: a walk that came out the same way in memory and a different
        // way through a file would be a walk nobody could diff.
        [Fact]
        public void TheSameWalkTwiceIsTheSameFile()
        {
            string ruleSet = Copy("roster");
            string written = Path.Combine(this.folder, "roster.test-design.json");

            _ = Run(() => DeriveCommand.Run(ruleSet, Plugins, null, null, null, new WalkSettings(Budget: 200)));
            byte[] first = File.ReadAllBytes(written);

            _ = Run(() => DeriveCommand.Run(ruleSet, Plugins, null, null, null, new WalkSettings(Budget: 200)));

            Assert.Equal(first, File.ReadAllBytes(written));
        }

        // The whole of what a person writes, and the whole of what happens to it. One choice
        // that can be taken, one that names an input the rules do not have: the first is
        // taken, the second is named on the way out, and the exit code says so.
        [Fact]
        public void AChoiceThatCouldNotBeCarriedIsNamedAndChangesTheExitCode()
        {
            string ruleSet = Copy("approval");
            string written = Path.Combine(this.folder, "approval.test-design.json");

            _ = Run(() => DeriveCommand.Run(ruleSet, Plugins, null, null, null, new WalkSettings(Budget: 3000)));
            Choose(written, ("#0", "submit"), ("#0", "nonesuch"));

            (int code, string said) = Run(() => DeriveCommand.Run(
                ruleSet, Plugins, null, null, null, new WalkSettings(Budget: 3000)));

            Assert.Equal(3, code);
            Assert.Contains("1 of 2 choices carried", said, StringComparison.Ordinal);
            Assert.Contains("nonesuch is not legal in #0", said, StringComparison.Ordinal);

            // And the choice that was taken is still in the file, so the next run has it.
            Assert.Equal(2, TestDesign.FromJson(File.ReadAllText(written)).Edits.Count);
        }

        [Fact]
        public void ADesignAppliedToTheVersionItCameFromSaysNothingAndExitsZero()
        {
            string ruleSet = Copy("approval");
            string written = Path.Combine(this.folder, "approval.test-design.json");
            _ = Run(() => DeriveCommand.Run(ruleSet, Plugins, null, null, null, new WalkSettings(Budget: 3000)));

            (int code, string said) = Run(() => DiffCommand.Run(written, ruleSet, Plugins, null, write: false));

            Assert.Equal(0, code);
            Assert.Contains("4 states in both, 0 decided differently", said, StringComparison.Ordinal);
        }

        // The exit code is the verification a build server reads, and the change is named
        // rather than counted.
        [Fact]
        public void ADesignAppliedToALaterVersionNamesWhatMovedAndExitsThree()
        {
            string ruleSet = Copy("approval");
            string later = Copy("approval-results");
            string written = Path.Combine(this.folder, "approval.test-design.json");
            _ = Run(() => DeriveCommand.Run(ruleSet, Plugins, null, null, null, new WalkSettings(Budget: 3000)));

            (int code, string said) = Run(() => DiffCommand.Run(written, later, Plugins, null, write: false));

            Assert.Equal(3, code);
            Assert.Contains("approval@1.0.0 -> approval@1.1.0", said, StringComparison.Ordinal);
            Assert.Contains("4 states in both, 2 decided differently", said, StringComparison.Ordinal);
            Assert.Contains("final passed, was final approved", said, StringComparison.Ordinal);
        }

        // Where a change moves which fields a position is compared on, the two walks rejoined
        // in different places, and part of the diff below is that rather than the rules. The
        // tool says so before the lines it changes the reading of, and it counts: here the
        // diff itself is empty, and exiting 0 on it would be a build going green because
        // nobody read the one line that mattered.
        [Fact]
        public void ADiffComparedOnDifferentStateSaysSo()
        {
            string ruleSet = Copy("reversi");
            string counted = Copy("reversi-counted");
            string written = Path.Combine(this.folder, "reversi.test-design.json");
            _ = Run(() => DeriveCommand.Run(ruleSet, Plugins, null, null, null, new WalkSettings(Budget: 200)));

            (int code, string said) = Run(() => DiffCommand.Run(written, counted, Plugins, null, write: false));

            Assert.Contains("200 states in both, 0 decided differently", said, StringComparison.Ordinal);
            Assert.Contains("the two are not compared on the same state:", said, StringComparison.Ordinal);
            Assert.Contains("now compared on board, turn, passes, plies", said, StringComparison.Ordinal);
            Assert.Equal(3, code);
        }

        [Fact]
        public void DiffAmendsTheTestDesignWhenItIsAskedTo()
        {
            string ruleSet = Copy("approval");
            string later = Copy("approval-results");
            string written = Path.Combine(this.folder, "approval.test-design.json");
            _ = Run(() => DeriveCommand.Run(ruleSet, Plugins, null, null, null, new WalkSettings(Budget: 3000)));

            _ = Run(() => DiffCommand.Run(written, later, Plugins, null, write: true));

            Assert.Equal("approval@1.1.0", TestDesign.FromJson(File.ReadAllText(written)).RuleSet);
        }

        // Reading the document is the whole of this one, so it answers about a rule set whose
        // vocabularies are nowhere to be had — which is the only thing that can be said about
        // `deploy`, whose plugins are not published.
        [Fact]
        public void ObserveAnswersWithoutAnyVocabularyAtAll()
        {
            string ruleSet = Copy("deploy");

            (int code, string said) = Run(() => ObserveCommand.Run(ruleSet, null));

            Assert.Equal(0, code);
            Assert.Contains("compared on: target, today, services, stages, building, deployed, approvals",
                said, StringComparison.Ordinal);
            Assert.Contains("dropped:     log", said, StringComparison.Ordinal);
        }

        // A composite is walked as the one rule set it stands for, so the documents it holds
        // are found beside it.
        [Fact]
        public void ACompositeIsWalkedWithTheDocumentsItHolds()
        {
            _ = Copy("seats");
            string ruleSet = Copy("seating");

            (int code, string said) = Run(() => DeriveCommand.Run(
                ruleSet, Plugins, null, null, null, new WalkSettings(Budget: 3000)));

            Assert.Equal(0, code);
            Assert.Contains("7 states", said, StringComparison.Ordinal);
            Assert.Contains("compared on s.left, s.right — s.note dropped", said, StringComparison.Ordinal);
        }

        // And a composite whose components are not there is refused. Answering would mean
        // returning a set of legal inputs with some of them missing, and nothing in the
        // answer would say any were.
        [Fact]
        public void ACompositeWithoutItsComponentsIsRefused()
        {
            string ruleSet = Copy("seating");

            (int code, string said) = Run(() => ObserveCommand.Run(ruleSet, null));

            Assert.Equal(1, code);
            Assert.Contains("holds rule sets that are in neither", said, StringComparison.Ordinal);
            Assert.Contains("seats", said, StringComparison.Ordinal);
        }

        private static string Plugins => AppContext.BaseDirectory;

        // Standard error is read along with standard output: what a command refuses to do is
        // as much of its interface as what it does.
        private static (int Code, string Said) Run(Func<int> command)
        {
            TextWriter wasOut = Console.Out;
            TextWriter wasError = Console.Error;
            StringWriter said = new();

            try
            {
                Console.SetOut(said);
                Console.SetError(said);
                return (command(), said.ToString());
            }
            finally
            {
                Console.SetOut(wasOut);
                Console.SetError(wasError);
            }
        }

        // A person writes choices into the test design by hand, which is the one place in it
        // a person writes at all. Written here the way they would be: a state, an input, and
        // nothing about whether it was carried, because that is the tool's half of it.
        private static void Choose(string testDesign, params (string State, string Input)[] choices)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(testDesign));
            using MemoryStream buffer = new();

            using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (!property.NameEquals("edits"))
                    {
                        property.WriteTo(writer);
                        continue;
                    }

                    writer.WriteStartArray("edits");
                    foreach ((string state, string input) in choices)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("state", state);
                        writer.WriteString("input", input);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            File.WriteAllBytes(testDesign, buffer.ToArray());
        }

        private string Copy(string ruleSet)
        {
            string path = Path.Combine(this.folder, ruleSet + ".json");
            File.WriteAllText(path, Vocabulary.Read(ruleSet));
            return path;
        }
    }
}
