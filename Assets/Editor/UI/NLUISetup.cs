// Naija Streets: one-click setup for the screen UI (HUD-1 to HUD-6), so the
// Editor steps for Ted's team are "click the menu, press Play, capture".
//
// Menus (Naija Streets > UI):
//   Add HUD to open scene     creates Assets/UI/NLPanelSettings.asset (once) and an
//                             "NL UI" object with UIDocument + every UI component,
//                             plus an EventSystem with the Input System UI module.
//   Test: pay a shift         Editor fake wallet pays +N1,200 (count-up + money toast)
//   Test: spend at Mama Put   Editor fake wallet charges N1,200
//   Test: go offline / online Bank timeout and retry states
//   Test: offer 3 prompts     one prompt + "+2" chip; Tab or tap +2 cycles
//   Test: low vitals          red bars
// Batch (CI / harness):
//   -executeMethod NaijaLife.UI.Editor.NLUISetup.AddToGreyboxBatch
//   opens Assets/Scenes/Greybox_M1.unity, adds the UI and saves the scene.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace NaijaLife.UI.Editor
{
    public static class NLUISetup
    {
        private const string PanelPath = "Assets/UI/NLPanelSettings.asset";
        private const string ThemePath = "Assets/UI/Styles/NLRuntimeTheme.tss";
        private const string UxmlPath = "Assets/UI/UXML/NLHud.uxml";
        private const string GreyboxScene = "Assets/Scenes/Greybox_M1.unity";

        [MenuItem("Naija Streets/UI/Add HUD to open scene")]
        public static void AddToOpenScene()
        {
            var go = AddUI();
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[NLUISetup] HUD added. Press Play. Use Naija Streets > UI > Test menus for states.");
        }

        public static void AddToGreyboxBatch()
        {
            var scene = EditorSceneManager.OpenScene(GreyboxScene, OpenSceneMode.Single);
            AddUI();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NLUISetup] HUD added to " + GreyboxScene + " and saved.");
        }

        private static GameObject AddUI()
        {
            var panel = EnsurePanelSettings();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (tree == null) Debug.LogError("[NLUISetup] Missing " + UxmlPath);

            var go = GameObject.Find("NL UI");
            if (go == null) go = new GameObject("NL UI");

            var doc = Get<UIDocument>(go);
            doc.panelSettings = panel;
            doc.visualTreeAsset = tree;

            Get<NLUIRoot>(go);
            Get<NLStickControl>(go);
            Get<NLButtonControl>(go);
            Get<NLTouchControls>(go);
            Get<NLHud>(go);
            Get<NLToasts>(go);
            Get<NLPrompts>(go);
            Get<NLPhone>(go);
            Get<NLRadar>(go);
            Get<NLDriveHud>(go);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
            return go;
        }

        private static PanelSettings EnsurePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel != null) return panel;
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (panel.themeStyleSheet == null) Debug.LogWarning("[NLUISetup] Theme not found at " + ThemePath + "; buttons will be unstyled.");
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = 1f;           // NLUIRoot sets the real scale at runtime
            panel.sortingOrder = 10;
            AssetDatabase.CreateAsset(panel, PanelPath);
            AssetDatabase.SaveAssets();
            return panel;
        }

        private static T Get<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        // ---------------- test menus (play mode) ----------------

        [MenuItem("Naija Streets/UI/Test: pay a shift")]
        private static void TestPay() => Fake()?.ServerEvent(1200, "job", "Conductor shift", "Oshodi to Ikeja", "Shift pay landed");

        [MenuItem("Naija Streets/UI/Test: spend at Mama Put")]
        private static void TestSpend() => Fake()?.ServerEvent(-1200, "food", "Mama Put jollof", "Oshodi", null);

        [MenuItem("Naija Streets/UI/Test: go offline")]
        private static void TestOffline() { var f = Fake(); if (f != null) f.SimulateOffline = true; }

        [MenuItem("Naija Streets/UI/Test: go online")]
        private static void TestOnline() { var f = Fake(); if (f != null) f.SimulateOffline = false; }

        [MenuItem("Naija Streets/UI/Test: slow network (10 s)")]
        private static void TestSlow() { var f = Fake(); if (f != null) f.LatencySeconds = 10f; }

        [MenuItem("Naija Streets/UI/Test: normal network")]
        private static void TestFast() { var f = Fake(); if (f != null) f.LatencySeconds = 0.6f; }

        [MenuItem("Naija Streets/UI/Test: offer 3 prompts")]
        private static void TestPrompts()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Enter Play mode first."); return; }
            NLPrompts.Offer(new NLPromptOffer { Id = "t1", Verb = "Take over", Subject = "Danfo conductor", Icon = "bus",
                OnAccept = () => NLToasts.Show("t1", NLToastKind.Job, "You be conductor now", "Shout the route, collect the fare") });
            NLPrompts.Offer(new NLPromptOffer { Id = "t2", Verb = "Buy jollof", Subject = "Mama Put, ₦1,200", Icon = "bowl",
                OnAccept = () => TestSpend() });
            NLPrompts.Offer(new NLPromptOffer { Id = "t3", Kind = NLPromptKind.Other, Verb = "Clock in", Subject = "Pawa too low", Icon = "lock",
                Explain = "Pawa too low. Chop first." });
            NLPhoneNext.Current = ("Next: find Musa", "Oshodi park");
        }

        [MenuItem("Naija Streets/UI/Test: clear prompts")]
        private static void TestClearPrompts() { NLPrompts.Withdraw("t1"); NLPrompts.Withdraw("t2"); NLPrompts.Withdraw("t3"); }

        [MenuItem("Naija Streets/UI/Test: low vitals")]
        private static void TestLowVitals() => NLHud.SetVitals(0.18f, 0.5f, 0.2f);

        private static NLEditorFakeWallet Fake()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Enter Play mode first."); return null; }
            if (NLEditorFakeWallet.Current == null) Debug.LogWarning("The fake wallet is off because a real wallet source is set.");
            return NLEditorFakeWallet.Current;
        }
    }
}
