using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BeachVolley.Core
{
    /// <summary>
    /// Every scene change goes through here: a loading screen with a spinning ball and a tip, the
    /// scene loaded in the background, then the screen fades away. The ball is
    /// Resources/UI/LoadingBall.png, so swapping that file changes it. Survives scene loads.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        const float MinShowTime = 0.6f;
        const float FadeTime = 0.25f;
        const float SpinSpeed = 180f;

        static readonly string[] Tips =
        {
            "Call the ball early. \"Mine\" after it lands is just a sound.",
            "Move before the ball arrives, not after.",
            "A good set gives your attacker a chance. A bad one gives them a problem.",
            "One finger: the blocker takes the line. Two: the cross.",
            "The pancake is a last resort. Getting there early is the first.",
            "Don't block the attack you expected. Defend the one they can make.",
            "Tired legs make late decisions.",
            "Every professional was once a beginner.",
        };

        static SceneLoader instance;
        static int tipIndex = -1;

        UIDocument document;
        VisualElement screen, ball;
        Label tip;
        float angle;

        public static bool IsLoading { get; private set; }

        /// <summary>Loads <paramref name="scene"/> behind the loading screen.</summary>
        public static void Load(string scene)
        {
            if (IsLoading) return;
            Ensure();
            instance.StartCoroutine(instance.LoadRoutine(scene));
        }

        static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("SceneLoader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SceneLoader>();
        }

        void Awake()
        {
            document = gameObject.AddComponent<UIDocument>();
            // Borrow the menus' panel settings (theme and font); a bare one as a fallback.
            PanelSettings panel = null;
            foreach (var d in FindObjectsByType<UIDocument>())
                if (d != document && d.panelSettings != null) { panel = d.panelSettings; break; }
            if (panel == null) panel = ScriptableObject.CreateInstance<PanelSettings>();
            document.panelSettings = panel;
            document.sortingOrder = 1000;
        }

        void OnEnable() => Build();

        void Build()
        {
            var root = document.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            screen = new VisualElement();
            screen.style.position = Position.Absolute;
            screen.style.left = screen.style.right = screen.style.top = screen.style.bottom = 0;
            screen.style.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f);
            screen.style.alignItems = Align.Center;
            screen.style.justifyContent = Justify.Center;
            screen.pickingMode = PickingMode.Ignore;

            ball = new VisualElement();
            ball.style.width = ball.style.height = 150;
            var tex = Resources.Load<Texture2D>("UI/LoadingBall");
            if (tex != null) ball.style.backgroundImage = new StyleBackground(tex);
            else
            {
                ball.style.backgroundColor = new Color(0.98f, 0.85f, 0.3f);
                ball.style.borderTopLeftRadius = ball.style.borderTopRightRadius =
                    ball.style.borderBottomLeftRadius = ball.style.borderBottomRightRadius = 75;
            }
            screen.Add(ball);

            var loading = new Label("Loading…");
            loading.style.fontSize = 28;
            loading.style.color = new Color(0.97f, 0.95f, 0.9f);
            loading.style.unityFontStyleAndWeight = FontStyle.Bold;
            loading.style.marginTop = 26;
            screen.Add(loading);

            tip = new Label();
            tip.style.fontSize = 19;
            tip.style.color = new Color(0.75f, 0.82f, 0.86f);
            tip.style.marginTop = 10;
            tip.style.maxWidth = 900;
            tip.style.whiteSpace = WhiteSpace.Normal;
            tip.style.unityTextAlign = TextAnchor.MiddleCenter;
            screen.Add(tip);

            root.Add(screen);
            screen.style.display = DisplayStyle.None;
        }

        void Update()
        {
            if (!IsLoading || ball == null) return;
            angle = (angle + SpinSpeed * Time.unscaledDeltaTime) % 360f;
            ball.style.rotate = new Rotate(angle);
        }

        IEnumerator LoadRoutine(string scene)
        {
            IsLoading = true;
            tipIndex = (tipIndex + 1 + Random.Range(0, Tips.Length - 1)) % Tips.Length;
            tip.text = Tips[tipIndex];
            screen.style.display = DisplayStyle.Flex;
            yield return Fade(0f, 1f);

            float shown = Time.unscaledTime;
            var op = SceneManager.LoadSceneAsync(scene);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f || Time.unscaledTime - shown < MinShowTime) yield return null;
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            // Let the new scene run its first frames (story scenes start on black) before revealing it.
            yield return null;
            yield return null;
            yield return Fade(1f, 0f);
            screen.style.display = DisplayStyle.None;
            IsLoading = false;
        }

        IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
            {
                screen.style.opacity = Mathf.Lerp(from, to, t / FadeTime);
                yield return null;
            }
            screen.style.opacity = to;
        }
    }
}
