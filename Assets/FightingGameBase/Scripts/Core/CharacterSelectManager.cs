using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace FightingGameBase
{
    // =================================================================================
    // 【CharacterSelectManager（キャラクターセレクト＆対戦マネージャー）】
    // 
    // 1. 最初に対戦モードを選択:
    //    - [1] 1v1 モード (vs CPU)
    //    - [2] 1v1 プレイヤー対戦 (2P Local)
    //    - [3] テストモード (Test Mode / サンドバッグ)
    // 2. 1P（およびPvP時は2P）のキャラクター（1〜7）を選択
    //    - 各キャラクター本来の固有スペシャル攻撃・必殺技がそのまま動作します！
    // 3. テストモード中: サンドバッグ設定パネルから攻撃モードの切り替えが可能
    // 4. [R]キーでいつでもモード選択画面に戻れます
    // =================================================================================
    public class CharacterSelectManager : MonoBehaviour
    {
        [System.Serializable]
        public class WeaponEntry
        {
            public string displayName;
            public GameObject prefab;
        }

        [Header("選択できるキャラクター一覧 (1〜7)")]
        public WeaponEntry[] weapons;

        [Header("配置設定")]
        public float spawnX = 5f;
        public float spawnY = -1f;

        public static CharacterSelectManager Instance { get; private set; }
        public static bool IsSpawningFromSelect { get; private set; } = false;

        public enum GameMode
        {
            VsCPU,      // 1P vs CPU AI
            PvP,        // 1P vs 2P 対戦
            TestMode    // 1P vs サンドバッグ
        }

        public enum Phase
        {
            SelectMode,
            SelectP1,
            SelectP2,
            Fighting
        }

        public GameMode gameMode = GameMode.VsCPU;
        public Phase phase = Phase.SelectMode;

        public bool IsSelectingPhase => phase != Phase.Fighting;

        public struct CharDef
        {
            public string name;
            public string summary;
            public CharDef(string n, string s) { name = n; summary = s; }
        }

        public static readonly CharDef[] CharacterList = new CharDef[]
        {
            new CharDef("大剣 (TAIKEN)", "豪快な広範囲近接斬撃 ＆ 巨大化溜め重斬り"),
            new CharDef("ダガー (Dagger)", "超高速の連撃 ＆ 突進一閃スペシャル"),
            new CharDef("炎 (Flame)", "炎刃スラッシュ ＆ 貫通大火球スペシャル"),
            new CharDef("雷 (Thunder)", "雷光強打撃 ＆ 天からの落雷スペシャル"),
            new CharDef("氷 (Ice)", "冷気の氷刃 ＆ 相手を凍結拘束する氷結弾スペシャル"),
            new CharDef("影 (Shadow)", "長射程の影鎌 ＆ 敵の必殺技を封印する影の領域"),
            new CharDef("忍者 (Ninja)", "雷球設置攻撃 ＆ 飛雷神クナイテレポート")
        };

        private int p1CharIndex = 0;
        private int p2CharIndex = 2; // デフォルト炎

        private GameObject currentP1;
        private GameObject currentP2;
        private SandbagAI activeSandbag;
        private bool showSandbagSettings = false;

        private static readonly Key[] NumberKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        private static readonly Key[] NumpadKeys =
        {
            Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
        };

        // UI スタイル
        private Texture2D panelTex;
        private Texture2D btnNormalTex;
        private Texture2D btnHoverTex;
        private Texture2D btnActiveTex;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle charButtonStyle;
        private GUIStyle labelStyle;
        private bool stylesInitialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureManager()
        {
            if (Instance == null && FindAnyObjectByType<CharacterSelectManager>() == null)
            {
                GameObject managerGo = new GameObject("CharacterSelectManager");
                managerGo.AddComponent<CharacterSelectManager>();
                Debug.Log("【CharacterSelectManager】シーン読み込み時に自動配置されました。");
            }
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureCameraSetup();
            InitializeDefaultWeapons();
        }

        private void EnsureCameraSetup()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                Vector3 p = cam.transform.position;
                p.z = -10f;
                cam.transform.position = p;
            }
        }

        public void InitializeDefaultWeapons()
        {
#if UNITY_EDITOR
            if (weapons == null || weapons.Length < 7)
            {
                string taikenPath = System.IO.File.Exists("Assets/TAIKEN/FloatingGreatsword.prefab")
                    ? "Assets/TAIKEN/FloatingGreatsword.prefab"
                    : (System.IO.File.Exists("Assets/TAIKEN/Taiken.prefab") ? "Assets/TAIKEN/Taiken.prefab" : "Assets/TAIKEN/FloatingGreatsword.prefab");

                weapons = new[]
                {
                    MakeEntry("TAIKEN", taikenPath),
                    MakeEntry("Dagger", "Assets/Dagger/Dagger.prefab"),
                    MakeEntry("Flame", "Assets/Flame/FlameCharacter.prefab"),
                    MakeEntry("Thunder", "Assets/Thunder/ThunderCharacter.prefab"),
                    MakeEntry("Ice", "Assets/Ice/IceCharacter.prefab"),
                    MakeEntry("Shadow", "Assets/Shadow/ShadowCharacter.prefab"),
                    MakeEntry("Ninja", "Assets/Ninja/NinjaCharacter.prefab"),
                };
            }
            else
            {
                // 各プレハブが確実にルートオブジェクトを参照しているか検証・補正
                for (int i = 0; i < weapons.Length; i++)
                {
                    if (weapons[i] != null && weapons[i].prefab != null)
                    {
                        if (weapons[i].prefab.GetComponent<CharacterBase>() == null)
                        {
                            var cb = weapons[i].prefab.GetComponentInParent<CharacterBase>();
                            if (cb != null) weapons[i].prefab = cb.gameObject;
                            else if (weapons[i].prefab.transform.root != null && weapons[i].prefab.transform.root.GetComponent<CharacterBase>() != null)
                            {
                                weapons[i].prefab = weapons[i].prefab.transform.root.gameObject;
                            }
                        }
                    }
                }
            }
#endif
        }

