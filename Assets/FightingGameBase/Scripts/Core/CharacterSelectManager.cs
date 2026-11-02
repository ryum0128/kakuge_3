using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace FightingGameBase
{
    // =================================================================================
    // CharacterSelectManager: モード選択 ＆ 自分流スタイル構築システム
    // 
    // 1. 最初に対戦モードを選択:
    //    - [1] 1v1 モード (vs CPU)
    //    - [2] 1v1 プレイヤー対戦 (2P Local)
    //    - [3] テストモード (Test Mode / サンドバッグ)
    // 2. 1P/2P それぞれ「通常攻撃（基本スタイル）」と「スペシャル攻撃（必殺技）」を選択可能
    // 3. テストモード中: 「サンドバッグ攻撃設定」ボタンを押すと設定ウィンドウが開閉し、
    //    攻撃モード（停止/通常/スペシャル）や攻撃間隔（1.0s/1.5s/2.0s/3.0s）を自在に変更可能
    // 4. スキル選択画面で1〜7の全ボタンが画面内にすっきり収まるよう、高さを最適化
    // 5. [RuntimeInitializeOnLoadMethod] により全シーンで自動起動
    // =================================================================================
    public class CharacterSelectManager : MonoBehaviour
    {
        [System.Serializable]
        public class WeaponEntry
        {
            public string displayName;
            public GameObject prefab;
        }

        [Header("選択できる武器・キャラ一覧")]
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
            P1SelectNormal,
            P1SelectSpecial,
            P2SelectNormal,
            P2SelectSpecial,
            Fighting
        }

        private GameMode gameMode = GameMode.VsCPU;
        private Phase phase = Phase.SelectMode;

        public bool IsSelectingPhase => phase != Phase.Fighting;

        // サンドバッグ設定パネルの開閉状態
        private bool showSandbagSettings = false;

        // シンプルで直感的な技名定義
        public struct NormalSkillDef
        {
            public string name;
            public string summary;
            public NormalSkillDef(string n, string s) { name = n; summary = s; }
        }

        public struct SpecialSkillDef
        {
            public string name;
            public string summary;
            public SpecialAttackStyle style;
            public SpecialSkillDef(string n, string s, SpecialAttackStyle st)
            {
                name = n;
                summary = s;
                style = st;
            }
        }

        private static readonly NormalSkillDef[] NormalSkills = new NormalSkillDef[]
        {
            new NormalSkillDef("大剣", "威力15 / 広範囲近接撃"),
            new NormalSkillDef("ダガー", "威力8 / 超高速2連撃"),
            new NormalSkillDef("炎の剣", "威力18 / 踏み込み炎撃"),
            new NormalSkillDef("雷の槌", "威力16 / 雷光強打撃"),
            new NormalSkillDef("氷結剣", "威力16 / 冷気の斬撃"),
            new NormalSkillDef("影の鎌", "威力35 / 長射程・自傷5"),
            new NormalSkillDef("忍の雷球", "威力25 / 雷球設置＆斬撃")
        };

        private static readonly SpecialSkillDef[] SpecialSkills = new SpecialSkillDef[]
        {
            new SpecialSkillDef("溜め重斬り", "消費マナ30 / 威力35 / 大剣溜め一撃", SpecialAttackStyle.Taiken),
            new SpecialSkillDef("突進一閃", "消費マナ20 / 威力25 / 高速突進斬り", SpecialAttackStyle.Dagger),
            new SpecialSkillDef("火球", "消費マナ20 / 威力22 / 貫通火炎弾", SpecialAttackStyle.Flame),
            new SpecialSkillDef("天の落雷", "消費マナ25 / 威力25 / 頭上への落雷", SpecialAttackStyle.Thunder),
            new SpecialSkillDef("氷結弾", "消費マナ15 / 威力8 / 相手を1.5秒凍結拘束", SpecialAttackStyle.Ice),
            new SpecialSkillDef("影の領域", "再使用25秒 / 威力0 / 敵の全必殺技を封印", SpecialAttackStyle.Shadow),
            new SpecialSkillDef("飛雷神クナイ", "消費マナ0 / 威力10 / クナイ投擲＆テレポート", SpecialAttackStyle.Ninja)
        };

        // 選択されたインデックス
        private int p1NormalIndex = 0;
        private int p1SpecialIndex = 0;
        private int p2NormalIndex = 2; // デフォルト炎
        private int p2SpecialIndex = 2;

        private GameObject currentP1;
        private GameObject currentP2;
        private SandbagAI activeSandbag;

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
        private Texture2D highlightBtnTex;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle skillButtonStyle;
        private GUIStyle labelStyle;
        private GUIStyle statusBannerStyle;
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

        private void InitializeDefaultWeapons()
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
#endif
        }

