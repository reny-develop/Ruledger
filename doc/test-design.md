# `ruledger/test-design/v2`

Written for: someone reading or writing a test design by hand, or writing something that
reads one.

A test design is what Ruledger produces, what a person edits, and what gets committed. This
is its form. It is a fourth document beside the three Rulealize already reads, and it is
made of them: a state travels as a `rulealize/state/v1`, an input as the name and arguments
a `rulealize/input/v1` carries, a draw as what a `rulealize/outcome/v1` carries. Nothing is
transcribed into a shape of its own, so a person stepping through a design by hand and
Ruledger applying it again are reading the same thing.

Ruledger produces one by walking the rule set: from the state the rule set starts in, take
each legal input, carry on from where it lands, and write down every state not arrived at
before, until the number of states it was given have been written down. **The walk** below
means that, and `settings` holds that number.

## The whole of it

```json
{
  "$schema": "ruledger/test-design/v2",
  "ruleSet": "reversi@1.0.0",
  "settings": { "states": 3000, "candidates": 10000, "outcomes": 64 },
  "observed": ["board", "turn", "passes"],
  "collapsed": [],
  "unreached": 252,
  "edits": [],
  "states": [ ... ]
}
```

| key | |
|---|---|
| `$schema` | `ruledger/test-design/v2`, and a document without it is refused rather than guessed at. A `ruledger/test-design/v1` document is read as one: v2 adds a move that waits for a value, and a v1 document has none |
| `ruleSet` | what the rule set calls itself, as `id@version` |
| `settings` | how the walk was told to go, each a count of the thing it limits: `states` to visit before stopping, `candidates` inputs to try in one state before giving up on finding more legal ones there, `outcomes` of one random draw to follow for an input. Recorded because none of it comes from the rule set, and two designs are only comparable when they agree. Each of the three leaves a mark where it bit: `"to": null`, `truncated`, `followed` |
| `observed` | the state paths two positions have to agree on to be the same position, in the order the rule set's `state.schema` declares them |
| `collapsed` | the state paths left out of that comparison because nothing observable reads them, in the same order |
| `admits` | what each parameter the rule set leaves open admits, by input and parameter: the op of its schema and the bounds beside it, as the runtime answered them. Absent where nothing is left open; see below |
| `unreached` | how many landings the walk had no states left for: one per `"to": null` below |
| `edits` | the choices a person made, and what became of each |
| `states` | the states, in the order the walk first arrived at them |

`observed` and `collapsed` together are every field the state declares. They are worked out
from the rule set, not declared in it. The walk compares positions on `observed` alone, so
that it recognises a position it has already been in and writes it down once; comparing whole
states instead would make an audit trail enough to tell two identical positions apart, and a
rule set that keeps one would never be walked back into a position it had visited. Both lists
are written down because a diff has to know whether two designs were compared the same way.

`unreached` is the only quantity of its kind. It counts the times an input arrived somewhere
the walk had not been with no states left to spend, and there is no percentage, no coverage
figure and no number standing in for how much of a rule set a design covers.

## A state

```json
{
  "name": "#1",
  "from": "#0",
  "by": "place(at: e6)",
  "state": { "$schema": "rulealize/state/v1", "ruleSet": "reversi@1.0.0", "data": { ... } },
  "evaluated": 65,
  "moves": [ ... ]
}
```

| key | |
|---|---|
| `name` | `#0` for the state the rule set starts in, and the rest numbered as the walk first arrives. **A number names a state inside one design only** |
| `from` | the state this one was first reached from. Absent on `#0` |
| `by` | the input that first reached it, written `place(at: e6)`. Absent on `#0` |
| `state` | the position itself, as a `rulealize/state/v1` document |
| `evaluated` | how many candidates had their guard evaluated to find `moves`, and how many values were tried for a parameter left open |
| `truncated` | present and `true` only when the search for legal inputs stopped at `candidates` |
| `terminal` | present only when the rules call this state final. It holds `result`, which may be `null` |
| `moves` | every input that is legal here, in the order the runtime offered them |
| `refused` | the values for a parameter left open that were tried here and refused, each with the codes that refused it. Absent where none were |

`from` and `by` are what make a state's real name: following them back to `#0` spells out
how the walk arrived, and that is the name that still means this state after the rule set
changes. Adding a field to every state changes every state and no route to one.

Three things are observable about a state and all three are here: which inputs are legal
(`moves`), whether it is final and with what result (`terminal`), and where each legal input
leads (below). Nothing else is written down, because nothing else can be checked.

