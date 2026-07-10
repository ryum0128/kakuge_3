using UnityEngine;
using UnityEngine.UI;

namespace FightingGameBase
{
    public class HUDManager : MonoBehaviour
    {
        public static HUDManager Instance { get; private set; }

        private CharacterBase player1;
        private CharacterBase player2;

        // UI References
        private Canvas canvas;

        // P1 UI elements (RectTransforms for scaling width)
        private RectTransform p1HpBarRect;
        private RectTransform p1PostureBarRect;
        private RectTransform p1ManaBarRect;
        private Text p1NameText;

        // P2 UI elements (RectTransforms for scaling width)
        private RectTransform p2HpBarRect;
        private RectTransform p2PostureBarRect;
        private RectTransform p2ManaBarRect;
        private Text p2NameText;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            FindPlayers();
            CreateHUD();
        }

        void FindPlayers()
        {
            CharacterBase[] characters = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            
            player1 = null;
            player2 = null;

            // Try mapping by playerID first
            foreach (CharacterBase c in characters)
            {
                if (c.playerID == 1) player1 = c;
                else if (c.playerID == 2) player2 = c;
            }

            // Fallback: If player mapping by ID is incomplete, assign by array index
            if (player1 == null || player2 == null)
            {
                if (characters.Length > 0) player1 = characters[0];
                if (characters.Length > 1) player2 = characters[1];
            }
        }

        private Font GetSafeFont()
        {
            Font f = null;

            // Try LegacyRuntime.ttf first (recommended by newer Unity versions)
            try
            {
                f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (System.Exception) { }

            // Try other common builtin fonts with exception safety
            if (f == null)
            {
                try
                {
                    f = Resources.GetBuiltinResource<Font>("LiberationSans.ttf");
                }
                catch (System.Exception) { }
            }

            if (f == null)
            {
                try
                {
                    f = Resources.GetBuiltinResource<Font>("LiberationSans-Regular.ttf");
                }
                catch (System.Exception) { }
            }

            if (f == null)
            {
                try
                {
                    f = Resources.GetBuiltinResource<Font>("Liberation Sans.ttf");
                }
                catch (System.Exception) { }
            }

            // Fallback for older Unity versions
            if (f == null)
            {
                try
                {
                    f = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch (System.Exception) { }
            }

            return f;
        }

        void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (obj == null) return;
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }

        void CreateHUD()
        {
            // 1. Create Canvas
            GameObject canvasObj = new GameObject("HUDCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Absolute topmost rendering
            
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);

            // Get robust font reference
            Font uiFont = GetSafeFont();

            // 2. Create P1 HUD Panel (Left Top)
            GameObject p1Panel = new GameObject("P1_Panel");
            p1Panel.transform.SetParent(canvasObj.transform, false);
            RectTransform p1Rect = p1Panel.AddComponent<RectTransform>();
            p1Rect.anchorMin = new Vector2(0, 1);
            p1Rect.anchorMax = new Vector2(0, 1);
            p1Rect.pivot = new Vector2(0, 1);
            p1Rect.anchoredPosition = new Vector2(25, -25);
            p1Rect.sizeDelta = new Vector2(280, 120);

            // Add panel background (solid dark background to ensure visibility)
            Image p1Bg = p1Panel.AddComponent<Image>();
            p1Bg.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);

            // P1 Player Name Text
            p1NameText = CreateText(p1Panel, "P1_Name", "PLAYER 1", new Vector2(15, -15), 16, Color.white, uiFont, FontStyle.Bold);

            // P1 HP Bar
            p1HpBarRect = CreateProgressBar(p1Panel, "HP_Bar", new Vector2(15, -45), new Vector2(250, 15), new Color(0.2f, 0.9f, 0.3f, 0.9f));
            CreateLabel(p1Panel, "HP_Label", "HP", new Vector2(15, -45), 10, Color.white, uiFont);

            // P1 Posture Bar
            p1PostureBarRect = CreateProgressBar(p1Panel, "Posture_Bar", new Vector2(15, -68), new Vector2(250, 10), new Color(1f, 0.8f, 0f, 0.9f));
            CreateLabel(p1Panel, "Posture_Label", "POSTURE", new Vector2(15, -68), 8, Color.white, uiFont);

            // P1 Mana Bar
            p1ManaBarRect = CreateProgressBar(p1Panel, "Mana_Bar", new Vector2(15, -88), new Vector2(250, 10), new Color(0f, 0.6f, 1f, 0.9f));
            CreateLabel(p1Panel, "Mana_Label", "MANA", new Vector2(15, -88), 8, Color.white, uiFont);


            // 3. Create P2 HUD Panel (Right Top)
            GameObject p2Panel = new GameObject("P2_Panel");
            p2Panel.transform.SetParent(canvasObj.transform, false);
            RectTransform p2Rect = p2Panel.AddComponent<RectTransform>();
            p2Rect.anchorMin = new Vector2(1, 1);
            p2Rect.anchorMax = new Vector2(1, 1);
            p2Rect.pivot = new Vector2(1, 1);
            p2Rect.anchoredPosition = new Vector2(-25, -25);
            p2Rect.sizeDelta = new Vector2(280, 120);

            // Add panel background
            Image p2Bg = p2Panel.AddComponent<Image>();
            p2Bg.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);

