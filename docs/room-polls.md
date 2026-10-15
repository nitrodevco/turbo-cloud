# Room polls

A poll the hotel offers to whoever enters a room: a short offer, then a few questions, then a
thank-you. The client side is Flash's `PollWidget` (offer `PollOfferDialog`, questions
`PollContentDialog`); the room's word quiz is a different thing (`IRoomGrain.AnswerWordQuizAsync`)
that shares only the answer message.

## What it does

- **On entering a room** the player is offered the room's first enabled poll they have not turned
  down or finished (`PollOffer`: id, type, headline, summary). It goes last in the entry: the
  client takes an offer only for the room it is in.
- **Start** (`PollStart`) sends the questions (`PollContents`), or `PollError` (a plain error
  alert) for a poll that is not this room's, disabled, turned down or finished. Starting records
  the player as started.
- **Cancel and the close button** send `PollReject`: the poll is not offered to that player again.
  **Later** sends nothing, so the poll is offered again on the next entry; so is one started and
  left unfinished.
- **Each question is answered on its own** (`PollAnswer`: poll id, question id, answers). A poll the
  player has started takes the answer first; any other answer goes to the room's word quiz. An
  answer is kept once per question when it fits: one of the choice values for a single choice,
  any distinct choice values for a multiple choice, one text of up to 512 characters (the client's
  input limit) for a text question. Once every top-level question has an answer the player has
  finished the poll; the client shows the end message itself.

## Setting one up (data)

| Table | What |
|---|---|
| `room_polls` | `room_id`, `headline` and `summary` (the offer), `start_message` (above the questions), `end_message` (the thank-you), `type` (sent with the offer, not shown), `nps_poll`, `enabled` |
| `room_poll_questions` | `poll_id`, `sort_order`, `question_type` (1 single choice, 2 multiple choice, 3 text line, 4 text area), `text`, `category`, `answer_type`, `parent_question_id` |
| `room_poll_choices` | `question_id`, `sort_order`, `value` (what the answer carries), `text` (what is shown), `choice_type` |

A question with a `parent_question_id` is a follow-up. In a score poll (`nps_poll`) the client asks
the follow-up whose `category` matches the `choice_type` of the choice picked for its parent.

Answers are in `room_poll_answers` (one row per player and question, multiple values one per
line) and where each player is with a poll in `player_room_polls` (0 started, 1 turned down,
2 finished).

## Evidence

No official capture of a room poll exists; everything here follows the AS3 of build
WIN63-202610131318: `PollOfferParser`, `PollContentsParser` (the question loop as it decompiles in
the 0909 build), `PollOfferDialog` (Start / Cancel / Later / close), `PollContentDialog`
(answer types, follow-ups, `pollFinished`) and the `poll_answer_text_input` layout (`max_chars`
512). Where in the room entry Habbo sends the offer is not recorded.
