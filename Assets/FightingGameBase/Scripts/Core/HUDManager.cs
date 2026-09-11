using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem;

namespace FightingGameBase
{
    public class HUDManager : MonoBehaviour
    {
        public static HUDManager Instance { get; private set; }

        private CharacterBase player1;
        private CharacterBase player2;

        // UI Canvas References
        private Canvas canvas;
        private Font uiFont;

        // Progress bar class for smooth management
        private class ProgressBarUI
        {
            public string name;
            public RectTransform bgRect;
            public RectTransform catchUpRect;
            public RectTransform fillRect;
            public Image fillImage;
            public Image catchUpImage;
            public float currentPercent = -1f;
            public float catchUpPercent = -1f;
            public float maxWidth;
        }

        private ProgressBarUI p1HpBar;
        private ProgressBarUI p1PostureBar;
        private ProgressBarUI p1ManaBar;

        private ProgressBarUI p2HpBar;
        private ProgressBarUI p2PostureBar;
        private ProgressBarUI p2ManaBar;

        // Text references
        private Text p1NameText;
        private Text p2NameText;
        private Text p1HpValueText;
        private Text p2HpValueText;

        // Timer
        private Text timerText;
        private float roundTimeRemaining = 99f;
        private bool isTimerActive = true;

        // Stock / Lives icons
        private GameObject p1StockContainer;
        private GameObject p2StockContainer;
        private Text respawnNotifyText;
        private Coroutine respawnNotifyCoroutine;

        // Combos
        private int p1ComboCount = 0;
        private float p1LastHitTime = -10f;
        private int p2ComboCount = 0;
        private float p2LastHitTime = -10f;
        private float comboTimeout = 1.5f;

        private Text p1ComboText;
        private Coroutine p1ComboCoroutine;

        private Sprite circleSprite;
        private Sprite GetCircleSprite()
        {
            if (circleSprite != null) return circleSprite;

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            float center = (size - 1) / 2f;
            float radius = size / 2f - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 1f - dist);
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();

            circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return circleSprite;
        }

        private Text p2ComboText;
        private Coroutine p2ComboCoroutine;
        private Coroutine shakeCoroutine;
        private Coroutine activeHitStopCoroutine;
        private float originalTimeScale = 1f;
        private bool isHitStopping = false;

        // Cache gradients to avoid memory leaks
        private Sprite p1HpGradient;
        private Sprite p2HpGradient;
        private Sprite p1PostureGradient;
        private Sprite p2PostureGradient;
        private Sprite p1ManaGradient;
        private Sprite p2ManaGradient;

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

            // Auto-instantiate StunGaugeUI if missing in the scene
            if (FindAnyObjectByType<StunGaugeUI>() == null)
            {
                GameObject stunGo = new GameObject("StunGaugeUI");
                stunGo.AddComponent<StunGaugeUI>();
            }

