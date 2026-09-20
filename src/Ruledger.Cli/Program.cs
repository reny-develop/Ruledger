// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text;
using Ruledger;
using Ruledger.Cli;

// Derives a test design from a Rulealize rule set, and applies one to the next version of it.
//
//   ruledger derive  <rule set>                 walk it, and write the test design down
//   ruledger diff    <test design> <rule set>   apply one to a version of the rules
//   ruledger observe <rule set>                 what a position is compared on
//
// Nothing here fetches. The vocabularies a rule set draws on come from a folder, which is
// what `rulealize restore <rule set>` fills, and the rule sets a composite holds come from
// the folder that composite sits in or from the one restore fetched into. Two tools writing
// one folder is a folder neither can account for, so this one only reads.
//
// The three settings a walk has are the three flags below it, and they are the whole of what
// is not derived from the rule set: how many states the walk may visit, how many candidates
// the runtime may try in one state, and how many outcomes it may return for one input. They
// travel in the test design, which is why `diff` does not take them — it walks the new
// version the way the test design in front of it was walked, and refuses a flag that would
// make the two incomparable.

// A rule set names its inputs and its states in whatever language it was written in, and
// what comes back out is those names: a card is '10♠', a day is '月'. The console's own code
// page turns both into question marks, which would make the one thing a person matches
// against their rule set unmatchable.
try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // No console attached, which is the case whenever this is piped into something. What is
    // written then is already UTF-8, so there is nothing to set and nothing to report.
}

const int DefaultBudget = 3_000;
const int DefaultValidationLimit = 10_000;
const int DefaultOutcomeLimit = 64;

if (args.Length is 0)
{
    return Usage();
}

string plugins = Option("--plugins") ?? PluginFolder.Default;

// Passed on as null rather than defaulted here, because the folder a document sits in is
// read as well and only Held knows both — and a rule set that holds nothing reads neither.
string? ruleSets = Option("--rulesets");
string? outPath = Option("--out");
string? fromPath = Option("--from");
bool write = args.Contains("--write");

int budget = DefaultBudget;
int validationLimit = DefaultValidationLimit;
int outcomeLimit = DefaultOutcomeLimit;

if (!Positive("--budget", ref budget)
    || !Positive("--limit", ref validationLimit)
    || !Positive("--outcomes", ref outcomeLimit))
{
    return Exit.Misread;
}

return args switch
{
    ["derive", string ruleSet, ..] => DeriveCommand.Run(
        ruleSet, plugins, ruleSets, outPath, fromPath,
        new WalkSettings(budget, validationLimit, outcomeLimit)),

    // A walk told to go a different distance visits different states, and none of that is
    // the rule set deciding differently. The settings are in the test design being applied;
    // a flag that would override them is refused rather than ignored, because a flag nobody
    // reads is a flag somebody wrote for a reason.
    ["diff", ..] when Walked() is string flag => Refuse(flag),
    ["diff", string testDesign, string ruleSet, ..] when !ruleSet.StartsWith("--", StringComparison.Ordinal) =>
        DiffCommand.Run(testDesign, ruleSet, plugins, ruleSets, write),

    ["observe", string ruleSet, ..] => ObserveCommand.Run(ruleSet, ruleSets),
    _ => Usage(),
};

string? Walked() =>
    new[] { "--budget", "--limit", "--outcomes" }.FirstOrDefault(flag => args.Contains(flag));

static int Refuse(string flag)
{
    Console.Error.WriteLine($"{flag} says how a walk goes, and 'diff' takes that from the test design it");
    Console.Error.WriteLine("was given: both versions have to be walked the same way for a difference between");
    Console.Error.WriteLine("them to be the rules' doing. Derive again with the setting you want, and diff that.");
    return Exit.Misread;
}

bool Positive(string name, ref int value)
{
    if (Option(name) is not string text)
    {
        return true;
    }

    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
    {
        Console.Error.WriteLine($"{name} takes a positive whole number, not '{text}'.");
        return false;
    }

    value = parsed;
    return true;
}

string? Option(string name)
{
    int at = Array.IndexOf(args, name);
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}

static int Usage()
{
    Console.Error.WriteLine("usage:");
    Console.Error.WriteLine("  ruledger derive  <rule set>");
    Console.Error.WriteLine("  ruledger diff    <test design> <rule set>");
    Console.Error.WriteLine("  ruledger observe <rule set>");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  --plugins <folder>   where the vocabularies are        (default 'plugin')");
    Console.Error.WriteLine("  --rulesets <folder>  where fetched components are read (default 'component')");
    Console.Error.WriteLine("  --out <file>         where derive writes    (default beside the rule set)");
    Console.Error.WriteLine("  --from <file>        the test design to carry the choices of (default --out)");
    Console.Error.WriteLine("  --write              amend the test design diff was given");
    Console.Error.WriteLine("  --budget <n>         states the walk may visit          (default 3000)");
    Console.Error.WriteLine("  --limit <n>          candidates GetValidInputs may try  (default 10000)");
    Console.Error.WriteLine("  --outcomes <n>       outcomes GetOutcomes may return    (default 64)");
    Console.Error.WriteLine();
    Console.Error.WriteLine("exit: 0 nothing to report, 1 could not be done, 2 command line not understood,");
    Console.Error.WriteLine("      3 the rules decided differently, a choice could not be carried, or the two");
    Console.Error.WriteLine("        versions are not compared on the same state");
    Console.Error.WriteLine();
    Console.Error.WriteLine("'rulealize restore <rule set>' fills the folders this reads.");
    return Exit.Misread;
}
