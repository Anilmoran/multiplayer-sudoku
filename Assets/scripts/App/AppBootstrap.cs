using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;

namespace CozySudoku
{
    /// <summary>
    /// Uygulama açılışında bir kez çalışır: kare hızı, kalıcı EventSystem,
    /// ses, tween koşucusu ve pil dostu render ayarlarını kurar.
    /// Sahnelerde ek bir nesneye gerek kalmaz.
    /// </summary>
    public static class AppBootstrap
    {
        public static GameObject Root { get; private set; }
        public static bool IsMobile =>
#if UNITY_ANDROID || UNITY_IOS
            true;
#else
            Application.isMobilePlatform;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (Root != null) return;

            // Mobilde 60 FPS sabit, masaüstünde vSync
            if (IsMobile)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
            }
            else
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
            }

            AudioListener.volume = Settings.Sound ? 1f : 0f;

            Root = new GameObject("[CozySudoku App]");
            Object.DontDestroyOnLoad(Root);
            Root.AddComponent<Runner>();
            Root.AddComponent<PowerSaver>();
            Sfx.Init(Root);

            var es = new GameObject("EventSystem");
            es.transform.SetParent(Root.transform, false);
            es.AddComponent<EventSystem>();
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }

    /// <summary>Kalıcı coroutine sahibi (tween'ler, geçişler).</summary>
    public sealed class Runner : MonoBehaviour
    {
        public static Runner Instance { get; private set; }
        void Awake() => Instance = this;
    }

    /// <summary>
    /// Sudoku çoğu zaman durağan bir oyundur: etkileşim/animasyon yokken
    /// render sıklığını yarıya indirip pil ve ısı tasarrufu sağlar (girdi her karede okunur).
    /// </summary>
    public sealed class PowerSaver : MonoBehaviour
    {
        const float IdleDelay = 1.25f;
        static float lastActivity;

        public static void Ping() => lastActivity = Time.unscaledTime;

        void Update()
        {
            if (Tween.ActiveCount > 0 || SceneTransition.IsBusy || HasInput()) Ping();

            int interval = Time.unscaledTime - lastActivity > IdleDelay ? 2 : 1;
            if (OnDemandRendering.renderFrameInterval != interval)
                OnDemandRendering.renderFrameInterval = interval;
        }

        static bool HasInput()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.delta.ReadValue().sqrMagnitude > 0.01f || mouse.scroll.ReadValue().sqrMagnitude > 0.01f)) return true;
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.isPressed) return true;
            return false;
        }
    }

    public static class Haptics
    {
        public static void Bump()
        {
            if (!Settings.Haptics) return;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
