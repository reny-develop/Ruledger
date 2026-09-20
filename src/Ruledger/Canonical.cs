// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Text.Json;

namespace Ruledger
{
    /// <summary>JSON written out one way, so that two of them can be compared as text.</summary>
    /// <remarks>
    /// The keys of every object go in order, because whether two things are the same is not a
    /// question about the order a writer happened to use. Two documents that came out of the
    /// same runtime agree anyway; two that went through a file do not, and both are compared
    /// here.
    /// </remarks>
    internal static class Canonical
    {
        public static void Write(JsonElement value, StringBuilder into)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    into.Append('{');
                    bool written = false;
                    foreach (JsonProperty property in value.EnumerateObject().OrderBy(static p => p.Name, StringComparer.Ordinal))
                    {
                        Separate(into, ref written);
                        into.Append(property.Name).Append(':');
                        Write(property.Value, into);
                    }

                    into.Append('}');
                    return;

                case JsonValueKind.Array:
                    into.Append('[');
                    bool first = false;
                    foreach (JsonElement item in value.EnumerateArray())
                    {
                        Separate(into, ref first);
                        Write(item, into);
                    }

                    into.Append(']');
                    return;

                default:
                    into.Append(value.GetRawText());
                    return;
            }
        }

        // Between and not after: what this writes is read by a person where it lands in the
        // name of a state, and a comma with nothing behind it is the sort of thing a person
        // then has to work out the meaning of.
        private static void Separate(StringBuilder into, ref bool written)
        {
            if (written)
            {
                into.Append(',');
            }

            written = true;
        }

        public static string Of(JsonElement value)
        {
            StringBuilder written = new();
            Write(value, written);
            return written.ToString();
        }
    }
}
