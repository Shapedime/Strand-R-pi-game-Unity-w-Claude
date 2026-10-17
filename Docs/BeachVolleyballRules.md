# Beach volleyball rules — reference for matches and tournaments

Practice mode does not enforce these. They are here so real games (story-mode matches, bot
tournaments, online with friends) can follow them later. Each rule notes how it would map to the game.
Based on the FIVB beach volleyball rules.

## Court, team, net
- **2 v 2**, no substitutes. Court **8 × 16 m** (two 8 × 8 halves), sand at least 40 cm deep.
- Net height **2.43 m** (men) / **2.24 m** (women). Antennas mark the crossing space.
- *Game:* court and net already match (`CourtDimensions`). A women's net height would be a match setting.

## Scoring
- **Rally scoring**: every rally scores a point.
- Best of 3 sets. Sets 1–2 to **21**, set 3 to **15**, always **win by 2**.
- **Switch sides every 7 points** (every 5 in set 3).
- One **30-second time-out** per team per set. In sets 1–2 a **technical time-out** when the points add up to 21.
- *Game:* a match/rally manager that tracks points, sets, side switches and time-outs.

## Contacts
- At most **3 contacts per side** to return the ball.
- **The block counts as one of the three** (unlike indoor).
- No player may touch the ball **twice in a row**. Exception: the blocker may make the next touch (it counts as the second).
- **First contact**: the ball may touch several body parts in one action. On a hard-driven ball a brief catch / finger action is allowed.
- *Game:* count touches per side in the rally manager. `BallContact.Played` already reports every touch with its type.

## Soft touches and setting (the tricky ones)
- **No open-hand tips or dinks** with the fingertips. Placement must be a **roll shot** (whole hand, ball rolled off the fingers with the arm), knuckles (**"cobra"**: rigid straight fingertips), or the heel of the palm.
- **No soft finger touch on the first contact** (except a hard-driven ball) **and none as the attack** (last contact). In other words, you can't "set" the ball over as your attack.
- **Overhand set over the net**: only legal if the ball travels **perpendicular to the line of the shoulders** (front or back set). A sideways set over the net is a fault.
- Sets are judged strictly: double contacts and lifts/catches are faults.
- *Game:* our roll shot (F) is the legal placement shot. A set (RMB) that crosses the net as the third touch, or not perpendicular to the shoulders, would be called a fault. A set of a hard-driven ball is already refused in practice ("Too fast – bump it").

## Serve
- Serve within **5 seconds** of the whistle. Partners **alternate serving** when their team wins the serve back.
- The served ball **may touch the net** and still be good.
- No screening of the server by the partner.
- *Game:* serve on C, then LMB (power) / F (short). The rally manager would handle whistle, timer and rotation.

## Ball in / out
- The ball must cross the net **between the antennas**. Touching an antenna is out.
- A ball landing on a line is **in**.
- Players may chase the ball outside the court, even into the opponent's free zone, and play it back through the crossing space.
- *Game:* the landing point is known (`VolleyBall.Landed`). In/out = inside the lines including line width.

## Net and centre line
- No touching the net while playing the ball. Incidental contact away from the play is not a fault.
- Players may cross under the net into the opponent's space **as long as they don't interfere** with the opponent.
- *Game:* net-touch detection near the net while a hit is being made. Interference only matters once there are opponents.

## Realism already built into practice
- **Dead ball**: once the ball touches the sand nobody can play it (red ring around it).
- No spiking or rolling a fast incoming ball ("Too fast – dig it"). No overhand set of one.
- No spiking from far outside the court ("Too far"). Rolls still work, less accurately. Spikes from deep in the court become down-balls.
- Blocks only at the net. Hard balls are harder to receive (smaller timing window). Passing on the run is less accurate.
- Jump height depends on the approach run.
