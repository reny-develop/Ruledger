# Walking a rule set

Written for: a developer who can read a Rulealize rule set, and who wants to find out what
deriving a test design from one is actually like. Half an hour, two commands to install,
no host program and no test cases.

**Being able to read a rule set is where this starts, not who it is for.** The runtime is
new, so almost nobody can read one yet. The method is aimed at whoever holds the
requirements; this rung of it is where the tools are today.

At the end there is a count of what did not happen. That is the part worth reading twice.

This is the half hour, not the manual. [doc/test-design.md](test-design.md) is what every key
of the file means, and `ruledger --help` is every flag.

## What you need

The .NET SDK, and two tools.

```sh
dotnet tool install -g Rulealize.Cli
dotnet tool install -g Ruledger.Cli
```

`rulealize` runs a rule set. `ruledger` derives a test design from one. They share a folder
and nothing else: `rulealize restore` fetches the vocabularies a rule set draws on into
`plugin/`, and `ruledger` reads that folder. So `restore` comes first, once per rule set;
`ruledger` fetches nothing itself, and without the folder it says so and stops.

Work in an empty directory. Everything below runs there.

## A rule set, in three commands

```sh
curl -O https://raw.githubusercontent.com/reny-develop/Ruledger/main/verify/ruleset/reversi.json
```

```
$ rulealize restore reversi.json
  Rulealize.Plugin.Arithmetic 1.0.0
  Rulealize.Plugin.Binding 1.0.0
  ... six more ...
  Rulealize.Plugin.State 1.0.0
  Rulealize.Plugin.TypeSchema 1.1.0
10 plugins -> plugin
'reversi.json' compiles against it.
```

```
$ ruledger derive reversi.json
reversi@1.0.0
  3,000 states, 849 final: black 842, draw 5, white 2
  252 landings the walk had no states left for
  compared on board, turn, passes
  at most 3,000 states, 10,000 candidates in a state, 64 outcomes for an input
  -> reversi.test-design.json
```

Two seconds. What it just did: start in the state the rule set starts in, ask the runtime
which inputs are legal there, take the first of them and carry on from where it lands — down
one route to its end, then back up to the next input it had not taken — writing down every
state it had not seen before, and stopping once three thousand of them are written down.
**That is the walk**, and three thousand is the default of `--states`, which is how many it
may visit. The word "walk" is used below in exactly that sense.

Nothing in it is chosen at random and there is no seed: the same rule set walked with the same
settings visits the same states in the same order, so `#60` below is the same position on a
second run as on the first.

Read the lines in order:

- **3,000 states** is where the walk was told to stop, not where reversi runs out. The game
  has around 10²⁸ reachable positions; every walk of it stops somewhere, and where it
  stopped is recorded rather than glossed.
- **849 final**, out of those three thousand, are the positions the rules call finished,
  counted by the result the rules give each one: black 842, draw 5, white 2.
- **252 landings the walk had no states left for.** An input was applied, it arrived somewhere
  the walk had not been, and its three thousand states were spent — 252 times. Not "252 things
  are missing", which nobody can say. It counts landings and not states, so the same place can
  be stood in front of more than once. It is the only number of its kind Ruledger prints;
  there is no percentage and no coverage figure, because a number standing in for quality is
  the thing this method was written against.
- **compared on board, turn, passes.** The walk has to recognise a position it has already
  been in, or it would write that position down a second time and walk on from it again. Two
  positions count as the same one when they agree on `board`, `turn` and `passes` — which is
  everything reversi's state declares. Nobody listed those three: Ruledger read the rule set
  and found that all three are read by something observable. A field that nothing observable
  reads is left out of the comparison, and the line says so — which is what happens to the
  roster's audit trail further down. `ruledger observe` answers this one question and nothing
  else — what a position is compared on, and what was dropped from the comparison — and it is
  the one command that reads a rule set without running it, so it does not need `plugin/`.
- **at most 3,000 states, 10,000 candidates in a state, 64 outcomes for an input.** The three
  settings this walk ran with, each a count of the thing it limits, and none of them from the
  rule set: states to visit before stopping, candidate inputs to try in one state before
  giving up on finding more legal ones there, and — for an input whose next state the rules
  draw rather than settle — outcomes of that draw to follow. Neither of these two rule sets
  draws, so the third never bites here. They are written into the test design, because two
  designs are only comparable when they match: a state the walk never visited because it was
  told to stop is not the rules deciding differently. Pass `--states 5000` to change one.
  `diff` takes none of the three — it walks the new version the way the design in front of it
  was walked.

## What came out

