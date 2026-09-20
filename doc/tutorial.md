# Walking a rule set

Written for: a developer who can read a Rulealize rule set, and who wants to find out what
deriving a test design from one is actually like. Half an hour, two commands to install,
no host program and no test cases.

**Being able to read a rule set is where this starts, not who it is for.** The runtime is
new, so almost nobody can read one yet. The method is aimed at whoever holds the
requirements; this rung of it is where the tools are today.

At the end there is a count of what did not happen. That is the part worth reading twice.

## What you need

The .NET SDK, and two tools.

```sh
dotnet tool install -g Rulealize.Cli
dotnet tool install -g Ruledger.Cli
```

`rulealize` runs a rule set. `ruledger` derives a test design from one. They share a folder
and nothing else: `rulealize restore` fetches the vocabularies a rule set draws on into
`plugin/`, and `ruledger` reads that folder.

## A rule set, in three commands

Work in an empty directory.

```sh
curl -O https://raw.githubusercontent.com/reny-develop/Ruledger/main/verify/ruleset/reversi.json
```

```
$ rulealize restore reversi.json
  Rulealize.Plugin.Arithmetic 1.0.0
  Rulealize.Plugin.Binding 1.0.0
  Rulealize.Plugin.Branch 1.0.0
  Rulealize.Plugin.Comparison 1.0.0
  Rulealize.Plugin.Definition 1.0.0
  Rulealize.Plugin.Grid 1.1.0
  Rulealize.Plugin.Logic 1.0.0
  Rulealize.Plugin.Sequence 1.2.0
  Rulealize.Plugin.State 1.0.0
  Rulealize.Plugin.TypeSchema 1.1.0
10 plugins -> plugin
'reversi.json' compiles against it.
```

```
$ ruledger derive reversi.json
reversi@1.0.0
  3,000 states, 849 final: black 842, draw 5, white 2
  252 landings the budget stopped in front of
  compared on board, turn, passes
  budget 3,000, limit 10,000, outcomes 64
  -> reversi.test-design.json
```

Two seconds. Read the lines in order:

- **3,000 states** is the budget, not the game. Reversi has around 10²⁸ reachable positions;
  every walk of it stops somewhere, and where it stopped is recorded rather than glossed.
- **849 final** are the positions the rules call finished, by what they call the outcome.
- **252 landings the budget stopped in front of.** Not "252 things are missing" — nobody can
  say that. It is how many places the walk was standing in front of when it ran out. It is
  the only number of its kind Ruledger prints; there is no percentage and no coverage
  figure, because a number standing in for quality is the thing this method was written
  against.
- **compared on board, turn, passes.** Ruledger worked out from the document which state two
  positions have to agree on to be the same position. Nobody declared it. `ruledger observe`
  asks the same question on its own.
- **budget, limit, outcomes.** The three settings, recorded in the test design, because none
  of them comes from the rule set and two designs are only comparable when they match.

## What came out

`reversi.test-design.json` is the test design. It opens like this:

```json
{
  "$schema": "ruledger/test-design/v1",
  "ruleSet": "reversi@1.0.0",
  "settings": {
    "budget": 3000,
    "validationLimit": 10000,
    "outcomeLimit": 64
  },
  "observed": [
    "board",
    "turn",
    "passes"
  ],
  "collapsed": [],
  "unreached": 252,
  "edits": [],
  "states": [
    {
      "name": "#0",
      "state": {
        "$schema": "rulealize/state/v1",
        "ruleSet": "reversi@1.0.0",
        "data": {
          "board": {
            "d5": "black",
            "e5": "white",
            "d4": "white",
            "e4": "black"
          },
          "turn": "black",
          "passes": 0
        }
      },
      "evaluated": 65,
      "moves": [
        {
          "input": "place",
          "args": {
            "at": "e6"
          },
          "actor": "black",
          "to": "#1"
        },
        {
          "input": "place",
          "args": {
            "at": "f5"
          },
          "actor": "black",
          "to": null
        },
```

That is one state, and it is everything that can be observed about it:

| | |
|---|---|
| which inputs are legal | four `place`s, out of 65 candidates whose guard was evaluated |
| whether it is final, and with what result | no `terminal` key, so it is not |
| where each legal input leads | `"to": "#1"`, and `"to": null` for the three the walk never took |