#if UNITY_EDITOR
        private static WeaponEntry MakeEntry(string displayName, string prefabPath)
        {
            var entry = new WeaponEntry { displayName = displayName };
            entry.prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
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

            // フェーズ毎のキー入力
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

                case Phase.P1SelectNormal:
                    HandleNumberInput(index =>
                    {
                        if (index >= 0 && index < NormalSkills.Length)
                        {
                            p1NormalIndex = index;
                            phase = Phase.P1SelectSpecial;
                        }
                    });
                    if (keyboard[Key.Escape].wasPressedThisFrame || keyboard[Key.B].wasPressedThisFrame)
                        phase = Phase.SelectMode;
                    break;

                case Phase.P1SelectSpecial:
                    HandleNumberInput(index =>
                    {
                        if (index >= 0 && index < SpecialSkills.Length)
                        {
                            p1SpecialIndex = index;
                            if (gameMode == GameMode.PvP)
                            {
                                phase = Phase.P2SelectNormal;
                            }
                            else
                            {
                                StartFight();
                            }
                        }
                    });
                    if (keyboard[Key.Escape].wasPressedThisFrame || keyboard[Key.B].wasPressedThisFrame)
                        phase = Phase.P1SelectNormal;
                    break;

                case Phase.P2SelectNormal:
                    HandleNumberInput(index =>
                    {
                        if (index >= 0 && index < NormalSkills.Length)
                        {
                            p2NormalIndex = index;
                            phase = Phase.P2SelectSpecial;
                        }
                    });
                    if (keyboard[Key.Escape].wasPressedThisFrame || keyboard[Key.B].wasPressedThisFrame)
                        phase = Phase.P1SelectSpecial;
                    break;

                case Phase.P2SelectSpecial:
                    HandleNumberInput(index =>
                    {
                        if (index >= 0 && index < SpecialSkills.Length)
                        {
                            p2SpecialIndex = index;
                            StartFight();
                        }
                    });
                    if (keyboard[Key.Escape].wasPressedThisFrame || keyboard[Key.B].wasPressedThisFrame)
                        phase = Phase.P2SelectNormal;
                    break;

                case Phase.Fighting:
                    if (gameMode == GameMode.TestMode && activeSandbag != null)
                    {
                        // TキーまたはTabキーでサンドバッグ設定を開閉
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
            phase = Phase.P1SelectNormal;
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

        // HUDManager からの呼び出し互換用
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

        public void StartFight()
        {
            IsSpawningFromSelect = true;

            // 既存キャラの削除
            if (currentP1 != null) Destroy(currentP1);
            if (currentP2 != null) Destroy(currentP2);

            ClearAllExistingCharacters();

            // 1P 生成
            int p1PrefabIndex = Mathf.Clamp(p1NormalIndex, 0, weapons.Length - 1);
            if (weapons != null && weapons.Length > p1PrefabIndex && weapons[p1PrefabIndex].prefab != null)
            {
                Vector3 p1Pos = new Vector3(-spawnX, spawnY, 0f);
                currentP1 = Instantiate(weapons[p1PrefabIndex].prefab, p1Pos, Quaternion.identity);
                currentP1.name = $"{weapons[p1PrefabIndex].displayName}_P1";

                ConfigureCharacterPlayerID(currentP1, 1);
                FaceDirection(currentP1.transform, 1f);

                var char1 = currentP1.GetComponent<CharacterBase>();
                if (char1 != null)
                {
                    char1.playerID = 1;
                    var custom1 = currentP1.AddComponent<CustomSpecialSkill>();
                    custom1.Initialize(char1, SpecialSkills[p1SpecialIndex].style, true);
                    char1.customSpecialSkill = custom1;
                }
            }

            // 2P / CPU / サンドバッグ 生成
            int p2PrefabIndex = Mathf.Clamp(p2NormalIndex, 0, weapons.Length - 1);
            if (gameMode == GameMode.TestMode)
            {
                p2PrefabIndex = 0; // TAIKEN
            }

            if (weapons != null && weapons.Length > p2PrefabIndex && weapons[p2PrefabIndex].prefab != null)
            {
                Vector3 p2Pos = new Vector3(spawnX, spawnY, 0f);
                currentP2 = Instantiate(weapons[p2PrefabIndex].prefab, p2Pos, Quaternion.identity);

                if (gameMode == GameMode.VsCPU)
                    currentP2.name = $"{weapons[p2PrefabIndex].displayName}_CPU_P2";
                else if (gameMode == GameMode.TestMode)
                    currentP2.name = "Sandbag_Dummy_P2";
                else
                    currentP2.name = $"{weapons[p2PrefabIndex].displayName}_P2";

                ConfigureCharacterPlayerID(currentP2, 2);
                FaceDirection(currentP2.transform, -1f);

                var char2 = currentP2.GetComponent<CharacterBase>();
                if (char2 != null)
                {
                    char2.playerID = 2;
                    var custom2 = currentP2.AddComponent<CustomSpecialSkill>();
                    custom2.Initialize(char2, SpecialSkills[p2SpecialIndex].style, true);
                    char2.customSpecialSkill = custom2;
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
                    showSandbagSettings = false; // 初期状態ではスッキリ閉じておく
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
            highlightBtnTex = MakeColorTex(new Color(0.18f, 0.55f, 0.35f, 1f));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 0.92f, 0.55f) }
            };

            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.92f, 1.0f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
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

            // スキル選択ボタン用（6・7が画面外に出ないよう高さを適度にし、読みやすいフォントに）
            skillButtonStyle = new GUIStyle(buttonStyle)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.80f, 0.90f, 1.0f) }
            };

            statusBannerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = panelTex, textColor = Color.white }
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

            DrawSkillSelectScreen(screenW, screenH);
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
        // 2. スキル選択画面 (通常攻撃 / スペシャル攻撃) - 1〜7がすべて収まる最適化レイアウト
        // =================================================================================
        private void DrawSkillSelectScreen(float screenW, float screenH)
        {
            // パネル全体の高さを440px前後に抑え、どんな解像度でも6・7が画面内に完全に収まるようにする
            float panelW = Mathf.Min(700f, screenW - 20f);
            float panelH = Mathf.Min(440f, screenH - 20f);
            float panelX = (screenW - panelW) * 0.5f;
            float panelY = (screenH - panelH) * 0.5f;

            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), panelTex);

            float curY = panelY + 12f;

            bool isNormalSelect = (phase == Phase.P1SelectNormal || phase == Phase.P2SelectNormal);
            int playerNumber = (phase == Phase.P1SelectNormal || phase == Phase.P1SelectSpecial) ? 1 : 2;

            string titleText = isNormalSelect
                ? $"【 {playerNumber}P  通常攻撃 を選択 】"
                : $"【 {playerNumber}P  スペシャル攻撃 を選択 】";

            string subText = isNormalSelect
                ? "基本攻撃スタイル (1〜7キー または クリック)"
                : "必殺技スタイル (1〜7キー または クリック)";

            GUI.Label(new Rect(panelX, curY, panelW, 28f), titleText, titleStyle);
            curY += 28f;
            GUI.Label(new Rect(panelX, curY, panelW, 20f), subText, subtitleStyle);
            curY += 22f;

            // ボタン一覧 (7個) - 高さ35px、間隔5pxに設定（7個合計で約275px）
            float btnW = Mathf.Min(640f, panelW - 30f);
            float btnH = 35f;
            float btnSpacing = 5f;
            float btnX = panelX + (panelW - btnW) * 0.5f;

            if (isNormalSelect)
            {
                for (int i = 0; i < NormalSkills.Length; i++)
                {
                    string label = $"[ {i + 1} ]  {NormalSkills[i].name}   ―   {NormalSkills[i].summary}";
                    if (GUI.Button(new Rect(btnX, curY, btnW, btnH), label, skillButtonStyle))
                    {
                        if (playerNumber == 1)
                        {
                            p1NormalIndex = i;
                            phase = Phase.P1SelectSpecial;
                        }
                        else
                        {
                            p2NormalIndex = i;
                            phase = Phase.P2SelectSpecial;
                        }
                    }
                    curY += btnH + btnSpacing;
                }
            }
            else
            {
                for (int i = 0; i < SpecialSkills.Length; i++)
                {
                    string label = $"[ {i + 1} ]  {SpecialSkills[i].name}   ―   {SpecialSkills[i].summary}";
                    if (GUI.Button(new Rect(btnX, curY, btnW, btnH), label, skillButtonStyle))
                    {
                        if (playerNumber == 1)
                        {
                            p1SpecialIndex = i;
                            if (gameMode == GameMode.PvP)
                            {
                                phase = Phase.P2SelectNormal;
                            }
                            else
                            {
                                StartFight();
                            }
                        }
                        else
                        {
                            p2SpecialIndex = i;
                            StartFight();
                        }
                    }
                    curY += btnH + btnSpacing;
                }
            }

            // フッター
            curY += 4f;
            string buildInfo = $"1P: 通常[{NormalSkills[p1NormalIndex].name}]";
            if (phase != Phase.P1SelectNormal)
            {
                buildInfo += $" ＋ 必殺[{SpecialSkills[p1SpecialIndex].name}]";
            }
            GUI.Label(new Rect(panelX + 25f, curY, panelW - 170f, 28f), buildInfo, labelStyle);

            if (GUI.Button(new Rect(panelX + panelW - 140f, curY - 2f, 120f, 30f), "戻る [B]", skillButtonStyle))
            {
                if (phase == Phase.P1SelectNormal) phase = Phase.SelectMode;
                else if (phase == Phase.P1SelectSpecial) phase = Phase.P1SelectNormal;
                else if (phase == Phase.P2SelectNormal) phase = Phase.P1SelectSpecial;
                else if (phase == Phase.P2SelectSpecial) phase = Phase.P2SelectNormal;
            }
        }

        // =================================================================================
        // 3. バトル中のHUD
        // =================================================================================
        private void DrawFightingHUD()
        {
            var topBtnStyle = new GUIStyle(buttonStyle) { fontSize = 12 };

            // [R] モード選択へ戻るボタン (コンパクトに右上端へ配置)
            if (GUI.Button(new Rect(Screen.width - 115f, 10f, 105f, 26f), "[R] メニュー", topBtnStyle))
            {
                ReturnToModeSelect();
            }

            // テストモード時: 「サンドバッグ設定」トグルボタン＆設定ウィンドウ
            if (gameMode == GameMode.TestMode && activeSandbag != null)
            {
                // キャラ名と重ならないよう、小型化したボタン: [ ⚙ 設定 ] [TAB]
                string btnLabel = showSandbagSettings ? "[ ✕ 閉じる ] [TAB]" : "[ ⚙ 設定 ] [TAB]";
                if (GUI.Button(new Rect(10f, 10f, 125f, 26f), btnLabel, topBtnStyle))
                {
                    showSandbagSettings = !showSandbagSettings;
                }

                // ボタンが押されて開いているときだけ設定変更ウィンドウを表示
                if (showSandbagSettings)
                {
                    DrawSandbagControlPanel();
                }
            }
        }

        // =================================================================================
        // サンドバッグ設定変更ウィンドウ (コンパクトデザイン)
        // =================================================================================
        private void DrawSandbagControlPanel()
        {
            float pW = 310f;
            float pH = 215f;
            float pX = 10f;
            float pY = 40f;

            GUI.DrawTexture(new Rect(pX, pY, pW, pH), panelTex);

            float curY = pY + 10f;
            var subStyle = new GUIStyle(subtitleStyle) { fontSize = 14 };
            GUI.Label(new Rect(pX, curY, pW, 22f), "【 サンドバッグ 攻撃設定 】", subStyle);

            curY += 24f;
            string statusStr = activeSandbag.attackMode == SandbagAI.AttackMode.None ? "停止中 (棒立ち)" :
                               activeSandbag.attackMode == SandbagAI.AttackMode.Normal ? "通常攻撃リピート" : "必殺技リピート";
            var miniLabelStyle = new GUIStyle(labelStyle) { fontSize = 12 };
            GUI.Label(new Rect(pX + 10f, curY, pW - 20f, 18f), $"状態: {statusStr}", miniLabelStyle);

            curY += 22f;
            float btnW = 280f;
            float btnH = 27f;
            float btnX = pX + 15f;

            var smallBtnStyle = new GUIStyle(buttonStyle) { fontSize = 12 };

            // 1. 攻撃停止
            if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "[ 1 ] 攻撃停止 (棒立ち)", smallBtnStyle))
            {
                activeSandbag.SetAttackMode(SandbagAI.AttackMode.None);
            }

            curY += btnH + 4f;
            // 2. 通常攻撃リピート
            if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "[ 2 ] 通常攻撃リピート", smallBtnStyle))
            {
                activeSandbag.SetAttackMode(SandbagAI.AttackMode.Normal);
            }

            curY += btnH + 4f;
            // 3. スペシャル攻撃リピート
            if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "[ 3 ] スペシャル攻撃リピート", smallBtnStyle))
            {
                activeSandbag.SetAttackMode(SandbagAI.AttackMode.Special);
            }

            // 攻撃間隔の調整
            curY += btnH + 8f;
            GUI.Label(new Rect(pX + 10f, curY, pW - 20f, 18f), $"攻撃間隔:  現在 {activeSandbag.attackInterval:F1} 秒", miniLabelStyle);

            curY += 19f;
            float intervalBtnW = 64f;
            float[] intervals = { 1.0f, 1.5f, 2.0f, 3.0f };
            for (int i = 0; i < intervals.Length; i++)
            {
                float val = intervals[i];
                float ix = btnX + i * (intervalBtnW + 8f);
                bool isSelected = Mathf.Approximately(activeSandbag.attackInterval, val);
                string intLabel = isSelected ? $"★{val:F1}s" : $"{val:F1}s";
                if (GUI.Button(new Rect(ix, curY, intervalBtnW, 24f), intLabel, smallBtnStyle))
                {
                    activeSandbag.attackInterval = val;
                }
            }
        }
    }
}
