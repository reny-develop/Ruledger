# `ruledger/test-design/v1`

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
  "$schema": "ruledger/test-design/v1",
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
| `$schema` | `ruledger/test-design/v1`, and a document without it is refused rather than guessed at |
| `ruleSet` | what the rule set calls itself, as `id@version` |
| `settings` | how the walk was told to go, each a count of the thing it limits: `states` to visit before stopping, `candidates` inputs to try in one state before giving up on finding more legal ones there, `outcomes` of one random draw to follow for an input. Recorded because none of it comes from the rule set, and two designs are only comparable when they agree |
| `observed` | the state paths two positions have to agree on to be the same position, in the order the rule set's `state.schema` declares them |
| `collapsed` | the state paths left out of that comparison because nothing observable reads them, in the same order |
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
| `evaluated` | how many candidates had their guard evaluated to find `moves` |
| `truncated` | present and `true` only when the search for legal inputs stopped at `candidates` |
| `terminal` | present only when the rules call this state final. It holds `result`, which may be `null` |
| `moves` | every input that is legal here, in the order the runtime offered them |

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
| `args` | what it was called with, by parameter name, each in the text form the runtime writes. Absent when the input takes none |
| `actor` | whose move it is. Absent where the rule set does not say |
| `to` / `lands` | where applying it goes, in one of the shapes below |

A legal input is its name, its arguments and whose it is, which is why `actor` is here and
why a field only `actor` reads is still among the `observed`.

### Where it goes

Four shapes, and they mean four different things.

| | |
|---|---|
| `"to": "#7"` | applying it settles the next state, and that state is `#7` |
| `"to": null` | the walk had no states left for it. **Not "it leads nowhere"** |
| `"lands": [ ... ]` | the rules draw, so one input arrives in more than one place |
| neither key | a move in a state the rules call final: offered, and not followed |

The last is the walk's decision and not the rule set's. What the rules still allow in a
finished position is theirs to say, and leaving the moves out would make stopping look like
something they decided.

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

## An edit

The one place a person writes.

```json
{ "state": "#0", "input": "assign", "args": { "who": "cy", "shift": "fri-am" }, "carried": "yes" }
```

| key | |
|---|---|
| `state` | the state to choose from, written either as how the walk arrives there — `#0 + submit` — or as the number that state has in the design being read |
| `input` | the name of the input to take first |
| `args` | what to call it with. Absent when the input takes none |
| `carried` | `yes`, `not legal here`, or `not reached`. Written by Ruledger; absent means `yes` |

Writing one by hand needs `state` and `input`, and nothing else. Ruledger writes `state`
back out the long way, because the number is a name inside one design and a choice has to
survive the rule set changing.

**An edit says which input to take first, and never what was found there.** Everything
downstream of a choice is worked out again from the rules. There is nowhere in this document
to write an observation, because an observation written by hand would be the only thing in a
test design that could be wrong.

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