            // P2 Player Name Text
            p2NameText = CreateText(p2Panel, "P2_Name", "PLAYER 2", new Vector2(-15, -15), 16, Color.white, uiFont, FontStyle.Bold, TextAnchor.UpperRight);

            // P2 HP Bar
            p2HpBarRect = CreateProgressBar(p2Panel, "HP_Bar", new Vector2(-15, -45), new Vector2(250, 15), new Color(0.2f, 0.9f, 0.3f, 0.9f), true);
            CreateLabel(p2Panel, "HP_Label", "HP", new Vector2(-15, -45), 10, Color.white, uiFont, TextAnchor.UpperRight);

            // P2 Posture Bar
            p2PostureBarRect = CreateProgressBar(p2Panel, "Posture_Bar", new Vector2(-15, -68), new Vector2(250, 10), new Color(1f, 0.8f, 0f, 0.9f), true);
            CreateLabel(p2Panel, "Posture_Label", "POSTURE", new Vector2(-15, -68), 8, Color.white, uiFont, TextAnchor.UpperRight);

            // P2 Mana Bar
            p2ManaBarRect = CreateProgressBar(p2Panel, "Mana_Bar", new Vector2(-15, -88), new Vector2(250, 10), new Color(0f, 0.6f, 1f, 0.9f), true);
            CreateLabel(p2Panel, "Mana_Label", "MANA", new Vector2(-15, -88), 8, Color.white, uiFont, TextAnchor.UpperRight);

