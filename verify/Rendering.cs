// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Text.Json;

namespace Ruledger.Verify
{
    /// <summary>That the form does not block the line-oriented rendering v1 does not build.</summary>
    /// <remarks>
    /// <para>
    /// The test design is the document of record and a rendering of it is a view. v1 ships
    /// no view, so what has to be checked is only that one is still possible — and the way
    /// to check that is to write the smallest possible one here and see what it needs.
    /// </para>
    /// <para>
    /// It reads the JSON rather than the object model on purpose. A renderer written against
    /// <see cref="TestDesign"/> would prove that Ruledger can render its own walk; the
    /// question is whether the committed document carries enough, and the committed document
    /// is JSON.
    /// </para>
    /// </remarks>
    public class Rendering(Xunit.Abstractions.ITestOutputHelper said)
    {
        public static TheoryData<string> Rendered => ["approval", "reversi", "blackjack", "roster"];

        [Theory]
        [MemberData(nameof(Rendered))]
        public void ATestDesignFoldsIntoLinesWithNothingElseToHand(string ruleSet)
        {
            using JsonDocument document = JsonDocument.Parse(Fixture.Walk(ruleSet, 60).ToJson());
            JsonElement states = document.RootElement.GetProperty("states");

            // The one thing a line needs that is not inside the state it belongs to: where a
            // move leads is a name, and the reader has to know it is a name of something.
            HashSet<string> named =
            [
                .. states.EnumerateArray().Select(static state => state.GetProperty("name").GetString() ?? string.Empty),
            ];

            int blocks = 0;
            StringBuilder lines = new();

            foreach (JsonElement state in states.EnumerateArray())
            {
                blocks++;
                Block(state, named, lines);
            }

            said.WriteLine($"{ruleSet} at 60 states: {blocks} states folded into "
                + $"{lines.ToString().Split('\n').Length - 1} lines, in the order the document has them");
            said.WriteLine(string.Join('\n', lines.ToString().Split('\n').Take(7)));

            Assert.Equal(states.GetArrayLength(), blocks);
            Assert.NotEmpty(lines.ToString());
        }

        // Written out so that what a line needs is visible rather than asserted. Everything
        // below comes from the one state object being folded, in the order `states` already
        // has them, with no lookup outside it but resolving a landing's name.
        private static void Block(JsonElement state, HashSet<string> named, StringBuilder lines)
        {
            string name = state.GetProperty("name").GetString() ?? string.Empty;

            lines.Append(name);
            if (state.TryGetProperty("from", out JsonElement from))
            {
                lines.Append(" = ").Append(from.GetString())
                    .Append(" + ").Append(state.GetProperty("by").GetString());
            }

            lines.Append('\n');

            bool first = true;
            foreach (JsonElement move in state.GetProperty("moves").EnumerateArray())
            {
                lines.Append(first ? "    legal   " : "            ");
                first = false;
                lines.Append(Called(move));

                foreach (string landing in Leads(move, named))
                {
                    lines.Append(landing);
                }

                lines.Append('\n');
            }

            if (first)
            {
                lines.Append("    legal   (none)\n");
            }

            lines.Append("    final   ").Append(Final(state)).Append('\n');

            if (state.TryGetProperty("truncated", out JsonElement truncated) && truncated.GetBoolean())
            {
                lines.Append("            the search for legal inputs stopped at the limit\n");
            }

            lines.Append('\n');
        }

        private static string Called(JsonElement move)
        {
            string input = move.GetProperty("input").GetString() ?? string.Empty;

            if (!move.TryGetProperty("args", out JsonElement args))
            {
                return input;
            }

            return $"{input}({string.Join(", ", args.EnumerateObject().Select(static a => $"{a.Name}: {a.Value.GetString()}"))})";
        }

        // Four shapes and four meanings, and a renderer that folded any two of them together
        // would be a renderer that hid one of them.
        private static IEnumerable<string> Leads(JsonElement move, HashSet<string> named)
        {
            if (move.TryGetProperty("lands", out JsonElement lands))
            {
                foreach (JsonElement landing in lands.EnumerateArray())
                {
                    yield return $" {Drew(landing)}{landing.GetProperty("probability").GetDouble():0.####} "
                        + $"-> {Where(landing, named)}";
                }

                yield break;
            }

            yield return move.TryGetProperty("to", out _)
                ? $" -> {Where(move, named)}"
                : "   (final; not followed)";
        }

        private static string Drew(JsonElement landing) =>
            landing.TryGetProperty("drew", out JsonElement drew)
                ? string.Join(',', drew.GetProperty("draws").EnumerateArray().Select(static draw => draw.ToString())) + " "
                : string.Empty;

        private static string Where(JsonElement node, HashSet<string> named)
        {
            JsonElement to = node.GetProperty("to");

            if (to.ValueKind == JsonValueKind.Null)
            {
                return "(not reached)";
            }

            string name = to.GetString() ?? string.Empty;

            // The one cross-reference a rendering makes, and it has to land. A design naming
            // a state it does not have would be a design nothing could render honestly.
            Assert.Contains(name, named);
            return name;
        }

        private static string Final(JsonElement state)
        {
            if (!state.TryGetProperty("terminal", out JsonElement terminal))
            {
                return "no";
            }

            JsonElement result = terminal.GetProperty("result");
            return result.ValueKind == JsonValueKind.Null ? "yes, no result" : $"yes, {result.GetString()}";
        }
    }
}
