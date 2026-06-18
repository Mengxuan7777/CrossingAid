"""
generate_configs.py (v3 — 6-trial design, no Safe/Unsafe factor)
------------------------------------------------------------------
Factors:
  Assistance level     : Unassisted, FullyAssisted        (2)
  Distraction type     : None, TextReading->Conversation  (2)
                          (paired: TextReading is always immediately
                           followed by its Conversation trial)

4 units total:
  Unit 0: None,                       Unassisted     -> 1 trial
  Unit 1: None,                       FullyAssisted  -> 1 trial
  Unit 2: TextReading->Conversation,  Unassisted     -> 2 trials (pair_A)
  Unit 3: TextReading->Conversation,  FullyAssisted  -> 2 trials (pair_B)

Trials per participant: 1 + 1 + 2 + 2 = 6

Counterbalancing: 4x4 Williams balanced Latin square -> 4 orderings -> 4 configs.
Each unit appears in every ordinal position exactly once.

Traffic signal:
  Each trial has `signalSecondsRemaining` -- how many seconds are left on the
  player's "walk" signal when the trial starts, before it changes. Defaults
  to 8.0; edit per trial as needed for the experiment design.

TextReading messages:
  `distractionTexts` is a list of up to 3 messages, shown one at a time
  (each for textDisplayDuration seconds, with a textDisplayInterval gap
  between them -- configured in TrialSequencer's Inspector).

Usage:
    python generate_configs.py

Output: trial_config_P001.json ... trial_config_P004.json (same folder as this script)
"""

import json, copy, os

# ── Texts and questions (one per pair) ────────────────────────────────────────

PAIRS = {
    "pair_A": {
        "texts": [
            "Meeting with Claire from 10:00-11am on Thursday.",

            "Pick up a Bestbuy order at 5th Avenue after 5pm.",

            "Book a flight to San Francisco on July 10th.",
        ],
        "questions": [
            "What time does the meeting with Claire start?",
            "Where do you need to pick up the Bestbuy order?",
            "What date is the flight to San Francisco?",
        ],
        "correctAnswers": [
            "10:00am",
            "5th Avenue",
            "July 10th",
        ],
    },
    "pair_B": {
        "texts": [
            "You missed a call from mom at 10:35pm yesterday.",

            "Visa appointment rescheduled to 2:30pm on Tuesday.",

            "Your OpenAI subscription renews on June 21th.",
        ],
        "questions": [
            "What time did your mom call yesterday?",
            "What time is the visa appointment rescheduled to?",
            "When does your OpenAI subscription renew?",
        ],
        "correctAnswers": [
            "10:35pm",
            "2:30pm on Tuesday",
            "June 21st",
        ],
    },
}

DEFAULT_SIGNAL_SECONDS_REMAINING = 8.0

# ── 4 units ───────────────────────────────────────────────────────────────────
# Unit 0: None, Unassisted
# Unit 1: None, FullyAssisted
# Unit 2: Pair (pair_A), Unassisted
# Unit 3: Pair (pair_B), FullyAssisted

NONE_UNITS = [
    {"assistanceLevel": "Unassisted"},
    {"assistanceLevel": "FullyAssisted"},
]

PAIR_UNITS = [
    {"pairId": "pair_A", "assistanceLevel": "Unassisted"},
    {"pairId": "pair_B", "assistanceLevel": "FullyAssisted"},
]

# ── Williams balanced Latin square ───────────────────────────────────────────

def williams_latin_square(n: int) -> list:
    """Balanced Latin square (Williams design) for n conditions (n must be even)."""
    first_row = [0]
    for i in range(1, n):
        first_row.append(i // 2 + 1 if i % 2 == 1 else n - i // 2)
    return [[(x + row) % n for x in first_row] for row in range(n)]

# ── Trial builder ─────────────────────────────────────────────────────────────

def expand_unit(unit_idx: int) -> list:
    """Return a list of trial dicts for the given unit (1 for None, 2 for pairs)."""
    if unit_idx < 2:
        u = NONE_UNITS[unit_idx]
        return [{
            "assistanceLevel":       u["assistanceLevel"],
            "distraction":           "None",
            "pairId":                "",
            "distractionTexts":      [],
            "questionTexts":         [],
            "correctAnswers":        [],
            "signalSecondsRemaining": DEFAULT_SIGNAL_SECONDS_REMAINING,
        }]
    else:
        u = PAIR_UNITS[unit_idx - 2]
        p = PAIRS[u["pairId"]]
        text_trial = {
            "assistanceLevel":       u["assistanceLevel"],
            "distraction":           "TextReading",
            "pairId":                u["pairId"],
            "distractionTexts":      list(p["texts"]),
            "questionTexts":         [],
            "correctAnswers":        [],
            "signalSecondsRemaining": DEFAULT_SIGNAL_SECONDS_REMAINING,
        }
        call_trial = {
            "assistanceLevel":       u["assistanceLevel"],
            "distraction":           "Conversation",
            "pairId":                u["pairId"],
            "distractionTexts":      [],
            "questionTexts":         list(p["questions"]),
            "correctAnswers":        list(p["correctAnswers"]),
            "signalSecondsRemaining": DEFAULT_SIGNAL_SECONDS_REMAINING,
        }
        return [text_trial, call_trial]


DISTRACTION_LABELS = {
    "None":         "NoDistraction",
    "TextReading":  "TextDistraction",
    "Conversation": "ConversationDistraction",
}


def build_config(participant_id: str, session_id: str, unit_order: list) -> dict:
    trials = []
    for unit_idx in unit_order:
        for t in expand_unit(unit_idx):
            t["trialID"] = f"{t['assistanceLevel']}-{DISTRACTION_LABELS[t['distraction']]}"
            t["conditionName"] = (
                f"{t['assistanceLevel']}_{t['distraction']}"
                + (f"[{t['pairId']}]" if t["pairId"] else "")
            )
            trials.append(t)
    return {"participantId": participant_id, "sessionId": session_id, "trials": trials}

# ── Main ──────────────────────────────────────────────────────────────────────

def main():
    output_dir = os.path.dirname(os.path.abspath(__file__))
    square = williams_latin_square(4)   # 4 units -> 4x4 square

    for participant, row in enumerate(square, start=1):
        pid = f"P{participant:03d}"
        config = build_config(pid, "S001", row)
        path = os.path.join(output_dir, f"trial_config_{pid}.json")
        with open(path, "w") as f:
            json.dump(config, f, indent=2)
        label = [f"U{r}" for r in row]
        print(f"  {pid} ({len(config['trials'])} trials): units {label}")

    print(f"\nGenerated {len(square)} files in:\n  {output_dir}")
    verify_balance(square)


def verify_balance(square: list):
    n = len(square)
    counts = [[0] * n for _ in range(n)]
    for row in square:
        for pos, unit in enumerate(row):
            counts[pos][unit] += 1
    perfect = all(counts[p][u] == 1 for p in range(n) for u in range(n))
    print(f"\nBalance check — each unit in each position exactly once: {perfect}")
    if not perfect:
        for p in range(n):
            for u in range(n):
                if counts[p][u] != 1:
                    print(f"  WARNING: unit {u} appears {counts[p][u]}x at position {p}")


if __name__ == "__main__":
    main()
