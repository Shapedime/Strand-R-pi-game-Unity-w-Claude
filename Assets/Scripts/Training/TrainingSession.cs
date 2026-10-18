using BeachVolley.Ball;
using BeachVolley.Core;
using BeachVolley.Player;
using BeachVolley.Skills;
using BeachVolley.Story;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeachVolley.Training
{
    public enum Drill
    {
        Footwork,
        Passing,
        Serve,
    }

    /// <summary>
    /// The core loop: city beginner training. Pick one of three beginner drills, do a short
    /// set of reps, earn a little XP for every rep (more for clean ones). Drills unlock as
    /// practices pile up or skills rise. Grading is generous for a rookie and tightens as
    /// the drill's stat improves. Enjoyment rises each session and nudges XP up.
    /// </summary>
    public class TrainingSession : MonoBehaviour
    {
        enum State { Idle, Running, Complete }

        [SerializeField] PlayerSkills skills;
        [SerializeField] BallContact contact;
        [SerializeField] PlayerServe serve;
        [SerializeField] VolleyBall ball;
        [SerializeField] Transform coach;
        [SerializeField] GameObject markerPrefab;
        [SerializeField] TrainingStory story;

        [Header("Session")]
        [SerializeField] int repsPerSession = 8;
        [SerializeField] float timeLimit = 150f;
        [SerializeField] float pauseBetweenReps = 1f;

        [Header("Unlocks (practices completed OR skill reached)")]
        [SerializeField] int passingAfterPractices = 1;
        [SerializeField] int passingAtPassSkill = 12;
        [SerializeField] int serveAfterPractices = 3;
        [SerializeField] int serveAtServeSkill = 10;

        [Header("Footwork drill: rookie → master")]
        [SerializeField] Vector2 markerRangeX = new Vector2(-3.2f, 3.2f);
        [SerializeField] Vector2 markerRangeZ = new Vector2(-7.2f, -1.5f);
        [SerializeField] float markerMinDistance = 2.5f;
        [SerializeField] Vector2 markerReach = new Vector2(0.9f, 0.45f);
        [SerializeField] Vector2 markerTime = new Vector2(9f, 4f);

        [Header("Passing drill: rookie → master")]
        [SerializeField] float tossApexHeight = 3.4f;
        [SerializeField] float tossTargetHeight = 0.9f;
        [Tooltip("How far off the player the coach's toss lands. Small for rookies.")]
        [SerializeField] Vector2 tossScatter = new Vector2(0.3f, 1.4f);
        [SerializeField] float tossTimeout = 6f;
        [Tooltip("Contacts below this height (player-relative) also train Defense.")]
        [SerializeField] float lowContactHeight = 0.7f;

        [Header("Serve drill: rookie → master")]
        [SerializeField] float serveTimeout = 12f;
        [Tooltip("How far outside the far court a serve can land and still count.")]
        [SerializeField] Vector2 serveMargin = new Vector2(2.5f, 0f);

        [Header("XP")]
        [SerializeField] float successXp = 4f;
        [SerializeField] float failXp = 1f;
        [SerializeField] float sideXp = 0.75f;
        [SerializeField] float completionXp = 2f;

        [Header("Enjoyment")]
        [Tooltip("Enjoyment for finishing a session, shrinking as enjoyment approaches 100.")]
        [SerializeField] float enjoymentPerSession = 6f;
        [SerializeField] float enjoymentPerCleanRep = 0.5f;

        State state = State.Idle;
        Drill drill = Drill.Footwork;
        bool repActive;
        int repIndex;
        int cleanReps;
        int completedPractices;
        float sessionStart;
        float repStart;
        float nextRepAt;
        GameObject marker;
        Vector3 markerPos;
        bool serveTossed, serveHit;
        float serveTossTime, serveHitTime, serveSide;
        int[] lastGains = new int[PlayerSkills.Count];
        float lastEnjoymentGain;
        string flash;
        float flashUntil;
        InputAction startAction, drill1, drill2, drill3;
        GUIStyle titleStyle, bodyStyle, smallStyle, flashStyle;

        public bool IsRunning => state == State.Running;
        public int CompletedPractices => completedPractices;

        /// <summary>Skills a drill trains: main first, then the side skills.</summary>
        public static SkillType[] TrainedSkills(Drill d) => d switch
        {
            Drill.Footwork => new[] { SkillType.Athleticism, SkillType.CourtAwareness },
            Drill.Passing => new[] { SkillType.Bump, SkillType.Dive, SkillType.CourtAwareness },
            _ => new[] { SkillType.Serve, SkillType.Spike, SkillType.CourtAwareness },
        };

        void Awake()
        {
            if (skills == null) skills = FindAnyObjectByType<PlayerSkills>();
            if (contact == null) contact = FindAnyObjectByType<BallContact>();
            if (serve == null) serve = FindAnyObjectByType<PlayerServe>();
            if (ball == null) ball = FindAnyObjectByType<VolleyBall>();
            if (story == null) story = FindAnyObjectByType<TrainingStory>();

            startAction = new InputAction("StartPractice", InputActionType.Button, "<Keyboard>/enter");
            startAction.AddBinding("<Keyboard>/numpadEnter");
            startAction.AddBinding("<Gamepad>/start");
            drill1 = new InputAction("Drill1", InputActionType.Button, "<Keyboard>/1");
            drill2 = new InputAction("Drill2", InputActionType.Button, "<Keyboard>/2");
            drill3 = new InputAction("Drill3", InputActionType.Button, "<Keyboard>/3");
        }

        void OnEnable()
        {
            startAction.Enable();
            drill1.Enable();
            drill2.Enable();
            drill3.Enable();
            if (contact != null) contact.Bumped += OnBumped;
            if (serve != null)
            {
                serve.Tossed += OnServeTossed;
                serve.Hit += OnServeHit;
            }
        }

        void OnDisable()
        {
            startAction.Disable();
            drill1.Disable();
            drill2.Disable();
            drill3.Disable();
            if (contact != null) contact.Bumped -= OnBumped;
            if (serve != null)
            {
                serve.Tossed -= OnServeTossed;
                serve.Hit -= OnServeHit;
            }
        }

        void OnDestroy()
        {
            startAction.Dispose();
            drill1.Dispose();
            drill2.Dispose();
            drill3.Dispose();
        }

        void Update()
        {
            if (state != State.Running)
            {
                if (story != null && (story.IsShowing || story.AdvancedThisFrame)) return;
                if (drill1.WasPressedThisFrame()) StartSession(Drill.Footwork);
                else if (drill2.WasPressedThisFrame()) StartSession(Drill.Passing);
                else if (drill3.WasPressedThisFrame()) StartSession(Drill.Serve);
                else if (startAction.WasPressedThisFrame()) StartSession(drill);
                return;
            }

            float now = Time.time;
            if (now - sessionStart > timeLimit)
            {
                if (repActive) EndRep(false, "Time's up");
                CompleteSession();
                return;
            }

            if (repActive) UpdateRep(now);
            else if (now >= nextRepAt) BeginRep(now);
        }

        // ---------- Unlocks ----------

        public bool IsUnlocked(Drill d) => d switch
        {
            Drill.Passing => completedPractices >= passingAfterPractices || skills.Get(SkillType.Bump) >= passingAtPassSkill,
            Drill.Serve => completedPractices >= serveAfterPractices || skills.Get(SkillType.Serve) >= serveAtServeSkill,
            _ => true,
        };

        string LockReason(Drill d)
        {
            int after = d == Drill.Passing ? passingAfterPractices : serveAfterPractices;
            int left = Mathf.Max(0, after - completedPractices);
            return $"after {left} more practice{(left == 1 ? "" : "s")}";
        }

        // ---------- Session ----------

        public void StartSession(Drill d)
        {
            if (state == State.Running || !IsUnlocked(d)) return;
            drill = d;
            skills.BeginSession();
            state = State.Running;
            repIndex = 0;
            cleanReps = 0;
            sessionStart = Time.time;
            nextRepAt = Time.time + 0.8f;
            Flash($"{d} drill - go!");
        }

        void CompleteSession()
        {
            if (state != State.Running) return;
            ClearMarker();
            repActive = false;

            bool passingWasOpen = IsUnlocked(Drill.Passing);
            bool serveWasOpen = IsUnlocked(Drill.Serve);

            // Showing up and finishing counts for something, even on a rough day.
            foreach (var s in TrainedSkills(drill))
                skills.AddXp(s, completionXp);
            lastEnjoymentGain = skills.AddEnjoyment(enjoymentPerSession * (1f - skills.Enjoyment / 100f));
            lastGains = skills.EndSession();
            completedPractices++;
            state = State.Complete;

            if (story != null)
            {
                if (!passingWasOpen && IsUnlocked(Drill.Passing)) story.OnPassingUnlocked();
                if (!serveWasOpen && IsUnlocked(Drill.Serve)) story.OnServeUnlocked();
                story.OnPracticeCompleted(completedPractices);
            }
        }

        // ---------- Reps ----------

        void BeginRep(float now)
        {
            repActive = true;
            repStart = now;
            switch (drill)
            {
                case Drill.Footwork: SpawnMarker(); break;
                case Drill.Passing: CoachToss(); break;
                case Drill.Serve:
                    serveTossed = serveHit = false;
                    break;
            }
        }

        void UpdateRep(float now)
        {
            float elapsed = now - repStart;
            bool ballDown = ball.transform.position.y < VolleyBall.Radius + 0.05f;
            switch (drill)
            {
                case Drill.Footwork:
                {
                    if (marker != null) marker.transform.Rotate(0f, 90f * Time.deltaTime, 0f);
                    Vector3 p = skills.transform.position;
                    float reach = Scale(SkillType.Athleticism, markerReach);
                    if (new Vector2(p.x - markerPos.x, p.z - markerPos.z).sqrMagnitude <= reach * reach) EndRep(true, "Made it!");
                    else if (elapsed > Scale(SkillType.Athleticism, markerTime)) EndRep(false, "Too slow - still learning");
                    break;
                }
                case Drill.Passing:
                    if ((elapsed > 0.3f && ballDown) || elapsed > tossTimeout) EndRep(false, "Missed - still learning");
                    break;
                case Drill.Serve:
                    if (serveHit && now - serveHitTime > 0.3f && ballDown)
                        EndRep(ServeLandedIn(), ServeLandedIn() ? "Over the net!" : "Out - keep swinging");
                    else if (serveTossed && !serveHit && now - serveTossTime > 0.5f && ballDown)
                        EndRep(false, "Missed the hit - toss again");
                    else if (elapsed > serveTimeout)
                        EndRep(false, "Time's up for that one");
                    break;
            }
        }

        bool ServeLandedIn()
        {
            Vector3 p = ball.transform.position;
            float margin = Scale(SkillType.Serve, serveMargin);
            float depth = p.z * serveSide;
            return depth > 0.15f && depth <= CourtHalfLength + margin && Mathf.Abs(p.x) <= CourtHalfWidth + margin;
        }

        const float CourtHalfLength = Court.CourtDimensions.SideLength;
        const float CourtHalfWidth = Court.CourtDimensions.HalfWidth;

        void OnBumped(float contactHeight)
        {
            if (state != State.Running || !repActive || drill != Drill.Passing) return;
            if (contactHeight < lowContactHeight)
                skills.AddXp(SkillType.Dive, sideXp * 2f);
            EndRep(true, "Nice touch!");
        }

        void OnServeTossed()
        {
            if (state != State.Running || !repActive || drill != Drill.Serve) return;
            serveTossed = true;
            serveHit = false;
            serveTossTime = Time.time;
        }

        void OnServeHit(float side)
        {
            if (state != State.Running || !repActive || drill != Drill.Serve) return;
            serveHit = true;
            serveHitTime = Time.time;
            serveSide = side;
        }

        void EndRep(bool clean, string message)
        {
            var trained = TrainedSkills(drill);
            skills.AddXp(trained[0], clean ? successXp : failXp);
            skills.AddXp(SkillType.CourtAwareness, sideXp);
            if (clean && drill == Drill.Serve) skills.AddXp(SkillType.Spike, sideXp);
            if (clean)
            {
                cleanReps++;
                skills.AddEnjoyment(enjoymentPerCleanRep);
            }
            Flash(message);

            ClearMarker();
            repActive = false;
            repIndex++;
            if (repIndex >= repsPerSession) CompleteSession();
            else nextRepAt = Time.time + pauseBetweenReps;
        }

        float Scale(SkillType type, Vector2 rookieToMaster) => skills.Scale(type, rookieToMaster.x, rookieToMaster.y);

        void SpawnMarker()
        {
            Vector3 player = skills.transform.position;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                markerPos = new Vector3(Random.Range(markerRangeX.x, markerRangeX.y), 0f, Random.Range(markerRangeZ.x, markerRangeZ.y));
                if (Vector2.Distance(new Vector2(markerPos.x, markerPos.z), new Vector2(player.x, player.z)) >= markerMinDistance)
                    break;
            }
            if (markerPrefab != null)
                marker = Instantiate(markerPrefab, markerPos, Quaternion.identity, transform);
        }

        void ClearMarker()
        {
            if (marker != null) Destroy(marker);
            marker = null;
        }

        /// <summary>Lobs the ball from the coach so it drops through waist height near the player.</summary>
        void CoachToss()
        {
            Vector3 origin = coach != null
                ? coach.position + coach.forward * 0.45f + Vector3.up * 1.5f
                : new Vector3(1.8f, 1.5f, -1.4f);

            Transform player = skills.transform;
            Vector2 scatter = Random.insideUnitCircle * Scale(SkillType.Bump, tossScatter);
            Vector3 target = player.position + player.forward * 0.5f + new Vector3(scatter.x, tossTargetHeight, scatter.y);

            ball.Place(origin);
            ball.Launch(ball.VelocityToReach(origin, target, tossApexHeight));
        }

        void Flash(string text)
        {
            flash = text;
            flashUntil = Time.time + 1.4f;
        }

        // ---------- Overlays ----------

        void OnGUI()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bodyStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, richText = true };
            smallStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, richText = true };
            flashStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.92f, 0.55f) },
            };

            // The story panel owns the screen while it's talking.
            if (story != null && story.IsShowing && state != State.Running) return;

            switch (state)
            {
                case State.Idle: DrawDrillPicker(); break;
                case State.Running: DrawRunning(); break;
                case State.Complete: DrawComplete(); break;
            }

            if (Time.time < flashUntil)
                GUI.Label(new Rect(0f, Screen.height * 0.22f, Screen.width, 40f), flash, flashStyle);
        }

        void DrawDrillPicker()
        {
            var rect = new Rect((Screen.width - 540f) * 0.5f, Screen.height - 170f, 540f, 150f);
            UiPointerGate.Block(rect);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x, rect.y + 6f, rect.width, 28f), "City beginner training", titleStyle);
            GUI.Label(new Rect(rect.x, rect.y + 32f, rect.width, 18f),
                $"<color=#bbbbbb>Practice {completedPractices + 1} - pick a drill (1 / 2 / 3)</color>", smallStyle);

            for (int i = 0; i < 3; i++)
            {
                var d = (Drill)i;
                var b = new Rect(rect.x + 15f + i * 175f, rect.y + 58f, 160f, 76f);
                bool open = IsUnlocked(d);
                string trains = string.Join(", ", System.Array.ConvertAll(TrainedSkills(d), s => s.ToString()));
                string label = open
                    ? $"<b>{i + 1}. {d}</b>\n<size=11>{trains}</size>"
                    : $"<b>{i + 1}. {d}</b>\n<size=11>Locked - {LockReason(d)}</size>";
                GUI.enabled = open;
                if (GUI.Button(b, new GUIContent(label), new GUIStyle(GUI.skin.button) { richText = true, wordWrap = true }))
                    StartSession(d);
                GUI.enabled = true;
            }
        }

        void DrawRunning()
        {
            string task = !repActive ? "Get ready..." : drill switch
            {
                Drill.Footwork => "Run to the marker",
                Drill.Passing => "Bump the coach's toss  (click)",
                _ => !serveTossed || serveHit ? "Toss the ball up  (C)" : "Serve it: click = power, F = short",
            };
            float left = Mathf.Max(0f, timeLimit - (Time.time - sessionStart));
            var rect = new Rect((Screen.width - 460f) * 0.5f, 10f, 460f, 62f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x, rect.y + 6f, rect.width, 26f),
                $"{drill}  -  Rep {Mathf.Min(repIndex + 1, repsPerSession)}/{repsPerSession}  -  {task}", bodyStyle);
            GUI.Label(new Rect(rect.x, rect.y + 32f, rect.width, 24f),
                $"<color=#bbbbbb>Clean {cleanReps}   Time {left:0}s</color>", bodyStyle);
        }

        void DrawComplete()
        {
            int gainedSkills = 0;
            foreach (int g in lastGains) if (g > 0) gainedSkills++;

            float height = 200f + Mathf.Max(1, gainedSkills) * 24f;
            var rect = new Rect((Screen.width - 380f) * 0.5f, (Screen.height - height) * 0.5f, 380f, height);
            UiPointerGate.Block(rect);
            GUI.Box(rect, GUIContent.none);

            float y = rect.y + 12f;
            GUI.Label(new Rect(rect.x, y, rect.width, 30f), "Session complete", titleStyle);
            y += 36f;
            GUI.Label(new Rect(rect.x, y, rect.width, 22f), $"{drill}: {cleanReps}/{repsPerSession} clean reps", bodyStyle);
            y += 30f;

            if (gainedSkills == 0)
            {
                GUI.Label(new Rect(rect.x, y, rect.width, 22f), "<color=#bbbbbb>No level-ups yet - XP banked</color>", bodyStyle);
                y += 24f;
            }
            for (int i = 0; i < lastGains.Length; i++)
            {
                if (lastGains[i] <= 0) continue;
                var type = (SkillType)i;
                GUI.Label(new Rect(rect.x, y, rect.width, 22f),
                    $"<b>{type} +{lastGains[i]}</b>  <color=#bbbbbb>(now {skills.Get(type)})</color>", bodyStyle);
                y += 24f;
            }

            y += 6f;
            GUI.Label(new Rect(rect.x, y, rect.width, 22f),
                $"Enjoyment +{lastEnjoymentGain:0.0}  <color=#bbbbbb>(XP x{skills.XpMultiplier:0.00})</color>", bodyStyle);
            y += 34f;

            if (GUI.Button(new Rect(rect.x + 30f, y, 150f, 40f), "Practice again  (Enter)"))
                StartSession(drill);
            if (GUI.Button(new Rect(rect.x + 200f, y, 150f, 40f), "Other drills"))
                state = State.Idle;
        }
    }
}