## A move

```json
{ "input": "place", "args": { "at": "e6" }, "actor": "black", "to": "#1" }
```

| key | |
|---|---|
| `input` | the name of the input |
| `args` | what it was called with, by parameter name, each in the text form the runtime writes — a value for a parameter left open included. Absent when the input takes none |
| `actor` | whose move it is. Absent where the rule set does not say |
| `open` | the parameters it is still waiting for, in declared order. Present only on a move the walk had no value for; see below |
| `to` / `lands` | where applying it goes, in one of the shapes below |
| `followed` | how much of the draw `lands` is. Present only when `outcomes` cut it short; absent means all of it |

A legal input is its name, its arguments and whose it is, which is why `actor` is here and
why a field only `actor` reads is still among the `observed`.

### Where it goes

Four shapes, and they mean four different things; a fifth, a move waiting for a value, is
below with the parameter it waits for.

| | |
|---|---|
| `"to": "#7"` | applying it settles the next state, and that state is `#7` |
| `"to": null` | the walk had no states left for it. **Not "it leads nowhere"** |
| `"lands": [ ... ]` | the rules draw, so one input arrives in more than one place |
| neither key | a move in a state the rules call final: offered, and not followed |

The last is the walk's decision and not the rule set's. What the rules still allow in a
finished position is theirs to say, and leaving the moves out would make stopping look like
something they decided.

### A parameter left open

A parameter with a `domain` arrives with its values enumerated, and each is a move of its own.
A parameter left `open` does not: its value comes from outside, and the runtime offers the move
with the value still to come. The walk does one of two things with it, and never guesses.

Where the schema it is open to admits few enough values to name — an enumeration, a boolean, a
whole number between two bounds — every one of them is tried, exactly as a domain's would be,
and each the rules take is a move with its value in `args`. What an input's `validate` refuses
is not legal, so it is not there.

Where the schema does not, as with text, the move is written down as legal and waiting, and not
followed:

```json
{ "input": "setName", "open": ["to"] }
```

It is followed only with a value a person wrote as a choice (below), and then it is there twice:
waiting, because the rules still take any other value, and with the value, going where that
value leads.

A value tried and refused is not a move, and it is not left out either. It goes beside the
moves, with what refused it:

```json
"refused": [ { "input": "setParty", "args": { "size": "1" }, "codes": ["party.unchanged"] } ]
```

A refusal is as much an answer as a move, and the one a later version can take back without
the list of moves showing it: relax the clause, and a value that was turned away is taken.

What each open parameter admits is written once, at the top, rather than in every state that
offers it — it is read off the schema, which the position does not change:

```json
"admits": { "setParty": { "size": { "op": "type.int", "min": 1, "max": 6 } } }
```

A move waiting for a value is one move whatever its schema allows, so this is where a bound
widened, a choice added or a text limit raised shows. The keys are the runtime's and are
carried, not read.

```json
{
  "input": "dealSeat",
  "lands": [
    {
      "probability": 0.07692307692307693,
      "drew": {
        "$schema": "rulealize/outcome/v1",
        "ruleSet": "blackjack@1.0.0",
        "input": "dealSeat",
        "draws": [
          "A"
        ]
      },
      "to": "#1"
    },
    {
      "probability": 0.07692307692307693,
      "drew": {
        "$schema": "rulealize/outcome/v1",
        "ruleSet": "blackjack@1.0.0",
        "input": "dealSeat",
        "draws": [
          "2"
        ]
      },
      "to": null
    }
  ]
}
```

One entry per branch, with what was drawn and how likely it was. `drew` is a
`rulealize/outcome/v1` document, and it is there because a draw is observable: two branches
of one input are told apart by nothing else, and a design that left it out would be naming
two different states the same.

```json
{ "input": "dealSeat", "followed": 0.23076923076923078, "lands": [ ... ] }
```

`followed` is the third thing a limit can do, beside `"to": null` for `states` and
`truncated` for `candidates`. Cutting a list of legal inputs short leaves a list of legal
inputs; cutting a draw short leaves a distribution that no longer sums to one, and the
branches that survive cannot say so between them. **Adding them up is not a way to find out
either**: thirteen thirteenths come to 0.9999999999999996, so a sum below one is as much what
floating point looks like as what a cut looks like. The runtime answers the question and the
answer is carried here, on the moves the runtime says were cut short and on no others.