            // Auto-instantiate WhipSkillGaugeUI if a whip character exists and UI is missing
            if (FindAnyObjectByType<WhipCharacter>() != null && FindAnyObjectByType<WhipSkillGaugeUI>() == null)
            {
                GameObject whipGaugeGo = new GameObject("WhipSkillGaugeUI");
                whipGaugeGo.AddComponent<WhipSkillGaugeUI>();
            }
        }

        void OnDestroy()
        {
            // Clean up programmatic textures to prevent memory leaks
            if (p1HpGradient != null) { Destroy(p1HpGradient.texture); Destroy(p1HpGradient); }
            if (p2HpGradient != null) { Destroy(p2HpGradient.texture); Destroy(p2HpGradient); }
            if (p1PostureGradient != null) { Destroy(p1PostureGradient.texture); Destroy(p1PostureGradient); }
            if (p2PostureGradient != null) { Destroy(p2PostureGradient.texture); Destroy(p2PostureGradient); }
            if (p1ManaGradient != null) { Destroy(p1ManaGradient.texture); Destroy(p1ManaGradient); }
            if (p2ManaGradient != null) { Destroy(p2ManaGradient.texture); Destroy(p2ManaGradient); }
            if (circleSprite != null) { Destroy(circleSprite.texture); Destroy(circleSprite); }
        }

        void FindPlayers()
        {
            CharacterBase[] characters = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            
            player1 = null;
            player2 = null;

            foreach (CharacterBase c in characters)
            {
                if (c.playerID == 1) player1 = c;
                else if (c.playerID == 2) player2 = c;
            }

            if (player1 == null || player2 == null)
            {
                if (characters.Length > 0) player1 = characters[0];
                if (characters.Length > 1) player2 = characters[1];
            }
        }

        private Font GetSafeFont()
        {
            Font f = null;
            string[] fontNames = { "LegacyRuntime.ttf", "LiberationSans.ttf", "LiberationSans-Regular.ttf", "Liberation Sans.ttf", "Arial.ttf" };
            foreach (var fontName in fontNames)
            {
                try
                {
                    f = Resources.GetBuiltinResource<Font>(fontName);
                    if (f != null) break;
                }
                catch (System.Exception) { }
            }
            return f;
        }

        private Sprite CreateGradientSprite(Color leftColor, Color rightColor)
        {
            Texture2D tex = new Texture2D(128, 16);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    Color col = Color.Lerp(leftColor, rightColor, (float)x / 127f);
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 128, 16), new Vector2(0.5f, 0.5f));
        }

        private void InitializeGradients()
        {
            // P1 HP Bar: Green at high health (right, near center) to yellow/red at outer edge (left)
            p1HpGradient = CreateGradientSprite(new Color(0.1f, 0.5f, 0.15f), new Color(0.2f, 1.0f, 0.4f));

            // P2 HP Bar: Green at high health (left, near center) to yellow/red at outer edge (right)
            p2HpGradient = CreateGradientSprite(new Color(0.2f, 1.0f, 0.4f), new Color(0.1f, 0.5f, 0.15f));

            // Posture: Orange to Yellow
            p1PostureGradient = CreateGradientSprite(new Color(0.9f, 0.35f, 0.05f), new Color(1.0f, 0.85f, 0.15f));
            p2PostureGradient = CreateGradientSprite(new Color(1.0f, 0.85f, 0.15f), new Color(0.9f, 0.35f, 0.05f));

            // Mana: Dark Blue to Bright Cyan
            p1ManaGradient = CreateGradientSprite(new Color(0.05f, 0.25f, 0.85f), new Color(0.1f, 0.75f, 1.0f));
            p2ManaGradient = CreateGradientSprite(new Color(0.1f, 0.75f, 1.0f), new Color(0.05f, 0.25f, 0.85f));
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
            canvas.sortingOrder = 100;
            
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);

            uiFont = GetSafeFont();
            InitializeGradients();

            // 2. Create Unified Top Header Panel
            GameObject hudHeader = new GameObject("HUDHeader");
            hudHeader.transform.SetParent(canvasObj.transform, false);
            RectTransform headerRect = hudHeader.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1.0f);
            headerRect.anchorMax = new Vector2(0.5f, 1.0f);
            headerRect.pivot = new Vector2(0.5f, 1.0f);
            headerRect.anchoredPosition = new Vector2(0, 0);
            headerRect.sizeDelta = new Vector2(800, 110);

            // Glassmorphic translucent dark background
            Image headerBg = hudHeader.AddComponent<Image>();
            headerBg.color = new Color(0.08f, 0.08f, 0.12f, 0.65f);

            // Thin metallic bottom border line
            GameObject bottomBorder = new GameObject("Header_BottomBorder");
            bottomBorder.transform.SetParent(hudHeader.transform, false);
            RectTransform borderLineRect = bottomBorder.AddComponent<RectTransform>();
            borderLineRect.anchorMin = new Vector2(0, 0);
            borderLineRect.anchorMax = new Vector2(1, 0);
            borderLineRect.pivot = new Vector2(0.5f, 0f);
            borderLineRect.anchoredPosition = Vector2.zero;
            borderLineRect.sizeDelta = new Vector2(0, 2);
            Image borderLineImg = bottomBorder.AddComponent<Image>();
            borderLineImg.color = new Color(0.7f, 0.7f, 0.75f, 0.8f);

            // 3. Create Center Timer Panel
            GameObject timerPanel = new GameObject("Timer_Panel");
            timerPanel.transform.SetParent(hudHeader.transform, false);
            RectTransform timerPanelRect = timerPanel.AddComponent<RectTransform>();
            timerPanelRect.anchorMin = new Vector2(0.5f, 1.0f);
            timerPanelRect.anchorMax = new Vector2(0.5f, 1.0f);
            timerPanelRect.pivot = new Vector2(0.5f, 1.0f);
            timerPanelRect.anchoredPosition = new Vector2(0, -15);
            timerPanelRect.sizeDelta = new Vector2(65, 65);

            // Circular frame for timer
            Image timerFrame = timerPanel.AddComponent<Image>();
            timerFrame.sprite = GetCircleSprite();
            timerFrame.color = new Color(0.12f, 0.12f, 0.16f, 0.9f);
            
            // Thin border ring for timer circle
            GameObject timerRing = new GameObject("Timer_Ring");
            timerRing.transform.SetParent(timerPanel.transform, false);
            RectTransform ringRect = timerRing.AddComponent<RectTransform>();
            ringRect.anchorMin = Vector2.zero;
            ringRect.anchorMax = Vector2.one;
            ringRect.sizeDelta = new Vector2(4, 4);
            Image ringImg = timerRing.AddComponent<Image>();
            ringImg.sprite = GetCircleSprite();
            ringImg.color = new Color(0.7f, 0.7f, 0.75f, 0.8f);
            timerRing.transform.SetAsFirstSibling();

            timerText = CreateTextWithShadow(timerPanel, "Timer_Text", "99", new Vector2(0, 0), 28, new Color(1.0f, 0.85f, 0.2f), uiFont, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 60);
            CreateTextWithShadow(timerPanel, "Round_Label", "ROUND 1", new Vector2(0, -42), 9, new Color(0.7f, 0.7f, 0.75f, 0.85f), uiFont, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 80);

            // 4. Create P1 HUD elements (Left side, pivot grows leftwards from center)
            p1NameText = CreateTextWithShadow(hudHeader, "P1_Name", "PLAYER 1", new Vector2(-320, -12), 22, Color.white, uiFont, TextAnchor.MiddleLeft, new Vector2(0.5f, 1.0f), new Vector2(0.0f, 1.0f));
            p1HpBar = CreateProgressBar(hudHeader, "P1_HP_Bar", new Vector2(-325, -60), new Vector2(280, 20), p1HpGradient, false);
            p1HpValueText = CreateTextWithShadow(hudHeader, "P1_HP_Value", "100%", new Vector2(-50, -60), 12, Color.white, uiFont, TextAnchor.MiddleRight, new Vector2(0.5f, 1.0f), new Vector2(1.0f, 0.5f), 80);
            p1PostureBar = CreateProgressBar(hudHeader, "P1_Posture_Bar", new Vector2(-325, -83), new Vector2(280, 8), p1PostureGradient, false);
            p1ManaBar = CreateProgressBar(hudHeader, "P1_Mana_Bar", new Vector2(-325, -94), new Vector2(280, 8), p1ManaGradient, false);

            CreateLabel(hudHeader, "P1_HP_Label", "HP", new Vector2(-320, -60), 9, new Color(1f, 1f, 1f, 0.75f), uiFont, TextAnchor.MiddleLeft, new Vector2(0.5f, 1.0f));
            CreateLabel(hudHeader, "P1_Posture_Label", "POSTURE", new Vector2(-320, -83), 7, new Color(1f, 1f, 1f, 0.75f), uiFont, TextAnchor.MiddleLeft, new Vector2(0.5f, 1.0f));
            CreateLabel(hudHeader, "P1_Mana_Label", "MANA", new Vector2(-320, -94), 7, new Color(1f, 1f, 1f, 0.75f), uiFont, TextAnchor.MiddleLeft, new Vector2(0.5f, 1.0f));

            // 5. Create P2 HUD elements (Right side, pivot grows rightwards from center)
            p2NameText = CreateTextWithShadow(hudHeader, "P2_Name", "PLAYER 2", new Vector2(320, -12), 22, Color.white, uiFont, TextAnchor.MiddleRight, new Vector2(0.5f, 1.0f), new Vector2(1.0f, 1.0f));
            p2HpBar = CreateProgressBar(hudHeader, "P2_HP_Bar", new Vector2(325, -60), new Vector2(280, 20), p2HpGradient, true);
            p2HpValueText = CreateTextWithShadow(hudHeader, "P2_HP_Value", "100%", new Vector2(50, -60), 12, Color.white, uiFont, TextAnchor.MiddleLeft, new Vector2(0.5f, 1.0f), new Vector2(0.0f, 0.5f), 80);
            p2PostureBar = CreateProgressBar(hudHeader, "P2_Posture_Bar", new Vector2(325, -83), new Vector2(280, 8), p2PostureGradient, true);
            p2ManaBar = CreateProgressBar(hudHeader, "P2_Mana_Bar", new Vector2(325, -94), new Vector2(280, 8), p2ManaGradient, true);

            CreateLabel(hudHeader, "P2_HP_Label", "HP", new Vector2(320, -60), 9, new Color(1f, 1f, 1f, 0.75f), uiFont, TextAnchor.MiddleRight, new Vector2(0.5f, 1.0f));
            CreateLabel(hudHeader, "P2_Posture_Label", "POSTURE", new Vector2(320, -83), 7, new Color(1f, 1f, 1f, 0.75f), uiFont, TextAnchor.MiddleRight, new Vector2(0.5f, 1.0f));
            CreateLabel(hudHeader, "P2_Mana_Label", "MANA", new Vector2(320, -94), 7, new Color(1f, 1f, 1f, 0.75f), uiFont, TextAnchor.MiddleRight, new Vector2(0.5f, 1.0f));

            // 6. Create Stock / Lives icons
            p1StockContainer = CreateStockIcons(hudHeader, "P1_Stocks", new Vector2(-320, -38), false);
            p2StockContainer = CreateStockIcons(hudHeader, "P2_Stocks", new Vector2(320, -38), true);

            // 7. Create Combo Counter Text Fields
            p1ComboText = CreateTextWithShadow(canvasObj, "P1_Combo", "", new Vector2(-280, -140), 24, new Color(1.0f, 0.85f, 0.2f), uiFont, TextAnchor.MiddleLeft, new Vector2(0.5f, 1.0f), new Vector2(0f, 1.0f));
            p1ComboText.gameObject.SetActive(false);

            p2ComboText = CreateTextWithShadow(canvasObj, "P2_Combo", "", new Vector2(280, -140), 24, new Color(1.0f, 0.85f, 0.2f), uiFont, TextAnchor.MiddleRight, new Vector2(0.5f, 1.0f), new Vector2(1f, 1.0f));
            p2ComboText.gameObject.SetActive(false);

            // 8. Respawn notification text (centered, initially hidden)
            respawnNotifyText = CreateTextWithShadow(canvasObj, "Respawn_Notify", "", new Vector2(0, -200), 36, new Color(0.2f, 1f, 0.5f), uiFont, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 500);
            respawnNotifyText.gameObject.SetActive(false);

            // Force UI Layer (5) recursively
            SetLayerRecursively(canvasObj, 5);
        }

        Text CreateTextWithShadow(GameObject parent, string name, string defaultText, Vector2 pos, int fontSize, Color color, Font uiFont, TextAnchor alignment, Vector2 anchor, Vector2 pivot, float width = 300)
        {
            GameObject txtObj = new GameObject(name);
            txtObj.transform.SetParent(parent.transform, false);
            RectTransform rect = txtObj.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(width, fontSize + 12);

            Text text = txtObj.AddComponent<Text>();
            text.font = uiFont;
            text.text = defaultText;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;

            Shadow shadow = txtObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return text;
        }

        void CreateLabel(GameObject parent, string labelName, string text, Vector2 pos, int fontSize, Color color, Font uiFont, TextAnchor alignment, Vector2 anchor)
        {
            GameObject labelObj = new GameObject(labelName);
            labelObj.transform.SetParent(parent.transform, false);
            RectTransform rect = labelObj.AddComponent<RectTransform>();
            
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = (alignment == TextAnchor.MiddleLeft) ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            rect.anchoredPosition = pos + ((alignment == TextAnchor.MiddleLeft) ? new Vector2(5, -2) : new Vector2(-5, -2));
            rect.sizeDelta = new Vector2(100, fontSize + 8);

            Text t = labelObj.AddComponent<Text>();
            t.font = uiFont;
            t.text = text;
            t.fontSize = fontSize;
            t.color = new Color(color.r, color.g, color.b, 0.75f);
            t.fontStyle = FontStyle.Bold;
            t.alignment = alignment;

            Shadow shadow = labelObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        private ProgressBarUI CreateProgressBar(GameObject parent, string name, Vector2 pos, Vector2 size, Sprite gradientSprite, bool isP2)
        {
            ProgressBarUI bar = new ProgressBarUI();
            bar.name = name;
            bar.maxWidth = size.x;

            // 0. Soft outer glow behind HP bars for rich sci-fi aesthetics
            if (name.Contains("HP_Bar"))
            {
                GameObject glowObj = new GameObject(name + "_Glow");
                glowObj.transform.SetParent(parent.transform, false);
                RectTransform glowRect = glowObj.AddComponent<RectTransform>();
                glowRect.anchorMin = isP2 ? new Vector2(0.5f, 1.0f) : new Vector2(0.5f, 1.0f);
                glowRect.anchorMax = isP2 ? new Vector2(0.5f, 1.0f) : new Vector2(0.5f, 1.0f);
                glowRect.pivot = isP2 ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
                glowRect.anchoredPosition = pos;
                glowRect.sizeDelta = size + new Vector2(12, 12);
                Image glowImg = glowObj.AddComponent<Image>();
                glowImg.color = isP2 ? new Color(1f, 0.35f, 0.1f, 0.2f) : new Color(0.1f, 0.65f, 1f, 0.2f);
            }

            // 1. Frame Border
            GameObject borderObj = new GameObject(name + "_Border");
            borderObj.transform.SetParent(parent.transform, false);
            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = isP2 ? new Vector2(0.5f, 1.0f) : new Vector2(0.5f, 1.0f);
            borderRect.anchorMax = isP2 ? new Vector2(0.5f, 1.0f) : new Vector2(0.5f, 1.0f);
            borderRect.pivot = isP2 ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            borderRect.anchoredPosition = pos;
            borderRect.sizeDelta = size + new Vector2(4, 4);

            Image borderImg = borderObj.AddComponent<Image>();
            if (name.Contains("HP_Bar"))
            {
                borderImg.color = isP2 ? new Color(1f, 0.45f, 0.15f, 0.85f) : new Color(0.15f, 0.75f, 1f, 0.85f); // Neon Cyan for P1, Fire Orange for P2!
            }
            else
            {
                borderImg.color = new Color(0.2f, 0.22f, 0.25f, 0.85f); // Sleek border frame
            }

            // 2. Background
            GameObject bgObj = new GameObject(name + "_Bg");
            bgObj.transform.SetParent(borderObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = size;

            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.06f, 0.9f);
            bar.bgRect = bgRect;

            // 3. CatchUp Fill
            GameObject catchUpObj = new GameObject(name + "_CatchUp");
            catchUpObj.transform.SetParent(bgObj.transform, false);
            RectTransform catchUpRect = catchUpObj.AddComponent<RectTransform>();
            if (isP2)
            {
                catchUpRect.anchorMin = new Vector2(1, 0);
                catchUpRect.anchorMax = new Vector2(1, 1);
                catchUpRect.pivot = new Vector2(1, 0.5f);
            }
            else
            {
                catchUpRect.anchorMin = new Vector2(0, 0);
                catchUpRect.anchorMax = new Vector2(0, 1);
                catchUpRect.pivot = new Vector2(0, 0.5f);
            }
            catchUpRect.anchoredPosition = Vector2.zero;
            catchUpRect.sizeDelta = new Vector2(size.x, 0f);

            Image catchUpImg = catchUpObj.AddComponent<Image>();
            catchUpImg.color = new Color(0.85f, 0.2f, 0.12f, 0.9f); // Red warning color
            bar.catchUpRect = catchUpRect;
            bar.catchUpImage = catchUpImg;

            // 4. Gradient Fill
            GameObject fillObj = new GameObject(name + "_Fill");
            fillObj.transform.SetParent(bgObj.transform, false);
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            if (isP2)
            {
                fillRect.anchorMin = new Vector2(1, 0);
                fillRect.anchorMax = new Vector2(1, 1);
                fillRect.pivot = new Vector2(1, 0.5f);
            }
            else
            {
                fillRect.anchorMin = new Vector2(0, 0);
                fillRect.anchorMax = new Vector2(0, 1);
                fillRect.pivot = new Vector2(0, 0.5f);
            }
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(size.x, 0f);

            Image fillImg = fillObj.AddComponent<Image>();
            if (gradientSprite != null)
            {
                fillImg.sprite = gradientSprite;
                fillImg.type = Image.Type.Simple;
                fillImg.color = Color.white;
            }
            else
            {
                fillImg.color = Color.green;
            }
            bar.fillRect = fillRect;
            bar.fillImage = fillImg;

            // 5. Mana Bar Dividers (25, 50, 75 の目盛り縦線)
            if (name.Contains("Mana"))
            {
                float[] thresholds = { 0.25f, 0.50f, 0.75f };
                foreach (float t in thresholds)
                {
                    int val = Mathf.RoundToInt(t * 100);

                    // 線の背後シャドウ（黒、幅2.5pxでくっきり境界を作る）
                    GameObject shadowLine = new GameObject($"{name}_Line_Shadow_{val}");
                    shadowLine.transform.SetParent(bgObj.transform, false);
                    RectTransform sRect = shadowLine.AddComponent<RectTransform>();
                    if (isP2)
                    {
                        sRect.anchorMin = new Vector2(1, 0);
                        sRect.anchorMax = new Vector2(1, 1);
                        sRect.pivot = new Vector2(0.5f, 0.5f);
                        sRect.anchoredPosition = new Vector2(-t * size.x, 0);
                    }
                    else
                    {
                        sRect.anchorMin = new Vector2(0, 0);
                        sRect.anchorMax = new Vector2(0, 1);
                        sRect.pivot = new Vector2(0.5f, 0.5f);
                        sRect.anchoredPosition = new Vector2(t * size.x, 0);
                    }
                    sRect.sizeDelta = new Vector2(2.5f, 2f);
                    Image sImg = shadowLine.AddComponent<Image>();
                    sImg.color = new Color(0.02f, 0.02f, 0.05f, 0.95f);

                    // 前面ハイライト縦線（発光シアンホワイト）
                    GameObject lineObj = new GameObject($"{name}_Line_{val}");
                    lineObj.transform.SetParent(bgObj.transform, false);
                    RectTransform lRect = lineObj.AddComponent<RectTransform>();
                    if (isP2)
                    {
                        lRect.anchorMin = new Vector2(1, 0);
                        lRect.anchorMax = new Vector2(1, 1);
                        lRect.pivot = new Vector2(0.5f, 0.5f);
                        lRect.anchoredPosition = new Vector2(-t * size.x, 0);
                    }
                    else
                    {
                        lRect.anchorMin = new Vector2(0, 0);
                        lRect.anchorMax = new Vector2(0, 1);
                        lRect.pivot = new Vector2(0.5f, 0.5f);
                        lRect.anchoredPosition = new Vector2(t * size.x, 0);
                    }
                    lRect.sizeDelta = new Vector2(1.2f, 0f);
                    Image lImg = lineObj.AddComponent<Image>();
                    lImg.color = (t == 0.5f) ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.85f, 0.95f, 1f, 0.85f);
                }
            }

            return bar;
        }

        /// <summary>
        /// ストック（残機）アイコンを作成する
        /// </summary>
        private GameObject CreateStockIcons(GameObject parent, string name, Vector2 pos, bool isP2)
        {
            GameObject container = new GameObject(name);
            container.transform.SetParent(parent.transform, false);
            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 1.0f);
            containerRect.anchorMax = new Vector2(0.5f, 1.0f);
            containerRect.pivot = isP2 ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            containerRect.anchoredPosition = pos;
            containerRect.sizeDelta = new Vector2(80, 16);

            // Create 3 stock circle icons
            for (int i = 0; i < 3; i++)
            {
                GameObject icon = new GameObject($"Stock_{i}");
                icon.transform.SetParent(container.transform, false);
                RectTransform iconRect = icon.AddComponent<RectTransform>();
                float xOffset = isP2 ? -(i * 18f) : (i * 18f);
                iconRect.anchorMin = isP2 ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
                iconRect.anchorMax = isP2 ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(xOffset, 0);
                iconRect.sizeDelta = new Vector2(12, 12);

                Image iconImg = icon.AddComponent<Image>();
                iconImg.sprite = GetCircleSprite();
                iconImg.color = isP2 ? new Color(1f, 0.5f, 0.15f, 1f) : new Color(0.15f, 0.75f, 1f, 1f);
            }

            return container;
        }

        /// <summary>
        /// ストックアイコンの表示を更新する
        /// </summary>
        private void UpdateStockIcons(GameObject container, int remainingLives, bool isP2)
        {
            if (container == null) return;

            for (int i = 0; i < container.transform.childCount; i++)
            {
                Image img = container.transform.GetChild(i).GetComponent<Image>();
                if (img != null)
                {
                    if (i < remainingLives)
                    {
                        // 残機あり: 明るい色
                        img.color = isP2 ? new Color(1f, 0.5f, 0.15f, 1f) : new Color(0.15f, 0.75f, 1f, 1f);
                    }
                    else
                    {
                        // 残機なし: 暗い色
                        img.color = new Color(0.2f, 0.2f, 0.2f, 0.4f);
                    }
                }
            }
        }

        /// <summary>
        /// リスポーン通知テキストを画面中央に表示する
        /// </summary>
        public void ShowRespawnNotification(int playerID, int livesRemaining)
        {
            if (respawnNotifyText == null) return;

            if (respawnNotifyCoroutine != null) StopCoroutine(respawnNotifyCoroutine);
            respawnNotifyCoroutine = StartCoroutine(RespawnNotifyRoutine(playerID, livesRemaining));
        }

        private IEnumerator RespawnNotifyRoutine(int playerID, int livesRemaining)
        {
            respawnNotifyText.gameObject.SetActive(true);
            respawnNotifyText.text = $"P{playerID} RESPAWN!  ×{livesRemaining}";
            respawnNotifyText.color = new Color(0.2f, 1f, 0.5f, 1f);

            // Scale pop
            float popElapsed = 0f;
            while (popElapsed < 0.15f)
            {
                popElapsed += Time.deltaTime;
                float s = Mathf.Lerp(1.8f, 1f, popElapsed / 0.15f);
                respawnNotifyText.transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            respawnNotifyText.transform.localScale = Vector3.one;

            yield return new WaitForSeconds(1.5f);

            // Fade out
            float fadeElapsed = 0f;
            while (fadeElapsed < 0.5f)
            {
                fadeElapsed += Time.deltaTime;
                Color c = respawnNotifyText.color;
                c.a = 1f - (fadeElapsed / 0.5f);
                respawnNotifyText.color = c;
                yield return null;
            }

            respawnNotifyText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 勝敗結果画面を表示する（K.O. → PLAYER X WIN!）
        /// </summary>
        public void ShowGameResult(int winnerID)
        {
            StartCoroutine(GameResultRoutine(winnerID));
        }

        private IEnumerator GameResultRoutine(int winnerID)
        {
            // --- 半透明の暗いオーバーレイ ---
            GameObject overlay = new GameObject("GameResult_Overlay");
            overlay.transform.SetParent(canvas.transform, false);
            RectTransform overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;
            Image overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0f);

            // オーバーレイをフェードイン
            float fadeInDur = 0.5f;
            float elapsed = 0f;
            while (elapsed < fadeInDur)
            {
                elapsed += Time.unscaledDeltaTime;
                overlayImg.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.6f, elapsed / fadeInDur));
                yield return null;
            }
            overlayImg.color = new Color(0f, 0f, 0f, 0.6f);

            // --- K.O. テキスト ---
            Text koText = CreateTextWithShadow(overlay, "KO_Text", "K.O.", new Vector2(0, 30), 72, new Color(1f, 0.2f, 0.15f), uiFont, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 500);

            // K.O. ポップインアニメーション
            elapsed = 0f;
            float popDur = 0.3f;
            while (elapsed < popDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / popDur;
                // バウンス感のあるイージング
                float scale;
                if (t < 0.6f)
                    scale = Mathf.Lerp(3f, 0.9f, t / 0.6f);
                else
                    scale = Mathf.Lerp(0.9f, 1f, (t - 0.6f) / 0.4f);
                koText.transform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            koText.transform.localScale = Vector3.one;

            // 画面を揺らす
            TriggerScreenShake(0.3f, 0.15f);

            yield return new WaitForSecondsRealtime(1.0f);

            // --- K.O. テキストをフェードアウト ---
            elapsed = 0f;
            float koFadeDur = 0.4f;
            while (elapsed < koFadeDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = 1f - (elapsed / koFadeDur);
                koText.color = new Color(1f, 0.2f, 0.15f, a);
                yield return null;
            }
            koText.gameObject.SetActive(false);

            // --- 勝者テキスト ---
            string winnerName = "";
            CharacterBase[] allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (CharacterBase c in allChars)
            {
                if (c.playerID == winnerID)
                {
                    winnerName = c.gameObject.name.Replace("(Clone)", "").ToUpper();
                    break;
                }
            }

            Color winColor = winnerID == 1 ? new Color(0.15f, 0.85f, 1f) : new Color(1f, 0.5f, 0.15f);
            Text winText = CreateTextWithShadow(overlay, "Winner_Text", $"PLAYER {winnerID}  WIN!", new Vector2(0, 20), 52, winColor, uiFont, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 600);
            
            // キャラ名を下に表示
            Text nameText = CreateTextWithShadow(overlay, "Winner_Name", winnerName, new Vector2(0, -30), 28, new Color(1f, 0.85f, 0.2f), uiFont, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 500);

            // 勝者テキストのスライドイン
            elapsed = 0f;
            float slideDur = 0.4f;
            while (elapsed < slideDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / slideDur;
                // 左からスライドイン
                float x = Mathf.Lerp(-400f, 0f, Mathf.SmoothStep(0f, 1f, t));
                winText.rectTransform.anchoredPosition = new Vector2(x, 20);
                // 右からスライドイン（キャラ名）
                float x2 = Mathf.Lerp(400f, 0f, Mathf.SmoothStep(0f, 1f, t));
                nameText.rectTransform.anchoredPosition = new Vector2(x2, -30);
                yield return null;
            }

            // 勝者テキストが輝くパルスアニメーション（ループ）
            // 「SPACE または R キーでキャラクターセレクトへ」案内テキスト
            Text promptText = CreateTextWithShadow(overlay, "Restart_Prompt", "PRESS [ SPACE ] OR [ R ] FOR CHARACTER SELECT", new Vector2(0, -90), 22, Color.white, uiFont, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 650);

            // クリックでも戻れるようにボタン機能も追加
            Button restartBtn = overlay.AddComponent<Button>();
            restartBtn.onClick.AddListener(() =>
            {
                if (CharacterSelectManager.Instance != null)
                {
                    CharacterSelectManager.Instance.ResetToCharacterSelect();
                }
            });

            while (true)
            {
                float pulse = 1f + Mathf.PingPong(Time.unscaledTime * 1.5f, 0.08f);
                winText.transform.localScale = new Vector3(pulse, pulse, 1f);

                if (promptText != null)
                {
                    float alpha = 0.5f + Mathf.PingPong(Time.unscaledTime * 2f, 0.5f);
                    promptText.color = new Color(1f, 1f, 1f, alpha);
                }

                // キー入力でキャラクターセレクトへ戻る
                bool restartPressed = false;
                if (Keyboard.current != null)
                {
                    restartPressed = Keyboard.current.spaceKey.wasPressedThisFrame
                                  || Keyboard.current.rKey.wasPressedThisFrame
                                  || Keyboard.current.enterKey.wasPressedThisFrame;
                }

                if (restartPressed)
                {
                    if (CharacterSelectManager.Instance != null)
                    {
                        CharacterSelectManager.Instance.ResetToCharacterSelect();
                    }
                    else
                    {
                        CharacterSelectManager.ClearAllExistingCharacters();
                        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                    }
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>
        /// HUDの状態を初期状態にリセットする
        /// </summary>
        public void ResetHUD()
        {
            StopAllCoroutines();
            if (canvas != null)
            {
                Transform overlay = canvas.transform.Find("GameResult_Overlay");
                if (overlay != null)
                {
                    Destroy(overlay.gameObject);
                }
            }
            if (respawnNotifyText != null)
            {
                respawnNotifyText.gameObject.SetActive(false);
            }

            player1 = null;
            player2 = null;
        }

        private void SetBarValue(ProgressBarUI bar, float percent)
        {
            if (bar == null) return;
            
            // Initialization check
            if (bar.currentPercent < 0f)
            {
                bar.currentPercent = percent;
                bar.catchUpPercent = percent;
            }
            else
            {
                bar.currentPercent = Mathf.Clamp01(percent);
            }

            // Immediately resize foreground
            float targetWidth = bar.maxWidth * bar.currentPercent;
            bar.fillRect.sizeDelta = new Vector2(targetWidth, 0f);

            // Lerp catchup bar
            if (bar.catchUpPercent > bar.currentPercent)
            {
                bar.catchUpPercent = Mathf.MoveTowards(bar.catchUpPercent, bar.currentPercent, Time.deltaTime * 0.35f);
            }
            else
            {
                bar.catchUpPercent = bar.currentPercent;
            }
            bar.catchUpRect.sizeDelta = new Vector2(bar.maxWidth * bar.catchUpPercent, 0f);
        }

        public void TriggerScreenShake(float duration, float magnitude)
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ScreenShakeRoutine(duration, magnitude));
        }

        private IEnumerator ScreenShakeRoutine(float duration, float magnitude)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;

            Vector3 originalPos = cam.transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                // If hit stop is active, we use realTimeDeltaTime
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;

                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;

                cam.transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
                yield return null;
            }

            cam.transform.localPosition = originalPos;
        }

        public void TriggerHitStop(int frames)
        {
            if (activeHitStopCoroutine != null)
            {
                StopCoroutine(activeHitStopCoroutine);
            }
            activeHitStopCoroutine = StartCoroutine(HitStopRoutine(frames));
        }

        private IEnumerator HitStopRoutine(int frames)
        {
            if (!isHitStopping)
            {
                originalTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                isHitStopping = true;
            }
            
            Time.timeScale = 0f;

            for (int i = 0; i < frames; i++)
            {
                yield return null; // Resumes regardless of timeScale
            }

            Time.timeScale = originalTimeScale;
            isHitStopping = false;
            activeHitStopCoroutine = null;
        }

        public void RegisterHit(int victimPlayerID, int damage, Vector3 position)
        {
            // Spawn Floating Damage text
            ShowDamageText(position, damage);

            // Trigger visual screen shake on hit
            TriggerScreenShake(0.08f, 0.05f);

            // Combo calculations
            if (victimPlayerID == 2)
            {
                // Player 1 landed hit on Player 2
                if (Time.time - p1LastHitTime <= comboTimeout)
                {
                    p1ComboCount++;
                }
                else
                {
                    p1ComboCount = 1;
                }
                p1LastHitTime = Time.time;
                ShowCombo(1, p1ComboCount);
            }
            else if (victimPlayerID == 1)
            {
                // Player 2 landed hit on Player 1
                if (Time.time - p2LastHitTime <= comboTimeout)
                {
                    p2ComboCount++;
                }
                else
                {
                    p2ComboCount = 1;
                }
                p2LastHitTime = Time.time;
                ShowCombo(2, p2ComboCount);
            }
        }

        public void ShowCombo(int playerID, int hits)
        {
            if (playerID == 1)
            {
                if (p1ComboCoroutine != null) StopCoroutine(p1ComboCoroutine);
                p1ComboCoroutine = StartCoroutine(ShowComboRoutine(1, hits));
            }
            else
            {
                if (p2ComboCoroutine != null) StopCoroutine(p2ComboCoroutine);
                p2ComboCoroutine = StartCoroutine(ShowComboRoutine(2, hits));
            }
        }

        private IEnumerator ShowComboRoutine(int playerID, int hits)
        {
            Text comboTxt = playerID == 1 ? p1ComboText : p2ComboText;
            if (comboTxt == null) yield break;

            if (hits < 2)
            {
                comboTxt.gameObject.SetActive(false);
                yield break;
            }

            comboTxt.gameObject.SetActive(true);
            comboTxt.text = hits + " HITS!";
            
            // Change color dynamically based on hit intensity
            Color comboColor = hits > 8 ? new Color(1.0f, 0.35f, 0.1f) : new Color(1.0f, 0.85f, 0.15f);
            comboTxt.color = comboColor;

            // Scale pop animation
            float elapsed = 0f;
            float popDuration = 0.1f;
            while (elapsed < popDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / popDuration;
                float scale = Mathf.Lerp(1.5f, 1.0f, t * t);
                comboTxt.transform.localScale = new Vector3(scale, scale, 1.0f);
                yield return null;
            }
            comboTxt.transform.localScale = Vector3.one;

            yield return new WaitForSeconds(comboTimeout);

            // Fade out
            elapsed = 0f;
            float fadeDuration = 0.3f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                Color col = comboTxt.color;
                col.a = 1.0f - t;
                comboTxt.color = col;
                yield return null;
            }

            comboTxt.gameObject.SetActive(false);
        }

        public void ShowDamageText(Vector3 worldPos, int damage)
        {
            if (canvas == null) return;

            GameObject dmgObj = new GameObject("DamageText");
            dmgObj.transform.SetParent(canvas.transform, false);

            RectTransform rect = dmgObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150, 50);

            Text text = dmgObj.AddComponent<Text>();
            text.font = uiFont;
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = damage.ToString();
            text.color = new Color(1.0f, 0.2f, 0.15f, 1.0f);

            // Shadow duplicate
            GameObject shadowObj = new GameObject("Shadow");
            shadowObj.transform.SetParent(dmgObj.transform, false);
            RectTransform shadowRect = shadowObj.AddComponent<RectTransform>();
            shadowRect.anchoredPosition = new Vector2(2, -2);
            shadowRect.sizeDelta = rect.sizeDelta;
            Text shadowText = shadowObj.AddComponent<Text>();
            shadowText.font = uiFont;
            shadowText.fontSize = 28;
            shadowText.fontStyle = FontStyle.Bold;
            shadowText.alignment = TextAnchor.MiddleCenter;
            shadowText.text = damage.ToString();
            shadowText.color = Color.black;

            StartCoroutine(AnimateDamageText(dmgObj, rect, text, shadowText, worldPos));
        }

        private IEnumerator AnimateDamageText(GameObject obj, RectTransform rect, Text text, Text shadow, Vector3 worldPos)
        {
            Camera cam = Camera.main;
            Vector2 screenPos = Vector2.zero;
            if (cam != null)
            {
                screenPos = cam.WorldToScreenPoint(worldPos + Vector3.up * 1.2f);
            }

            // Slight random horizontal scatter
            screenPos.x += Random.Range(-25f, 25f);
            rect.position = screenPos;

            float elapsed = 0f;
            float duration = 0.75f;
            Vector3 startPos = rect.anchoredPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Arc path upward
                rect.anchoredPosition = startPos + new Vector3(0f, t * 90f, 0f);

                // Punch scale animation
                float scale = 1.0f;
                if (t < 0.15f)
                {
                    scale = Mathf.Lerp(1.0f, 1.4f, t / 0.15f);
                }
                else
                {
                    scale = Mathf.Lerp(1.4f, 0.9f, (t - 0.15f) / 0.85f);
                }
                rect.localScale = new Vector3(scale, scale, 1.0f);

                // Fade out
                Color col = text.color;
                col.a = 1.0f - t;
                text.color = col;

                Color sCol = shadow.color;
                sCol.a = (1.0f - t) * 0.85f;
                shadow.color = sCol;

                yield return null;
            }

            Destroy(obj);
        }

        void Update()
        {
            if (player1 == null || player2 == null)
            {
                FindPlayers();
            }

            // Round Timer calculation
            if (isTimerActive && GameManager.Instance != null && GameManager.Instance.IsPlaying)
            {
                roundTimeRemaining -= Time.deltaTime;
                if (roundTimeRemaining <= 0)
                {
                    roundTimeRemaining = 0;
                    isTimerActive = false;
                    
                    // Time out resolution: player with lower HP loses
                    if (player1 != null && player2 != null)
                    {
                        if (player1.currentHP < player2.currentHP)
                        {
                            player1.TakeDamage(player1.maxHP);
                        }
                        else if (player2.currentHP < player1.currentHP)
                        {
                            player2.TakeDamage(player2.maxHP);
                        }
                        else
                        {
                            player1.TakeDamage(player1.maxHP);
                            player2.TakeDamage(player2.maxHP);
                        }
                    }
                }
            }

            // Update timer text and alert pulses
            if (timerText != null)
            {
                timerText.text = Mathf.CeilToInt(roundTimeRemaining).ToString();
                if (roundTimeRemaining <= 10f && roundTimeRemaining > 0f)
                {
                    timerText.color = new Color(1.0f, 0.25f, 0.25f);
                    float pulse = 1f + Mathf.PingPong(Time.time * 4f, 0.15f);
                    timerText.transform.parent.localScale = new Vector3(pulse, pulse, 1f);
                }
                else
                {
                    timerText.color = new Color(1.0f, 0.85f, 0.2f);
                    timerText.transform.parent.localScale = Vector3.one;
                }
            }

            // Update P1 UI
            if (player1 != null)
            {
                if (p1NameText != null)
                {
                    string rawName = player1.gameObject.name.Replace("(Clone)", "").ToUpper();
                    p1NameText.text = $"<color=#FFD700>P1</color>  <color=#00e5ff>{rawName}</color>";
                }
                
                float maxHp = player1.maxHP > 0 ? player1.maxHP : 100f;
                float hpPercent = (float)player1.currentHP / maxHp;
                SetBarValue(p1HpBar, hpPercent);

                if (p1HpValueText != null)
                {
                    int hpPercentInt = Mathf.CeilToInt(hpPercent * 100f);
                    p1HpValueText.text = $"{hpPercentInt}%";
                }
                
                float maxPos = player1.maxPosture > 0 ? player1.maxPosture : 100f;
                float postPercent = player1.currentPosture / maxPos;
                SetBarValue(p1PostureBar, postPercent);
                
                float maxMana = player1.maxMana > 0 ? player1.maxMana : 100f;
                float manaPercent = player1.currentMana / maxMana;
                SetBarValue(p1ManaBar, manaPercent);

                // Low HP alarm flash
                if (hpPercent < 0.3f && player1.currentHP > 0)
                {
                    p1HpBar.fillImage.color = Color.Lerp(Color.white, new Color(1.0f, 0.3f, 0.2f), Mathf.PingPong(Time.time * 6f, 1.0f));
                }
                else
                {
                    p1HpBar.fillImage.color = Color.white;
                }

                // ストック表示の更新
                UpdateStockIcons(p1StockContainer, player1.remainingLives, false);
            }
            else
            {
                SetBarValue(p1HpBar, 0f);
                SetBarValue(p1PostureBar, 0f);
                SetBarValue(p1ManaBar, 0f);
                if (p1HpValueText != null)
                {
                    p1HpValueText.text = "0%";
                }
            }

            // Update P2 UI
            if (player2 != null)
            {
                if (p2NameText != null)
                {
                    string rawName = player2.gameObject.name.Replace("(Clone)", "").ToUpper();
                    p2NameText.text = $"<color=#ff5500>{rawName}</color>  <color=#FFD700>P2</color>";
                }
                
                float maxHp = player2.maxHP > 0 ? player2.maxHP : 100f;
                float hpPercent = (float)player2.currentHP / maxHp;
                SetBarValue(p2HpBar, hpPercent);

                if (p2HpValueText != null)
                {
                    int hpPercentInt = Mathf.CeilToInt(hpPercent * 100f);
                    p2HpValueText.text = $"{hpPercentInt}%";
                }
                
                float maxPos = player2.maxPosture > 0 ? player2.maxPosture : 100f;
                float postPercent = player2.currentPosture / maxPos;
                SetBarValue(p2PostureBar, postPercent);
                
                float maxMana = player2.maxMana > 0 ? player2.maxMana : 100f;
                float manaPercent = player2.currentMana / maxMana;
                SetBarValue(p2ManaBar, manaPercent);

                // Low HP alarm flash
                if (hpPercent < 0.3f && player2.currentHP > 0)
                {
                    p2HpBar.fillImage.color = Color.Lerp(Color.white, new Color(1.0f, 0.3f, 0.2f), Mathf.PingPong(Time.time * 6f, 1.0f));
                }
                else
                {
                    p2HpBar.fillImage.color = Color.white;
                }

                // ストック表示の更新
                UpdateStockIcons(p2StockContainer, player2.remainingLives, true);
            }
            else
            {
                SetBarValue(p2HpBar, 0f);
                SetBarValue(p2PostureBar, 0f);
                SetBarValue(p2ManaBar, 0f);
                if (p2HpValueText != null)
                {
                    p2HpValueText.text = "0%";
                }
            }
        }
    }
}