#if UNITY_EDITOR
        private static WeaponEntry MakeEntry(string displayName, string prefabPath)
        {
            var entry = new WeaponEntry { displayName = displayName };
            var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (loaded != null && loaded.GetComponent<CharacterBase>() == null)
            {
                var cb = loaded.GetComponentInParent<CharacterBase>();
                if (cb != null) loaded = cb.gameObject;
            }
            entry.prefab = loaded;
            return entry;
        }
#endif

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Rキーでいつでもモード選択に戻る
            if (keyboard[Key.R].wasPressedThisFrame)
            {
                ReturnToModeSelect();
                return;
            }

            switch (phase)
            {
                case Phase.SelectMode:
                    if (keyboard[Key.Digit1].wasPressedThisFrame || keyboard[Key.Numpad1].wasPressedThisFrame)
                        SelectGameMode(GameMode.VsCPU);
                    else if (keyboard[Key.Digit2].wasPressedThisFrame || keyboard[Key.Numpad2].wasPressedThisFrame)
                        SelectGameMode(GameMode.PvP);
                    else if (keyboard[Key.Digit3].wasPressedThisFrame || keyboard[Key.Numpad3].wasPressedThisFrame)
                        SelectGameMode(GameMode.TestMode);
                    break;

                case Phase.SelectP1:
                    HandleNumberInput(index =>
                    {
                        if (index >= 0 && index < weapons.Length && index < CharacterList.Length)
                        {
                            p1CharIndex = index;
                            if (gameMode == GameMode.PvP)
                            {
                                phase = Phase.SelectP2;
                            }
                            else
                            {
                                StartFight();
                            }
                        }
                    });
                    if (keyboard[Key.Escape].wasPressedThisFrame || keyboard[Key.B].wasPressedThisFrame)
                        phase = Phase.SelectMode;
                    break;

                case Phase.SelectP2:
                    HandleNumberInput(index =>
                    {
                        if (index >= 0 && index < weapons.Length && index < CharacterList.Length)
                        {
                            p2CharIndex = index;
                            StartFight();
                        }
                    });
                    if (keyboard[Key.Escape].wasPressedThisFrame || keyboard[Key.B].wasPressedThisFrame)
                        phase = Phase.SelectP1;
                    break;

                case Phase.Fighting:
                    if (gameMode == GameMode.TestMode && activeSandbag != null)
                    {
                        if (keyboard[Key.T].wasPressedThisFrame || keyboard[Key.Tab].wasPressedThisFrame)
                        {
                            showSandbagSettings = !showSandbagSettings;
                        }
                        if (keyboard[Key.Digit1].wasPressedThisFrame || keyboard[Key.Numpad1].wasPressedThisFrame)
                            activeSandbag.SetAttackMode(SandbagAI.AttackMode.None);
                        else if (keyboard[Key.Digit2].wasPressedThisFrame || keyboard[Key.Numpad2].wasPressedThisFrame)
                            activeSandbag.SetAttackMode(SandbagAI.AttackMode.Normal);
                        else if (keyboard[Key.Digit3].wasPressedThisFrame || keyboard[Key.Numpad3].wasPressedThisFrame)
                            activeSandbag.SetAttackMode(SandbagAI.AttackMode.Special);
                    }
                    break;
            }
        }

        private void HandleNumberInput(System.Action<int> onSelect)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (int i = 0; i < NumberKeys.Length; i++)
            {
                if (keyboard[NumberKeys[i]].wasPressedThisFrame || keyboard[NumpadKeys[i]].wasPressedThisFrame)
                {
                    onSelect(i);
                    return;
                }
            }
        }

        public void SelectGameMode(GameMode mode)
        {
            gameMode = mode;
            phase = Phase.SelectP1;
        }

        public void ReturnToModeSelect()
        {
            phase = Phase.SelectMode;
            showSandbagSettings = false;
            if (currentP1 != null) Destroy(currentP1);
            if (currentP2 != null) Destroy(currentP2);
            activeSandbag = null;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.currentState = GameManager.GameState.Starting;
            }
        }

        public void ResetToCharacterSelect()
        {
            ReturnToModeSelect();
        }

        public static void ClearAllExistingCharacters()
        {
            var existingChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (var c in existingChars)
            {
                if (c != null)
                {
                    Destroy(c.gameObject);
                }
            }
        }

        private GameObject GetSafePrefab(int index)
        {
            if (weapons == null || index < 0 || index >= weapons.Length) return null;
            var prefab = weapons[index].prefab;
            if (prefab == null) return null;

            // ルートオブジェクトが確実に CharacterBase を持つかチェック・補正
            if (prefab.GetComponent<CharacterBase>() == null)
            {
                var cb = prefab.GetComponentInParent<CharacterBase>();
                if (cb != null) return cb.gameObject;

                if (prefab.transform.root != null)
                {
                    var rootCb = prefab.transform.root.GetComponent<CharacterBase>();
                    if (rootCb != null) return rootCb.gameObject;
                }
            }
            return prefab;
        }

        public void StartFight()
        {
            IsSpawningFromSelect = true;

            // 既存キャラの削除
            if (currentP1 != null) Destroy(currentP1);
            if (currentP2 != null) Destroy(currentP2);

            ClearAllExistingCharacters();

            // 1P 生成
            int p1Index = Mathf.Clamp(p1CharIndex, 0, weapons.Length - 1);
            GameObject p1Prefab = GetSafePrefab(p1Index);
            if (p1Prefab != null)
            {
                Vector3 p1Pos = new Vector3(-spawnX, spawnY, 0f);
                currentP1 = Instantiate(p1Prefab, p1Pos, Quaternion.identity);
                currentP1.name = $"{weapons[p1Index].displayName}_P1";

                ConfigureCharacterPlayerID(currentP1, 1);
                FaceDirection(currentP1.transform, 1f);

                var char1 = currentP1.GetComponent<CharacterBase>();
                if (char1 != null)
                {
                    char1.playerID = 1;
                    char1.customSpecialSkill = null; // 各キャラ固有のスペシャル攻撃をそのまま使用！
                }
            }

            // 2P / CPU / サンドバッグ 生成
            int p2Index = Mathf.Clamp(p2CharIndex, 0, weapons.Length - 1);
            if (gameMode == GameMode.TestMode)
            {
                p2Index = 0; // TAIKEN
            }

            GameObject p2Prefab = GetSafePrefab(p2Index);
            if (p2Prefab != null)
            {
                Vector3 p2Pos = new Vector3(spawnX, spawnY, 0f);
                currentP2 = Instantiate(p2Prefab, p2Pos, Quaternion.identity);

                if (gameMode == GameMode.VsCPU)
                    currentP2.name = $"{weapons[p2Index].displayName}_CPU_P2";
                else if (gameMode == GameMode.TestMode)
                    currentP2.name = "Sandbag_Dummy_P2";
                else
                    currentP2.name = $"{weapons[p2Index].displayName}_P2";

                ConfigureCharacterPlayerID(currentP2, 2);
                FaceDirection(currentP2.transform, -1f);

                var char2 = currentP2.GetComponent<CharacterBase>();
                if (char2 != null)
                {
                    char2.playerID = 2;
                    char2.customSpecialSkill = null;
                }

                if (gameMode == GameMode.VsCPU)
                {
                    var pInput = currentP2.GetComponent<PlayerInputController>();
                    if (pInput != null) pInput.enabled = false;

                    var cpuAi = currentP2.AddComponent<VersusCPUAI>();
                    cpuAi.targetPlayer = currentP1 != null ? currentP1.GetComponent<CharacterBase>() : null;
                    cpuAi.difficulty = VersusCPUAI.AIDifficulty.Normal;
                }
                else if (gameMode == GameMode.TestMode)
                {
                    var pInput = currentP2.GetComponent<PlayerInputController>();
                    if (pInput != null) pInput.enabled = false;

                    activeSandbag = currentP2.AddComponent<SandbagAI>();
                    activeSandbag.isImmortal = true;
                    activeSandbag.autoAttack = true;
                    activeSandbag.attackInterval = 2.0f;
                    activeSandbag.attackMode = SandbagAI.AttackMode.Normal;
                    showSandbagSettings = false;
                }
                else
                {
                    var pInput = currentP2.GetComponent<PlayerInputController>();
                    if (pInput != null) pInput.enabled = true;
                }
            }

            IsSpawningFromSelect = false;
            phase = Phase.Fighting;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartBattle();
            }
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ResetHUD();
            }
        }

        private static void ConfigureCharacterPlayerID(GameObject go, int playerID)
        {
            if (go == null) return;
            CharacterBase charBase = go.GetComponent<CharacterBase>();
            if (charBase != null)
            {
                charBase.playerID = playerID;
            }

            PlayerInputController pic = go.GetComponent<PlayerInputController>();
            if (pic != null)
            {
                pic.SetupKeys(playerID);
            }
        }

        private static void FaceDirection(Transform t, float sign)
        {
            if (t == null) return;
            Vector3 s = t.localScale;
            s.x = Mathf.Abs(s.x) * Mathf.Sign(sign);
            t.localScale = s;
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            panelTex = MakeColorTex(new Color(0.06f, 0.08f, 0.12f, 0.95f));
            btnNormalTex = MakeColorTex(new Color(0.14f, 0.18f, 0.25f, 0.96f));
            btnHoverTex = MakeColorTex(new Color(0.24f, 0.38f, 0.58f, 1f));
            btnActiveTex = MakeColorTex(new Color(0.12f, 0.50f, 0.85f, 1f));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 0.92f, 0.55f) }
            };

            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.92f, 1.0f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            buttonStyle.normal.background = btnNormalTex;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.background = btnHoverTex;
            buttonStyle.hover.textColor = new Color(1f, 0.95f, 0.4f);
            buttonStyle.active.background = btnActiveTex;
            buttonStyle.active.textColor = Color.white;

            charButtonStyle = new GUIStyle(buttonStyle)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.80f, 0.90f, 1.0f) }
            };

            stylesInitialized = true;
        }

        private Texture2D MakeColorTex(Color col)
        {
            var tex = new Texture2D(2, 2);
            Color[] pix = new Color[4];
            for (int i = 0; i < 4; i++) pix[i] = col;
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        void OnGUI()
        {
            InitStyles();

            if (phase == Phase.Fighting)
            {
                DrawFightingHUD();
                return;
            }

            float screenW = Screen.width;
            float screenH = Screen.height;

            if (phase == Phase.SelectMode)
            {
                DrawModeSelectScreen(screenW, screenH);
                return;
            }

            DrawCharacterSelectScreen(screenW, screenH);
        }

        // =================================================================================
        // 1. 対戦モード選択画面
        // =================================================================================
        private void DrawModeSelectScreen(float screenW, float screenH)
        {
            float panelW = Mathf.Min(700f, screenW - 20f);
            float panelH = Mathf.Min(480f, screenH - 20f);
            float panelX = (screenW - panelW) * 0.5f;
            float panelY = (screenH - panelH) * 0.5f;

            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), panelTex);

            float curY = panelY + 22f;
            GUI.Label(new Rect(panelX, curY, panelW, 32f), "【 対 戦 モ ー ド 選 択 】", titleStyle);

            curY += 40f;
            GUI.Label(new Rect(panelX, curY, panelW, 26f), "プレイするモードを選択してください (数字キー または クリック)", subtitleStyle);

            curY += 38f;
            float btnW = Mathf.Min(620f, panelW - 40f);
            float btnH = 62f;
            float btnX = panelX + (panelW - btnW) * 0.5f;

            // [1] vs CPU
            if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "[ 1 ]  1v1 モード  (vs CPU)\nAIと真剣勝負！ 間合い管理やコンボを挑む", buttonStyle))
            {
                SelectGameMode(GameMode.VsCPU);
            }

            curY += btnH + 16f;
            // [2] PvP
            if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "[ 2 ]  1v1 プレイヤー対戦  (2P Local)\n1P vs 2P のローカル2人対戦", buttonStyle))
            {
                SelectGameMode(GameMode.PvP);
            }

            curY += btnH + 16f;
            // [3] Test Mode (サンドバッグ)
            if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "[ 3 ]  テストモード  (Test Mode / サンドバッグ)\n不死身のサンドバッグ相手に技の練習やパリー特訓！", buttonStyle))
            {
                SelectGameMode(GameMode.TestMode);
            }

            curY += btnH + 22f;
            GUI.Label(new Rect(panelX, curY, panelW, 24f), "※ 対戦中はいつでも [R] キーでこの画面に戻ることができます", labelStyle);
        }

        // =================================================================================
        // 2. キャラクター選択画面 (1〜7の武器・キャラを選択)
        // =================================================================================
        private void DrawCharacterSelectScreen(float screenW, float screenH)
        {
            float panelW = Mathf.Min(700f, screenW - 20f);
            float panelH = Mathf.Min(450f, screenH - 20f);
            float panelX = (screenW - panelW) * 0.5f;
            float panelY = (screenH - panelH) * 0.5f;

            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), panelTex);

            float curY = panelY + 12f;
            int playerNum = (phase == Phase.SelectP1) ? 1 : 2;

            string title = $"【 {playerNum}P  キャラクター選択 】";
            string subtitle = "使いたいキャラクターを選択してください (1〜7キー または クリック)";

            GUI.Label(new Rect(panelX, curY, panelW, 28f), title, titleStyle);
            curY += 28f;
            GUI.Label(new Rect(panelX, curY, panelW, 20f), subtitle, subtitleStyle);
            curY += 22f;

            float btnW = Mathf.Min(640f, panelW - 30f);
            float btnH = 36f;
            float btnSpacing = 5f;
            float btnX = panelX + (panelW - btnW) * 0.5f;

            int count = Mathf.Min(weapons.Length, CharacterList.Length);
            for (int i = 0; i < count; i++)
            {
                string label = $"[ {i + 1} ]  {CharacterList[i].name}   ―   {CharacterList[i].summary}";
                if (GUI.Button(new Rect(btnX, curY, btnW, btnH), label, charButtonStyle))
                {
                    if (phase == Phase.SelectP1)
                    {
                        p1CharIndex = i;
                        if (gameMode == GameMode.PvP)
                        {
                            phase = Phase.SelectP2;
                        }
                        else
                        {
                            StartFight();
                        }
                    }
                    else if (phase == Phase.SelectP2)
                    {
                        p2CharIndex = i;
                        StartFight();
                    }
                }
                curY += btnH + btnSpacing;
            }

            // フッター
            curY += 6f;
            string status = (phase == Phase.SelectP1) ? "1P 選択中" : $"1P: [{CharacterList[p1CharIndex].name}]  |  2P 選択中";
            GUI.Label(new Rect(panelX + 25f, curY, panelW - 170f, 28f), status, labelStyle);

            if (GUI.Button(new Rect(panelX + panelW - 140f, curY - 2f, 120f, 30f), "戻る [B]", charButtonStyle))
            {
                if (phase == Phase.SelectP1) phase = Phase.SelectMode;
                else if (phase == Phase.SelectP2) phase = Phase.SelectP1;
            }
        }

        // =================================================================================
        // 3. バトル中のHUD
        // =================================================================================
        private void DrawFightingHUD()
        {
            var topBtnStyle = new GUIStyle(buttonStyle) { fontSize = 12 };

            // [R] モード選択へ戻るボタン
            if (GUI.Button(new Rect(Screen.width - 115f, 10f, 105f, 26f), "[R] メニュー", topBtnStyle))
            {
                ReturnToModeSelect();
            }

            // テストモード時: 「サンドバッグ設定」トグルボタン＆設定ウィンドウ
            if (gameMode == GameMode.TestMode && activeSandbag != null)
            {
                string btnLabel = showSandbagSettings ? "[ × 閉じる ] [TAB]" : "[ ⚙ 設定 ] [TAB]";
                if (GUI.Button(new Rect(10f, 10f, 125f, 26f), btnLabel, topBtnStyle))
                {
                    showSandbagSettings = !showSandbagSettings;
                }

                if (showSandbagSettings)
                {
                    DrawSandbagControlPanel();
                }
            }
        }

        private void DrawSandbagControlPanel()
        {
            float panelW = 300f;
            float panelH = 160f;
            float panelX = 10f;
            float panelY = 42f;

            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), panelTex);

            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.9f, 0.5f) }
            };
            GUI.Label(new Rect(panelX, panelY + 6f, panelW, 20f), "【 サンドバッグ動作設定 】", headerStyle);

            var optBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            float curY = panelY + 30f;
            GUI.Label(new Rect(panelX + 10f, curY, panelW - 20f, 18f), "攻撃モード:", labelStyle);
            curY += 20f;

            float btnW = 86f;
            float btnH = 28f;

            Color oldBg = GUI.backgroundColor;

            // [1] 停止
            if (activeSandbag.attackMode == SandbagAI.AttackMode.None) GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUI.Button(new Rect(panelX + 10f, curY, btnW, btnH), "[1] 停止", optBtnStyle))
            {
                activeSandbag.SetAttackMode(SandbagAI.AttackMode.None);
            }

            // [2] 通常攻撃
            GUI.backgroundColor = (activeSandbag.attackMode == SandbagAI.AttackMode.Normal) ? new Color(0.3f, 0.8f, 0.4f) : oldBg;
            if (GUI.Button(new Rect(panelX + 105f, curY, btnW, btnH), "[2] 通常", optBtnStyle))
            {
                activeSandbag.SetAttackMode(SandbagAI.AttackMode.Normal);
            }

            // [3] 特殊攻撃
            GUI.backgroundColor = (activeSandbag.attackMode == SandbagAI.AttackMode.Special) ? new Color(0.3f, 0.8f, 0.4f) : oldBg;
            if (GUI.Button(new Rect(panelX + 200f, curY, btnW, btnH), "[3] 特殊", optBtnStyle))
            {
                activeSandbag.SetAttackMode(SandbagAI.AttackMode.Special);
            }

            GUI.backgroundColor = oldBg;

            curY += btnH + 10f;
            GUI.Label(new Rect(panelX + 10f, curY, panelW - 20f, 18f), $"攻撃間隔: {activeSandbag.attackInterval:F1}秒", labelStyle);
            curY += 20f;

            float intBtnW = 62f;
            float[] intervals = { 1.0f, 1.5f, 2.0f, 3.0f };
            for (int i = 0; i < intervals.Length; i++)
            {
                float val = intervals[i];
                if (Mathf.Approximately(activeSandbag.attackInterval, val)) GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
                else GUI.backgroundColor = oldBg;

                if (GUI.Button(new Rect(panelX + 10f + i * 70f, curY, intBtnW, 24f), $"{val:F1}s", optBtnStyle))
                {
                    activeSandbag.attackInterval = val;
                }
            }
            GUI.backgroundColor = oldBg;
        }
    }
}