**It is carried and not reported.** A share of something is the one shape Ruledger does not
print, because a number that says how much of a thing was got is the number this method was
written against, and the only quantity it prints is `unreached`, which is a count of places. A
diff between two versions that followed different amounts of a draw says so by their branches
differing, which is what moved the share in the first place.

## An edit

The one place a person writes.

```json
{ "state": "#0", "input": "assign", "args": { "who": "cy", "shift": "fri-am" }, "carried": "yes" }
```

| key | |
|---|---|
| `state` | the state to choose from, written either as how the walk arrives there — `#0 + submit` — or as the number that state has in the design being read |
| `input` | the name of the input to take first |
| `args` | what to call it with. Absent when the input takes none. For a parameter left open, the value to follow it with |
| `carried` | `yes`, `not legal here`, or `not reached`. Written by Ruledger; absent means `yes` |

Writing one by hand needs `state` and `input`, and nothing else. Ruledger writes `state`
back out the long way, because the number is a name inside one design and a choice has to
survive the rule set changing.

**An edit says which input to take first, and never what was found there.** Everything
downstream of a choice is worked out again from the rules. There is nowhere in this document
to write an observation, because an observation written by hand would be the only thing in a
test design that could be wrong.

**A choice is also how a value nobody could enumerate gets walked.** An edit that gives a
value for every parameter a move leaves open — `{ "state": "#0", "input": "setName", "args":
{ "to": "alice" } }` — is followed with that value, and first. It is still a choice and not an
observation: whether `alice` is a legal name is worked out from the rules, and a value the
rules refuse is `not legal here`.

**A choice that could not be taken stays in the file, with `carried` saying which of two
ways it went missing.** It is never dropped, and it never falls back to the input the
machine would have picked. The two ways are different enough to be worth telling apart: the
state is still there and the input is not legal in it any more, or the walk stopped before it
reached a state of that name.

## Reading it as lines

A test design is the document of record; a line-oriented rendering of it is a view. v1 does
not build one. What the form guarantees is that one is a fold over `states` and nothing else:

```
#0
    legal   place(at: e6) -> #1
            place(at: f5) -> (not reached)
            place(at: c4) -> (not reached)
            place(at: d3) -> (not reached)
    final   no
```

Every line above comes from one state object, in the order `states` already has them, with
no lookup outside it but resolving `to` to another state's `name`. Nothing has to be
recomputed and nothing has to be walked again.

That is the whole of what "the form does not block a rendering" means, and it is measured
rather than asserted: `verify/Rendering.cs` is the smallest such renderer, written against
the JSON rather than against Ruledger's own types, and it folds approval, reversi,
blackjack and roster with nothing else to hand. The block above is its output.

## Two designs

Ruledger compares two designs by route — `from` and `by` followed back — and never by what
is in `state`. Adding a field to every state moves every state and no route, so a diff by
content would report a change the rules did not make and would lose every choice a person
had made. This is measured, on a version of reversi that counts its own moves: all 3,000
states differ as documents, and all twenty choices survive.

Two designs walked with different `settings` are not compared at all. They visited different
states because they were told to, and none of that is the rule set deciding differently.

A state both designs have is **decided differently** when any of these moved: the three
observables, what was refused there, and the limit that can cut the first of them short:

| | |
|---|---|
| it lost a legal input | |
| it gained one | |
| a legal input lands somewhere else | where the rules draw, this is asked of each branch: one appearing or going, a different value drawn, the same branch at a different chance, or a different `followed` |
| its ending moved | `terminal` gained, lost, or a different `result` |
| its `truncated` moved | it started or stopped hitting `candidates`, so the two say different amounts about it |
| a refusal came, went, or changed its codes | a value that moved between refused and legal is already a legal input lost or gained, and is said once |

What an open parameter admits is compared as well, once per parameter rather than per state,
where both designs record it; one that moved is a difference like any of these.

A state only one of the two designs has is not in that count. It is reported on its own line
either way, never left as a number: the walk stopped before reaching it this time, or it
reached somewhere the earlier walk had not.

**Two designs can also disagree about `observed` itself**, and then the walks rejoined in
different places and some of what the diff says is that rather than the rules. Adding a field
that something observable reads is enough: the rules decide exactly what they decided, and
positions that used to be the same position are not any more. There is no telling the two
apart from inside the diff, so it says which it cannot tell apart, and the exit code is the
same as the rules moving — a build that went green on it would be a build that passed because
nobody was told.
