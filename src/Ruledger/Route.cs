// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Text.Json;

namespace Ruledger
{
    /// <summary>How the walk arrived at a state, which is the name the state keeps across versions.</summary>
    /// <remarks>
    /// <para>
    /// The same-state question is asked twice and answered twice. Walking asks it of the state
    /// itself, because a rule set that keeps a history would otherwise never come back to a
    /// position it has been in. A diff and the choices carried across one ask it of the route:
    /// <c>#0 + submit + approve</c> is a place in the test design, and it is spelled the same after
    /// a field is added to the state, after a definition is renamed, after anything at all that
    /// leaves the way there walkable. Asking a diff by content instead would make one extra
    /// field in the state enough to lose every state and every choice a person made, with
    /// nothing having changed about what the rules decide.
    /// </para>
    /// <para>
    /// A route is held as a step away from another route rather than as text, because a walk
    /// that rejoins little is a walk whose routes are as long as the walk is: written out, one
    /// test design would cost the square of its own size. Two of them interned here share their
    /// numbering, so a state of one is the state of the other exactly when the numbers agree.
    /// </para>
    /// </remarks>
    internal sealed class Route
    {
        /// <summary>The route to the state a rule set starts in, which is no steps at all.</summary>
        public const int Opening = 0;

        private readonly Dictionary<(int From, string By), int> numbered = [];
        private readonly List<(int From, string By)> steps = [];

        /// <summary>Writes the step that arrives somewhere: the input, and what was drawn with it.</summary>
        /// <remarks>
        /// What was drawn belongs in the name. Where the rules draw, one input arrives at more
        /// than one state, and the input alone would name all of them the same.
        /// </remarks>
        public static string Step(string by, string? draw) =>
            draw is null ? by : by + " " + Drew(draw);

        /// <summary>Writes what was drawn, and nothing where nothing was.</summary>
        /// <remarks>
        /// The draws of the outcome document and nothing else of it: it also carries which
        /// rule set it came from, and that is the one thing that is different on purpose when
        /// two versions are held against each other.
        /// </remarks>
        public static string Drew(string? draw)
        {
            if (draw is null)
            {
                return string.Empty;
            }

            using JsonDocument drawn = JsonDocument.Parse(draw);
            return Canonical.Of(drawn.RootElement.GetProperty("draws"));
        }

        /// <summary>Numbers the route to every state of a test design, in the order the walk arrived.</summary>
        /// <returns>The route of each state, by its position in <see cref="TestDesign.States"/>.</returns>
        /// <exception cref="InvalidOperationException">A state says it was reached from somewhere the test design does not have.</exception>
        public int[] Of(TestDesign design)
        {
            Dictionary<string, int> found = new(StringComparer.Ordinal);
            int[] routes = new int[design.States.Count];

            for (int at = 0; at < design.States.Count; at++)
            {
                TestDesignState state = design.States[at];
                if (state.From is null)
                {
                    routes[at] = Opening;
                }
                else
                {
                    if (!found.TryGetValue(state.From, out int from))
                    {
                        throw new InvalidOperationException(
                            $"'{state.Name}' was reached from '{state.From}', which is not a state the walk had arrived at.");
                    }

                    if (state.By is null)
                    {
                        throw new InvalidOperationException(
                            $"'{state.Name}' says which state it was reached from and not by what input.");
                    }

                    routes[at] = Take(routes[from], Step(state.By, Arriving(design.States[from], state)));
                }

                found[state.Name] = at;
            }

            return routes;
        }

        /// <summary>Takes one step from a route, numbering it the same for every test design interned here.</summary>
        public int Take(int from, string by)
        {
            if (this.numbered.TryGetValue((from, by), out int route))
            {
                return route;
            }

            this.steps.Add((from, by));
            route = this.steps.Count;
            this.numbered[(from, by)] = route;
            return route;
        }

        /// <summary>Writes a route out, as <c>#0 + submit + approve</c>.</summary>
        public string Text(int route)
        {
            List<string> back = [];
            for (int at = route; at != Opening; at = this.steps[at - 1].From)
            {
                back.Add(this.steps[at - 1].By);
            }

            StringBuilder written = new("#0");
            for (int at = back.Count - 1; at >= 0; at--)
            {
                written.Append(" + ").Append(back[at]);
            }

            return written.ToString();
        }

        // The landing that first arrived is the one the state was named after: the first, in
        // the order the move's outcomes came back, that leads to this state.
        private static string? Arriving(TestDesignState from, TestDesignState state)
        {
            foreach (Move move in from.Moves)
            {
                if (!string.Equals(move.Text, state.By, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Landing landing in move.Landings)
                {
                    if (string.Equals(landing.To, state.Name, StringComparison.Ordinal))
                    {
                        return landing.Draw;
                    }
                }
            }

            throw new InvalidOperationException(
                $"'{state.Name}' was reached from '{from.Name}' by '{state.By}', which does not lead there.");
        }
    }
}
