using System;
using System.Collections;
using BeachVolley.Player;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Volleyball that only looks real: a prop ball flown along scripted arcs between figures on a
    /// court, with no physics or AI. Used for the TV match and the players warming up at the club.
    /// Positions are in this court's local space (net on z = 0, near team on −z).
    /// When a story scene takes one of the players away (puts them somewhere else, or hides them)
    /// while the rally runs, a substitute steps in for them, so nobody keeps playing a ball from
    /// across the club. <see cref="ResetPositions"/> brings the originals back.
    /// </summary>
    public class ScriptedPlay : MonoBehaviour
    {
        [SerializeField] Transform ball;
        [Tooltip("Near-side team (−z). Index 0 is the attacker who wins the point.")]
        [SerializeField] Transform[] nearTeam = new Transform[0];
        [SerializeField] Transform[] farTeam = new Transform[0];
        [SerializeField] bool loopOnStart = true;
        [SerializeField] float tempo = 1f;
        [Tooltip("Inactive extras who step in when a player is taken away mid-rally.")]
        [SerializeField] Transform[] substitutes = new Transform[0];

        Coroutine loop;
        Vector3[] homes;
        Transform[] originals;      // who started in each slot (near team first, then far)
        Vector3[] expected;         // where the rally last put each slot's player

        /// <summary>Raised with each point of the scripted final ("20 : 19").</summary>
        public event Action<string> Score;

        void Awake()
        {
            homes = new Vector3[nearTeam.Length + farTeam.Length];
            for (int i = 0; i < nearTeam.Length; i++) homes[i] = nearTeam[i].localPosition;
            for (int i = 0; i < farTeam.Length; i++) homes[nearTeam.Length + i] = farTeam[i].localPosition;
            originals = new Transform[homes.Length];
            for (int i = 0; i < homes.Length; i++) originals[i] = Member(i);
            expected = (Vector3[])homes.Clone();
            foreach (var sub in substitutes) if (sub != null) sub.gameObject.SetActive(false);
        }

        // ---------- Slots and substitutes ----------

        int Slots => nearTeam.Length + farTeam.Length;
        Transform Member(int i) => i < nearTeam.Length ? nearTeam[i] : farTeam[i - nearTeam.Length];
        void SetMember(int i, Transform t) { if (i < nearTeam.Length) nearTeam[i] = t; else farTeam[i - nearTeam.Length] = t; }

        int SlotOf(Transform who)
        {
            for (int i = 0; i < Slots; i++) if (Member(i) == who) return i;
            return -1;
        }

        /// <summary>Moves a rally player and remembers where, so a move by anyone else stands out.</summary>
        void Place(Transform who, Vector3 local)
        {
            who.localPosition = local;
            int i = SlotOf(who);
            if (i >= 0) expected[i] = local;
        }

        void LateUpdate()
        {
            if (loop == null || expected == null) return;
            for (int i = 0; i < Slots; i++)
            {
                var m = Member(i);
                Vector3 d = m.localPosition - expected[i];
                d.y = 0f;
                if (m.gameObject.activeInHierarchy && d.sqrMagnitude < 0.75f * 0.75f) continue;
                if (!Substitute(i))
                {
                    // Nobody to bring in: stop rather than have someone play a ball from miles away.
                    StopLoop();
                    return;
                }
            }
        }

        bool Substitute(int slot)
        {
            foreach (var sub in substitutes)
            {
                if (sub == null || sub.gameObject.activeSelf || SlotOf(sub) >= 0) continue;
                var gone = Member(slot);
                sub.localPosition = expected[slot];
                sub.localRotation = gone.parent == transform ? gone.localRotation : Quaternion.LookRotation(slot < nearTeam.Length ? Vector3.forward : Vector3.back);
                sub.gameObject.SetActive(true);
                SetMember(slot, sub);
                return true;
            }
            return false;
        }

        /// <summary>Puts every original player back in their slot (substitutes leave).</summary>
        void RestoreOriginals(bool onlyIfNearHome)
        {
            for (int i = 0; i < Slots; i++)
            {
                var o = originals[i];
                if (Member(i) == o || o == null) continue;
                if (onlyIfNearHome)
                {
                    Vector3 d = o.localPosition - homes[i];
                    d.y = 0f;
                    if (!o.gameObject.activeInHierarchy || d.sqrMagnitude > 1.5f * 1.5f) continue;
                }
                Member(i).gameObject.SetActive(false);
                SetMember(i, o);
                expected[i] = o.localPosition;
            }
        }

        void Start() { if (loopOnStart) StartLoop(); }

        public void StartLoop()
        {
            StopLoop();
            if (originals != null) RestoreOriginals(onlyIfNearHome: true);
            for (int i = 0; expected != null && i < Slots; i++) expected[i] = Member(i).localPosition;
            loop = StartCoroutine(RallyLoop());
        }

        public void StopLoop()
        {
            if (loop != null) StopCoroutine(loop);
            loop = null;
        }

        Vector3 W(Vector3 local) => transform.TransformPoint(local);

        // ---------- Building blocks ----------

        /// <summary>Flies the prop ball from → to (local) peaking at apex height, in duration seconds.</summary>
        public IEnumerator Arc(Vector3 from, Vector3 to, float apex, float duration)
        {
            duration /= tempo;
            float top = Mathf.Max(apex, from.y, to.y);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                Vector3 p = Vector3.Lerp(from, to, k);
                // Quadratic Bézier whose midpoint height is the apex.
                float c = 2f * top - (from.y + to.y) * 0.5f;
                p.y = (1f - k) * (1f - k) * from.y + 2f * k * (1f - k) * c + k * k * to.y;
                ball.position = W(p);
                ball.Rotate(400f * Time.deltaTime, 120f * Time.deltaTime, 0f, Space.Self);
                yield return null;
            }
            ball.position = W(to);
        }

        IEnumerator Move(Transform who, Vector3 local, float duration)
        {
            duration /= tempo;
            Vector3 start = who.localPosition;
            local.y = start.y;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                Place(who, Vector3.Lerp(start, local, Mathf.SmoothStep(0f, 1f, t / duration)));
                yield return null;
            }
            Place(who, local);
        }

        IEnumerator Jump(Transform who, float height, float duration)
        {
            duration /= tempo;
            Vector3 start = who.localPosition;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                Place(who, start + Vector3.up * (height * Mathf.Sin(Mathf.PI * t / duration)));
                yield return null;
            }
            Place(who, start);
        }

        void Go(IEnumerator routine) => StartCoroutine(routine);

        /// <summary>Plays the touch animation on whoever hits (if they're a StoryActor).</summary>
        static void Hit(Transform who, HitType type)
        {
            var actor = who != null ? who.GetComponent<StoryActor>() : null;
            if (actor != null) actor.Volley(type);
        }

        static void Act(Transform who, string gesture)
        {
            var actor = who != null ? who.GetComponent<StoryActor>() : null;
            if (actor != null) actor.Gesture(gesture);
        }

        IEnumerator HitAfter(Transform who, HitType type, float delay)
        {
            yield return new WaitForSeconds(delay / tempo);
            Hit(who, type);
        }

        static Vector3 Hands(Transform who, float h) => who.localPosition + new Vector3(0f, h, 0f);

        // ---------- Idle rally ----------

        IEnumerator RallyLoop()
        {
            var rng = new System.Random(name.GetHashCode());
            int side = 0;
            Transform last = nearTeam.Length > 0 ? nearTeam[0] : null;
            if (last == null) yield break;
            while (true)
            {
                // Pass to partner, set, then send it over to a random player on the other side.
                var team = side == 0 ? nearTeam : farTeam;
                var other = side == 0 ? farTeam : nearTeam;
                var partner = team.Length > 1 && team[0] == last ? team[1] : team[0];
                Hit(last, HitType.Bump);
                yield return Arc(Hands(last, 0.8f), Hands(partner, 2.1f), 4f, 1.2f);
                var hitter = team.Length > 1 && partner == team[0] ? team[1] : last;
                Hit(partner, HitType.Set);
                Go(HitAfter(hitter, HitType.Spike, 0.7f));
                yield return Arc(Hands(partner, 2.1f), Hands(hitter, 2.4f), 4.2f, 1.1f);
                var receiver = other[rng.Next(other.Length)];
                yield return Arc(Hands(hitter, 2.4f), Hands(receiver, 0.8f), 3.6f, 1.3f);
                last = receiver;
                side = 1 - side;
            }
        }

        // ---------- The televised match point ----------

        /// <summary>Match point: serve, pass, set, and the near team's attacker smashes it into the sand. Then celebration.</summary>
        public IEnumerator WinningPoint()
        {
            StopLoop();
            ResetPositions();
            var attacker = nearTeam[0];
            var setter = nearTeam.Length > 1 ? nearTeam[1] : nearTeam[0];
            var server = farTeam[0];
            Score?.Invoke("20 : 19");

            // Serve from behind the far baseline.
            Place(server, new Vector3(1.5f, 0f, 8.6f));
            Hit(server, HitType.Spike);
            yield return new WaitForSeconds(0.35f / tempo);
            yield return Arc(Hands(server, 2.3f), new Vector3(-1f, 0.9f, -5f), 5.5f, 1.5f);
            // Attacker passes, setter runs in.
            Go(Move(attacker, new Vector3(-1f, 0f, -5f), 0.3f));
            Go(Move(setter, new Vector3(0.3f, 0f, -1.6f), 0.9f));
            Hit(attacker, HitType.Bump);
            yield return Arc(new Vector3(-1f, 0.9f, -5f), new Vector3(0.3f, 2.2f, -1.6f), 4.6f, 1.25f);
            // Set to the left; attacker approaches and jumps.
            Go(Move(attacker, new Vector3(-1.6f, 0f, -2.2f), 0.8f));
            Go(DelayedJump(attacker, 0.75f, 0.95f, 0.9f));
            Hit(setter, HitType.Set);
            Go(HitAfter(attacker, HitType.Spike, 0.85f));
            yield return Arc(new Vector3(0.3f, 2.2f, -1.6f), new Vector3(-1.4f, 3.2f, -0.9f), 5f, 1.25f);
            // The spike: straight down into the far court, defenders too late.
            Go(Move(farTeam[farTeam.Length - 1], new Vector3(2.6f, 0f, 3.5f), 0.4f));
            yield return Arc(new Vector3(-1.4f, 3.2f, -0.9f), new Vector3(2.2f, 0.16f, 4.2f), 3.2f, 0.28f);
            yield return Arc(new Vector3(2.2f, 0.16f, 4.2f), new Vector3(3f, 0.16f, 5.5f), 0.7f, 0.5f);
            Score?.Invoke("21 : 19  —  MATCH");

            // Celebration: they jump and meet in the middle.
            yield return new WaitForSeconds(0.3f / tempo);
            Go(Move(setter, new Vector3(-0.8f, 0f, -2.6f), 0.6f));
            Act(attacker, "FistPump");
            Act(setter, "Cheer");
            foreach (var f in farTeam) Act(f, "HeadShake");
            for (int i = 0; i < 3; i++)
            {
                Go(Jump(setter, 0.5f, 0.45f));
                yield return Jump(attacker, 0.6f, 0.5f);
            }
        }

        IEnumerator DelayedJump(Transform who, float delay, float height, float duration)
        {
            yield return new WaitForSeconds(delay / tempo);
            yield return Jump(who, height, duration);
        }

        /// <summary>The club: the near attacker (Marlon) gets a set and puts it away hard.</summary>
        public IEnumerator PowerSpike()
        {
            StopLoop();
            ResetPositions();
            var attacker = nearTeam[0];
            var setter = nearTeam.Length > 1 ? nearTeam[1] : nearTeam[0];
            Place(setter, new Vector3(0.5f, 0f, -1.4f));
            Place(attacker, new Vector3(-1.5f, 0f, -4.5f));
            ball.position = W(Hands(setter, 2.2f));
            yield return new WaitForSeconds(0.4f);
            Go(Move(attacker, new Vector3(-1.4f, 0f, -1.9f), 0.9f));
            Go(DelayedJump(attacker, 0.8f, 1f, 0.9f));
            Hit(setter, HitType.Set);
            Go(HitAfter(attacker, HitType.Spike, 0.85f));
            yield return Arc(Hands(setter, 2.2f), new Vector3(-1.3f, 3.3f, -0.9f), 5f, 1.25f);
            yield return Arc(new Vector3(-1.3f, 3.3f, -0.9f), new Vector3(1.5f, 0.16f, 3.6f), 3.3f, 0.25f);
            yield return Arc(new Vector3(1.5f, 0.16f, 3.6f), new Vector3(2.4f, 0.16f, 5.4f), 0.9f, 0.5f);
        }

        /// <summary>Everyone back to their spot, the original players included (substitutes leave).</summary>
        public void ResetPositions()
        {
            if (originals != null) RestoreOriginals(onlyIfNearHome: false);
            for (int i = 0; i < Slots; i++)
            {
                var m = Member(i);
                if (!m.gameObject.activeSelf) m.gameObject.SetActive(true);
                Place(m, homes[i]);
            }
        }
    }
}