`reversi.test-design.json` is the test design. It opens with what the report just said, written
down — the rule set, the three settings, what a position is compared on, the 252 — and an
empty `edits`. Then `states`, and the first of them is the opening position:

```json
    {
      "name": "#0",
      "state": {
        "$schema": "rulealize/state/v1",
        "ruleSet": "reversi@1.0.0",
        "data": {
          "board": { "d5": "black", "e5": "white", "d4": "white", "e4": "black" },
          "turn": "black",
          "passes": 0
        }
      },
      "evaluated": 65,
      "moves": [
        { "input": "place", "args": { "at": "e6" }, "actor": "black", "to": "#1" },
        { "input": "place", "args": { "at": "f5" }, "actor": "black", "to": null },
        { "input": "place", "args": { "at": "c4" }, "actor": "black", "to": null },
        { "input": "place", "args": { "at": "d3" }, "actor": "black", "to": null }
      ]
    },
```

That is one state — a move folded onto a line here, indented over several in the file — and it
is everything that can be observed about it:

| what is observable | where it is |
|---|---|
| which inputs are legal | four `place`s, out of 65 candidates whose guard was evaluated — `place` on each of the board's 64 squares, and `pass` |
| whether it is final, and with what result | no `terminal` key, so it is not |
| where each legal input leads | `"to": "#1"`, and `"to": null` for the three the walk never took |

Nothing else is written down, because nothing else can be checked. And there is nowhere in
this file to write an observation by hand — if there were, that would be the one thing in a
test design that could be wrong.

`"to": null` on three of the four opening moves is worth stopping at. The walk takes one
route to the end before coming back, so it spent all three thousand states below
`place(at: e6)` and never got back up to `f5`. Reported as that, and never as those moves
leading nowhere.

Every state below `#0` also carries `from` and `by` — which state it was first reached from
and by which input. Following those back spells out how the walk arrived, and that is the name
a state keeps when the rule set changes. **A number names a state inside one design only.**

Which is what makes the likeliest change to a rule set survivable. Add a field that something
observable reads — a counter of the moves made, say — and the rules decide exactly what they
decided before, but the field joins `compared on`, positions that used to be the same position
are not any more, and the walk rejoins elsewhere. Numbers move; no route does. So the diff of
that change still reads, and a choice a person made still names the state it named — what it
can lose is the walk reaching that state, which is the same thing a smaller `--states` would
cost and is reported the same way. And where the diff carries a line the rules did not put
there, it says above them that the two were not compared on the same state, rather than
letting that pass as the rules moving.

## Change one line

Open `reversi.json`, find the end of `terminal.result`, and swap which count wins:

```diff
-        "cases": { "gt": "black", "lt": "white", "eq": "draw" }
+        "cases": { "gt": "white", "lt": "black", "eq": "draw" }
```

Raise the version to `1.1.0` while you are there — nothing makes you, but the first line of a
diff is those two names. Then apply the test design you already have to the rule set you now
have:

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

`#60 = #59 + place(at: h1)` is that route being spelled out, because the number means nothing
in the other design. **Decided differently** is a state both designs have that disagree about
something observable in it — here, 844 times, the ending.

Now the other end of the same rule set. Put the result line back the way it was — the test
design is untouched, because `diff` without `--write` only reads — and this time change
`definitions.canPlace`, so that a placement has to flip two discs instead of one. The version
is the third the rule set has had, so raise it to `1.2.0`:

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
so the opening position has nothing left to place and `pass` is all there is. The 2,999 and
the 2 each get a line of their own, further down than the page has room for. Neither side is
ever left as a count.

**Nobody had to remember that `pass` existed.** It is not in the change, it is not in any
test somebody wrote, and it arrived in the diff anyway, because the diff is derived from the
rules rather than from a list of cases a person kept up to date.

## What did not happen

Between the two changes above:

| what it took | how much |
|---|---|
| test cases written | 0 |
| test data set up | 0 |
| expected values worked out | 0 |
| blast radius estimated by hand | 0 |
| host program written | 0 lines |
| the `pass` ripple | arrived without being remembered |

The fifth row is the one that needs saying out loud. Applying inputs to a rule set, reading
back what is legal and following where each one goes is what a person would otherwise write a
program to do, and no such program was written here: the two installed tools did it, and there
is no project to open.

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
  16,424 landings the walk had no states left for
  compared on staff, shifts, assigned — log dropped
  at most 3,000 states, 10,000 candidates in a state, 64 outcomes for an input
  -> roster.test-design.json