Nothing else is written down, because nothing else can be checked. And there is nowhere in
this file to write an observation by hand — if there were, that would be the one thing in a
test design that could be wrong.

`"to": null` on three of the four opening moves is worth stopping at. The walk takes one
route to the end before coming back, so it spent the whole budget below `place(at: e6)` and
never got back up to `f5`. Reported as that, and never as those moves leading nowhere.

## Change one line

Open `reversi.json`, find the end of `terminal.result`, and swap which count wins:

```diff
-        "cases": { "gt": "black", "lt": "white", "eq": "draw" }
+        "cases": { "gt": "white", "lt": "black", "eq": "draw" }
```

Raise the version to `1.1.0` while you are there, and apply the test design you already have
to the rule set you now have:

```
$ ruledger diff reversi.test-design.json reversi.json
reversi@1.0.0 -> reversi@1.1.0
  3,000 states in both, 844 decided differently

  #60 = #59 + place(at: h1): final white, was final black
  #63 = #62 + place(at: g1): final white, was final black
  #66 = #65 + place(at: g1): final white, was final black
  #68 = #67 + place(at: f1): final white, was final black
  #73 = #72 + place(at: g1): final white, was final black
```

Every state is still there. Not one legal input moved. 844 endings say the other name, and
that is exactly the line you changed. **The diff is the size of the change.**

Now the other end of the same rule set. Put `reversi.json` back, derive again, and this time
require a placement to flip two discs instead of one:

```diff
-          { "op": "seq.any", "source": { "op": "def.call", "def": "flips", "args": { "at": "@at" } } }
+          { "op": "cmp.gte", "right": 2,
+            "left": { "op": "seq.count",
+                      "source": { "op": "def.call", "def": "flips", "args": { "at": "@at" } } } }
```

```
$ ruledger diff reversi.test-design.json reversi.json
reversi@1.0.0 -> reversi@1.2.0
  1 state in both, 1 decided differently
  2,999 only in the test design, 2 only in this walk

  #0: lost place(at: e6), place(at: f5), place(at: c4), place(at: d3); gained pass

  #1 = #0 + place(at: e6): not walked any more
  #2 = #1 + place(at: d6): not walked any more
```

One line, and the game does not start: every opening move of reversi flips exactly one disc,
so the opening position has nothing left to place and `pass` is all there is.

**Nobody had to remember that `pass` existed.** It is not in the change, it is not in any
test somebody wrote, and it arrived in the diff anyway, because the diff is derived from the
rules rather than from a list of cases a person kept up to date.

## What did not happen

Between the two changes above:

| | |
|---|---|
| test cases written | 0 |
| test data set up | 0 |
| expected values worked out | 0 |
| blast radius estimated by hand | 0 |
| host program written | 0 lines |
| the `pass` ripple | arrived without being remembered |

That is the whole claim, and it is the reason the method exists. What a test design costs is
reading the diff.

## The business side

A board game is easy to dismiss. Do it again with a shift roster.

```sh
curl -O https://raw.githubusercontent.com/reny-develop/Ruledger/main/verify/ruleset/roster.json
rulealize restore roster.json
```

```
$ ruledger derive roster.json
roster@1.0.0
  3,000 states, 998 final: complete 997, stuck 1
  16,424 landings the budget stopped in front of
  compared on staff, shifts, assigned — log dropped
  budget 3,000, limit 10,000, outcomes 64
  -> roster.test-design.json
```

`log dropped` is Ruledger having worked out that this rule set keeps an audit trail nothing
observable reads. Two positions are the same position whatever the trail says. Without that,
a walk of a rule set that keeps a history never comes back to a position it has been in —
measured, in this repository: 2,052 rejoins with the trail dropped, 0 without, and one of
this rule set's two endings never reached inside the budget.

### Writing the one thing a person writes

The machine picked `assign(who: ann, shift: mon-am)` to go down first. Suppose you want to
look at what happens when `cy` takes Friday morning instead. That is a choice, and it is the
only kind of thing there is to write. Put it in `edits`:

```json
  "edits": [
    { "state": "#0", "input": "assign", "args": { "who": "cy", "shift": "fri-am" } }
  ],
```

