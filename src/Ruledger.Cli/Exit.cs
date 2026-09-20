// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

namespace Ruledger.Cli
{
    /// <summary>What a command's exit code says, which is what a build server reads.</summary>
    /// <remarks>
    /// <see cref="Different"/> is the one worth spelling out. A diff that comes back with
    /// something in it is not a failure of the tool and not a fault in the rule set — it is
    /// the rules deciding something the committed test design does not say they decide, which
    /// is exactly what somebody wanted to be told about. It is kept apart from
    /// <see cref="Undone"/> so that a build that could not run the command and a build whose
    /// rules moved are not the same red.
    /// </remarks>
    internal static class Exit
    {
        /// <summary>Done, and there is nothing to report beyond the answer.</summary>
        public const int Done = 0;

        /// <summary>It could not be done, and why is on standard error.</summary>
        public const int Undone = 1;

        /// <summary>The command line was not understood.</summary>
        public const int Misread = 2;

        /// <summary>Done, and something is different: the rules moved, a choice could not be carried, or the two are not compared on the same state.</summary>
        public const int Different = 3;
    }
}