```

`log dropped` is the same question as `compared on`, answered the other way. This state
declares four fields, and `log` is an audit trail: nothing observable reads it, so it is left
out when two positions are compared, and two positions are the same position whatever the
trail behind them says. Without that they never would be — the trail is different every time,
so a walk that compared it would never once come back to a position it had been in, and would
spend its three thousand states going down one corridor.

### Writing the one thing a person writes

The machine picked `assign(who: ann, shift: mon-am)` to go down first. Suppose you want to
look at what happens when `cy` takes Friday morning instead. That is a choice, and it is the
only kind of thing there is to write. Put it in `edits`:

```json
  "edits": [
    { "state": "#0", "input": "assign", "args": { "who": "cy", "shift": "fri-am" } }
  ],
```

and derive again. `derive` reads the file it is about to write, and takes the `edits` out of
it and nothing else: what that file says about the states is what the previous rule set
decided. Re-deriving after a change to the rules is meant to be the ordinary thing to do.

```
$ ruledger derive roster.json
roster@1.0.0
  3,000 states, 998 final: complete 997, stuck 1
  16,465 landings the walk had no states left for
  compared on staff, shifts, assigned — log dropped
  at most 3,000 states, 10,000 candidates in a state, 64 outcomes for an input
  choices from 'roster.test-design.json'
  1 choice, all carried
  -> roster.test-design.json
```

`#1` is now `#0 + assign(who: cy, shift: fri-am)`, and everything below it was worked out
again from the rules. You changed which route the walk takes first. You did not write down
one thing about what it found there.

Which is also the whole of why 16,424 became 16,465. A different first route means a different
three thousand states fit, so the walk ends up standing in front of a different number of
places it has nothing left for. The endings did not move, and nothing about the rules did.

### One clause, and half the staff

Now change `definitions.mayWork` so that only senior staff may be placed at all, where before
that was asked only of the shifts marked senior — delete the `logic.or` around it and keep the
inner test, and raise the roster's version to `1.1.0`:

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

A `diff` carries the choices as well as the settings, both so that what comes back is the
rules' doing and not the walk's. The state space moved out from under this one and it survived
anyway, because a choice is held by how the walk arrives at a state — `#0` is still `#0` — and
not by what is in the state — which is why the field added further up costs nothing either.

## When a choice cannot be carried

`cy` is senior, so that choice survived a change that took half the staff off the board. One
that does not is easy to arrange: twenty choices, each made from the design the ones before it
produced, the first of them placing `di` — who is not senior — and then the same change. It is
the command you have already run, with nothing different on disk but `edits`. Swapping the
choices changes which route the new walk takes, so what comes back differs from its second
line on and not only in its tail:

```
$ ruledger diff roster.test-design.json roster.json
roster@1.0.0 -> roster@1.1.0
  1 state in both, 1 decided differently
...
  0 of 20 choices carried
    assign(who: di, shift: fri-pm) is not legal in #0
    release(shift: fri-pm) was not chosen: #0 + assign(who: di, shift: fri-pm) was not reached before the walk stopped
    release(shift: fri-pm) was not chosen: #0 + assign(who: di, shift: fri-pm) + assign(who: ann, shift: mon-am) was not reached before the walk stopped
```

Either the state is still there and the input is not legal in it any more, or the walk did
not get that far this time. Both are printed, every one of them, and the command exits `3`.

One choice fell — the first — and the other nineteen went with it, because they were made
below it and the walk does not go that way any more. Their states are named by a route that
starts with the move that is now illegal, so there is no such state to reach. That is what
the nineteen "not reached" lines are: not nineteen separate problems, one problem and its
consequences, each said rather than summarised.

**There is no path in the tool that quietly falls back to the input the machine would have
picked.** A tool that reported only what it managed would be a tool that decided something
for you in silence.

## What it is for

Commit the test design. The next diff that comes back with something in it is the blast
radius of whatever changed since, and `ruledger diff --write` updates the design in place once
you have read what moved. What `diff` found is in its exit code — `0` when there is nothing to
report and `3` when there is, whether that is the rules deciding differently or a choice that
could not be carried — so the check belongs wherever the rule set is built. `ruledger --help`
has all four codes.

That is the half of it v1 ships. The other half is that the file is already material for a
test runner: a state travels in it as a `rulealize/state/v1` document and a legal input as the
name and arguments a `rulealize/input/v1` carries, so the concrete values and what the rules
answer for them are both there, and nothing has to be walked again to get at them. **v1 ships
no converter to a particular runner**, and no `ruledger` subcommand touches an implementation.
There are three of them, and `derive`, `diff` and `observe` are all three.

Every number Ruledger says on this page was printed by the command above it, or is in the file
that command wrote. There is nothing here to take on trust, which is what a test design is
for as well.