and derive again:

```
$ ruledger derive roster.json
roster@1.0.0
  3,000 states, 998 final: complete 997, stuck 1
  16,465 landings the budget stopped in front of
  compared on staff, shifts, assigned — log dropped
  budget 3,000, limit 10,000, outcomes 64
  choices from 'roster.test-design.json'
  1 choice, all carried
  -> roster.test-design.json
```

`#1` is now `#0 + assign(who: cy, shift: fri-am)`, and everything below it was worked out
again from the rules. You changed which route the walk takes first. You did not write down
one thing about what it found there.

### One clause, and half the staff

Now change the guard so that only senior staff may be placed at all, where before that was
asked only of the shifts marked senior — delete the `logic.or` around it and keep the inner
test:

```diff
           {
-            "op": "logic.or",
-            "any": [
-              {
-                "op": "logic.not",
-                "value": {
-                  "op": "rec.at", "key": "senior",
-                  "record": { "op": "def.call", "def": "shift", "args": { "id": "@id" } }
-                }
-              },
-              {
-                "op": "rec.at", "key": "senior",
-                "record": { "op": "def.call", "def": "person", "args": { "who": "@who" } }
-              }
-            ]
+            "op": "rec.at", "key": "senior",
+            "record": { "op": "def.call", "def": "person", "args": { "who": "@who" } }
           },
```

```
$ ruledger diff roster.test-design.json roster.json
roster@1.0.0 -> roster@1.1.0
  5 states in both, 5 decided differently
  2,995 only in the test design, 2,995 only in this walk

  #0: lost assign(who: bo, shift: mon-pm), assign(who: bo, shift: tue-am), ...
```

Half the staff can no longer be placed anywhere, so almost nothing the old design described
is still there. That is proportional too: the change was large.

The last line of that run is the one to look at:

```
  1 choice, all carried
```

The state space moved out from under it and the choice survived, because a choice is held by
how the walk arrives at a state — `#0` is still `#0` — and not by what is in the state. Add a
field to every state in a rule set and every choice still holds; that is measured here too,
on reversi, with twenty of them.

## When a choice cannot be carried

`cy` is senior, so that choice survived a change that took half the staff off the board.
One that does not is easy to arrange: put twenty choices in the design, the first of them
placing `di` — who is not senior — and make the same change. The tail of the run:

```
  0 of 20 choices carried
    assign(who: di, shift: fri-pm) is not legal in #0
    release(shift: fri-pm) was not chosen: #0 + assign(who: di, shift: fri-pm) was not reached inside the budget
    release(shift: fri-pm) was not chosen: #0 + assign(who: di, shift: fri-pm) + assign(who: ann, shift: mon-am) was not reached inside the budget
```

Either the state is still there and the input is not legal in it any more, or the walk did
not get that far this time. Both are printed, every one of them, and the exit code changes.

One choice fell — the first — and the other nineteen went with it, because they were made
below it and the walk does not go that way any more. Their states are named by a route that
starts with the move that is now illegal, so there is no such state to reach. That is what
the nineteen "not reached" lines are: not nineteen separate problems, one problem and its
consequences, each said rather than summarised.

**There is no path in the tool that quietly falls back to the input the machine would have
picked.** A tool that reported only what it managed would be a tool that decided something
for you in silence.

## Exit codes

`diff` is meant to run on a build server, so what it found is in the exit code.

| | |
|---|---|
| 0 | nothing to report |
| 1 | it could not be done — the rule set does not compile, a file is missing |
| 2 | the command line was not understood |
| 3 | the rules decided differently, a choice could not be carried, or the two versions are not compared on the same state |

Commit the test design. The next diff that comes back with something in it is the blast
radius of whatever changed since.

`ruledger diff --write` updates the test design in place once you have read what moved.

## Where these numbers come from

Every number in this file is printed by a command you just ran. Every number in the
repository's own documents comes out of

```sh
dotnet test verify/Ruledger.Verify.csproj --logger "console;verbosity=detailed"
```

which walks the measured rule sets, prints what it found, and fails if any of it has moved.
Nothing here is a figure somebody wrote down once, and the way to check one is to run it
yourself.