            // Forcibly apply UI Layer (Layer 5) recursively
            SetLayerRecursively(canvasObj, 5);
        }

        Text CreateText(GameObject parent, string name, string defaultText, Vector2 pos, int fontSize, Color color, Font uiFont, FontStyle style = FontStyle.Normal, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            GameObject txtObj = new GameObject(name);
            txtObj.transform.SetParent(parent.transform, false);
            RectTransform rect = txtObj.AddComponent<RectTransform>();
            
            if (alignment == TextAnchor.UpperRight)
            {
                rect.anchorMin = new Vector2(1, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(1, 1);
            }
            else
            {
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
            }
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(200, 30);

            if (uiFont != null)
            {
                Text text = txtObj.AddComponent<Text>();
                text.font = uiFont;
                text.text = defaultText;
                text.fontSize = fontSize;
                text.color = color;
                text.fontStyle = style;
                text.alignment = alignment;
                return text;
            }

            return null;
        }

        void CreateLabel(GameObject parent, string labelText, string text, Vector2 pos, int fontSize, Color color, Font uiFont, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            GameObject labelObj = new GameObject(labelText);
            labelObj.transform.SetParent(parent.transform, false);
            RectTransform rect = labelObj.AddComponent<RectTransform>();
            
            if (alignment == TextAnchor.UpperRight)
            {
                rect.anchorMin = new Vector2(1, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = pos + new Vector2(-5, -2);
            }
            else
            {
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = pos + new Vector2(5, -2);
            }
            rect.sizeDelta = new Vector2(100, 20);

            if (uiFont != null)
            {
                Text t = labelObj.AddComponent<Text>();
                t.font = uiFont;
                t.text = text;
                t.fontSize = fontSize;
                t.color = new Color(color.r, color.g, color.b, 0.7f);
                t.fontStyle = FontStyle.Bold;
                t.alignment = alignment;
            }
        }

        RectTransform CreateProgressBar(GameObject parent, string name, Vector2 pos, Vector2 size, Color color, bool alignRight = false)
        {
            // Outer container (Background)
            GameObject barBgObj = new GameObject(name + "_Bg");
            barBgObj.transform.SetParent(parent.transform, false);
            RectTransform bgRect = barBgObj.AddComponent<RectTransform>();
            
            if (alignRight)
            {
                bgRect.anchorMin = new Vector2(1, 1);
                bgRect.anchorMax = new Vector2(1, 1);
                bgRect.pivot = new Vector2(1, 1);
            }
            else
            {
                bgRect.anchorMin = new Vector2(0, 1);
                bgRect.anchorMax = new Vector2(0, 1);
                bgRect.pivot = new Vector2(0, 1);
            }
            bgRect.anchoredPosition = pos;
            bgRect.sizeDelta = size;

            Image bgImage = barBgObj.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.05f, 0.7f); // Dark solid background

            // Inner fill (Active part)
            GameObject barFillObj = new GameObject(name + "_Fill");
            barFillObj.transform.SetParent(barBgObj.transform, false);
            RectTransform fillRect = barFillObj.AddComponent<RectTransform>();
            
            // Align anchors for horizontal scaling
            if (alignRight)
            {
                fillRect.anchorMin = new Vector2(1, 0); // Bottom Right
                fillRect.anchorMax = new Vector2(1, 1); // Top Right
                fillRect.pivot = new Vector2(1, 0.5f);  // Pivot Right
            }
            else
            {
                fillRect.anchorMin = new Vector2(0, 0); // Bottom Left
                fillRect.anchorMax = new Vector2(0, 1); // Top Left
                fillRect.pivot = new Vector2(0, 0.5f);  // Pivot Left
            }
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(size.x, 0f);

            Image fillImage = barFillObj.AddComponent<Image>();
            fillImage.color = color; // Solid color

            return fillRect;
        }

        void SetBarValue(RectTransform barRect, float percent, float maxWidth)
        {
            if (barRect == null) return;
            float targetWidth = maxWidth * Mathf.Clamp01(percent);
            barRect.sizeDelta = new Vector2(targetWidth, 0f);
        }

        void Update()
        {
            if (player1 == null || player2 == null)
            {
                FindPlayers();
            }

            // Update P1 UI
            if (player1 != null)
            {
                if (p1NameText != null)
                {
                    p1NameText.text = player1.gameObject.name.Replace("(Clone)", "").ToUpper() + " (P1)";
                }
                
                float maxHp = player1.maxHP > 0 ? player1.maxHP : 100f;
                SetBarValue(p1HpBarRect, (float)player1.currentHP / maxHp, 250f);
                
                float maxPos = player1.maxPosture > 0 ? player1.maxPosture : 100f;
                SetBarValue(p1PostureBarRect, player1.currentPosture / maxPos, 250f);
                
                float maxMana = player1.maxMana > 0 ? player1.maxMana : 100f;
                SetBarValue(p1ManaBarRect, player1.currentMana / maxMana, 250f);
            }
            else
            {
                SetBarValue(p1HpBarRect, 0f, 250f);
                SetBarValue(p1PostureBarRect, 0f, 250f);
                SetBarValue(p1ManaBarRect, 0f, 250f);
            }

            // Update P2 UI
            if (player2 != null)
            {
                if (p2NameText != null)
                {
                    p2NameText.text = player2.gameObject.name.Replace("(Clone)", "").ToUpper() + " (P2)";
                }
                
                float maxHp = player2.maxHP > 0 ? player2.maxHP : 100f;
                SetBarValue(p2HpBarRect, (float)player2.currentHP / maxHp, 250f);
                
                float maxPos = player2.maxPosture > 0 ? player2.maxPosture : 100f;
                SetBarValue(p2PostureBarRect, player2.currentPosture / maxPos, 250f);
                
                float maxMana = player2.maxMana > 0 ? player2.maxMana : 100f;
                SetBarValue(p2ManaBarRect, player2.currentMana / maxMana, 250f);
            }
            else
            {
                SetBarValue(p2HpBarRect, 0f, 250f);
                SetBarValue(p2PostureBarRect, 0f, 250f);
                SetBarValue(p2ManaBarRect, 0f, 250f);
            }
        }
    }
}
