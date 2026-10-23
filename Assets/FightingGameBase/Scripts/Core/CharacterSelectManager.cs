using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGameBase
{
    // =================================================================================
    // 【CharacterSelectManager（キャラクターセレクト画面）】
    // 数字キー(1〜4)を使って、1P→2Pの順に武器を選択します。
    // サンドバッグ（通常攻撃／スペシャル攻撃／停止）または2Pプレイヤー操作を選択可能。
    // =================================================================================
    public class CharacterSelectManager : MonoBehaviour
    {
        [System.Serializable]
        public class WeaponEntry
        {
            public string displayName;
            public GameObject prefab;
        }

        [Header("選択できる武器一覧")]
        public WeaponEntry[] weapons;

        [Header("配置設定")]
        [Tooltip("画面中央からの左右の距離")]
        public float spawnX = 5f;
        public float spawnY = -1f;

        private static readonly Key[] NumberKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        private static readonly Key[] NumpadKeys =
        {
            Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
        };

        public static CharacterSelectManager Instance { get; private set; }
        public static bool IsSpawningFromSelect { get; private set; } = false;

        private enum Phase { SelectP1, SelectP2, Fighting }
        private Phase phase = Phase.SelectP1;

        public enum OpponentMode
        {
            Player2,
            SandbagNormal,
            SandbagSpecial,
            SandbagNone
        }

        [Header("2P / サンドバッグの動作モード")]
        public OpponentMode opponentMode = OpponentMode.SandbagNormal;

        private GameObject currentP1;
        private GameObject currentP2;

        private WeaponEntry p1Choice;

        private GUIStyle titleStyle;
        private GUIStyle turnStyle;
        private GUIStyle pickedStyle;
        private GUIStyle buttonStyle;
        private GUIStyle restartButtonStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnGameStart()
        {
            // ゲームスタートした時点で、Hierarchyにある既存キャラクターをすべて即座に消去！
            ClearAllExistingCharacters();
            ClearAllProjectiles();

            CharacterSelectManager csm = FindAnyObjectByType<CharacterSelectManager>();
            if (csm == null)
            {
                GameObject go = new GameObject("CharacterSelectManager");
                csm = go.AddComponent<CharacterSelectManager>();
            }

            csm.EnsureWeaponsLoaded();
            csm.ResetToCharacterSelect();

            Debug.Log("【ゲームスタート】Hierarchyの既存キャラクターを全消去し、キャラクターセレクト画面に接続しました！");
        }

        void Awake()
        {
            Instance = this;
            EnsureWeaponsLoaded();
            ClearAllExistingCharacters();
            ClearAllProjectiles();
        }

        void Start()
        {
            EnsureWeaponsLoaded();
            ClearAllExistingCharacters();
            ClearAllProjectiles();
        }

        public void EnsureWeaponsLoaded()
        {
            // ガンランス・ハンマー・鞭・ライトセーバーを除外 & ダガー・炎の名称更新
            if (weapons != null && weapons.Length > 0)
            {
                var filtered = new System.Collections.Generic.List<WeaponEntry>();
                foreach (var w in weapons)
                {
                    if (w == null) continue;
                    if (w.displayName == "ガンランス" || w.displayName == "ハンマー" ||
                        w.displayName == "鞭" || w.displayName == "ライトセーバー")
                    {
                        continue;
                    }
                    if (w.displayName == "ダガー") w.displayName = "Dagger";
                    if (w.displayName == "炎") w.displayName = "Flame";
                    filtered.Add(w);
                }
                weapons = filtered.ToArray();
            }

#if UNITY_EDITOR
            if (weapons == null || weapons.Length == 0)
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
                };
            }
            else
            {
                var list = new System.Collections.Generic.List<WeaponEntry>(weapons);
                bool modified = false;

                bool hasThunder = list.Exists(w => w != null && w.displayName == "Thunder");
                if (!hasThunder)
                {
                    list.Add(MakeEntry("Thunder", "Assets/Thunder/ThunderCharacter.prefab"));
                    modified = true;
                }

                bool hasIce = list.Exists(w => w != null && w.displayName == "Ice");
                if (!hasIce)
                {
                    list.Add(MakeEntry("Ice", "Assets/Ice/IceCharacter.prefab"));
                    modified = true;
                }

                bool hasShadow = list.Exists(w => w != null && w.displayName == "Shadow");
                if (!hasShadow)
                {
                    list.Add(MakeEntry("Shadow", "Assets/Shadow/ShadowCharacter.prefab"));
                    modified = true;
                }

                if (modified)
                {
                    weapons = list.ToArray();
                }
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
            if (Keyboard.current == null) return;

            // 試合中：Rキーでいつでもキャラクター全消去＆キャラクターセレクトに戻れる
            if (phase == Phase.Fighting)
            {
                if (Keyboard.current.rKey.wasPressedThisFrame)
                {
                    ResetToCharacterSelect();
                }
                return;
            }

            for (int i = 0; i < weapons.Length && i < NumberKeys.Length; i++)
            {
                bool pressed = Keyboard.current[NumberKeys[i]].wasPressedThisFrame;
                if (!pressed && i < NumpadKeys.Length)
                {
                    pressed = Keyboard.current[NumpadKeys[i]].wasPressedThisFrame;
                }

                if (pressed)
                {
                    OnWeaponChosen(weapons[i]);
                    break;
                }
            }
        }

        /// <summary>
        /// 既存のキャラクターをすべて完全に消去して、キャラクターセレクト画面に繋げる
        /// </summary>
        public void ResetToCharacterSelect()
        {
            // 1. シーン上に存在するすべてのキャラクターを削除
            ClearAllExistingCharacters();

            // 2. 画面に残っている弾や飛び道具（FireBall等）を消去
            ClearAllProjectiles();

            // 3. GameManager の状態をリセット
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResetGame();
            }

            // 4. HUDManager の GameResult オーバーレイやゲージ類をリセット
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ResetHUD();
            }

            // 5. キャラクターセレクト画面の状態（1P選択フェーズ）に戻す
            phase = Phase.SelectP1;
            p1Choice = null;
            currentP1 = null;
            currentP2 = null;

            Debug.Log("【ゲーム再開】既存キャラクターをすべて消去し、キャラクターセレクト画面に接続しました！");
        }

        /// <summary>
        /// シーン内のすべてのキャラクター（アクティブ・非アクティブ問わず）を消去する
        /// </summary>
        public static void ClearAllExistingCharacters()
        {
            CharacterBase[] allChars = FindObjectsByType<CharacterBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in allChars)
            {
                if (c != null && c.gameObject != null)
                {
                    Destroy(c.gameObject);
                }
            }

            // ルートオブジェクトからもキャラクター関連プレハブを検出して確実に消去
            string[] keywords = { "Flame", "TAIKEN", "Dagger", "Hammer", "Whip", "Lightsaber", "Gannrannsu", "Template", "Thunder" };
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in roots)
            {
                if (r == null) continue;
                if (r.name.Contains("CharacterSelectManager") || r.name.Contains("GameManager") || r.name.Contains("HUDManager") || r.name.Contains("Camera") || r.name.Contains("Floor") || r.name.Contains("Wall"))
                {
                    continue;
                }

                if (r.GetComponentInChildren<CharacterBase>(true) != null)
                {
                    Destroy(r);
                    continue;
                }

                foreach (var kw in keywords)
                {
                    if (r.name.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Destroy(r);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 画面上に残っている飛び道具を消去する
        /// </summary>
        public static void ClearAllProjectiles()
        {
            Hitbox[] hitboxes = FindObjectsByType<Hitbox>(FindObjectsSortMode.None);
            foreach (var hb in hitboxes)
            {
                if (hb != null && hb.isProjectile && hb.gameObject != null)
                {
                    Destroy(hb.gameObject);
                }
            }
        }

        private void OnWeaponChosen(WeaponEntry chosen)
        {
            if (chosen == null || chosen.prefab == null) return;

            if (phase == Phase.SelectP1)
            {
                p1Choice = chosen;
                phase = Phase.SelectP2;
            }
            else if (phase == Phase.SelectP2)
            {
                StartFight(p1Choice, chosen);
            }
        }

        private void StartFight(WeaponEntry p1, WeaponEntry p2)
        {
            phase = Phase.Fighting;

            IsSpawningFromSelect = true;
            GameObject p1Go = Instantiate(p1.prefab, new Vector3(-spawnX, spawnY, 0f), Quaternion.identity);
            GameObject p2Go = Instantiate(p2.prefab, new Vector3(spawnX, spawnY, 0f), Quaternion.identity);
            IsSpawningFromSelect = false;

            currentP1 = p1Go;
            currentP2 = p2Go;

            p1Go.name = $"{p1.displayName}_P1";
            p2Go.name = $"{p2.displayName}_P2";

            ConfigureCharacterPlayerID(p1Go, 1);
            ConfigureCharacterPlayerID(p2Go, 2);

            // 1Pは右向き、2Pは左向き(お互いに向き合う)。動けば自動で向き直る。
            FaceDirection(p1Go.transform, 1f);
            FaceDirection(p2Go.transform, -1f);

            // 2Pがサンドバッグ設定の場合の処理
            if (opponentMode != OpponentMode.Player2)
            {
                // プレイヤーのキー入力を無効化（誤操作・二重入力防止）
                PlayerInputController pic = p2Go.GetComponent<PlayerInputController>();
                if (pic != null) Destroy(pic);

                // 個別コントローラーがあれば削除
                var otherControllers = p2Go.GetComponents<MonoBehaviour>();
                foreach (var c in otherControllers)
                {
                    if (c == null) continue;
                    string typeName = c.GetType().Name;
                    if (typeName.EndsWith("InputController") && typeName != "GamepadInputController")
                    {
                        Destroy(c);
                    }
                }

                SandbagAI sb = p2Go.GetComponent<SandbagAI>();
                if (sb == null) sb = p2Go.AddComponent<SandbagAI>();
                sb.isImmortal = true;
                sb.attackInterval = 2.0f; // 2秒に1回繰り返す

                if (opponentMode == OpponentMode.SandbagNormal)
                {
                    sb.SetAttackMode(SandbagAI.AttackMode.Normal);
                }
                else if (opponentMode == OpponentMode.SandbagSpecial)
                {
                    sb.SetAttackMode(SandbagAI.AttackMode.Special);
                }
                else
                {
                    sb.SetAttackMode(SandbagAI.AttackMode.None);
                }
            }

            if (GameManager.Instance == null)
            {
                GameObject gmGo = new GameObject("GameManager");
                gmGo.AddComponent<GameManager>();
                GameManager.Instance.StartBattle();
            }
            else
            {
                GameManager.Instance.StartBattle();
            }
        }

        private static void ConfigureCharacterPlayerID(GameObject go, int playerID)
        {
            CharacterBase charBase = go.GetComponent<CharacterBase>();
            if (charBase != null)
            {
                charBase.playerID = playerID;
            }

            // PlayerInputController
            PlayerInputController pic = go.GetComponent<PlayerInputController>();
            if (pic != null)
            {
                pic.SetupKeys(playerID);
            }

            // その他のコントローラーがあれば必要に応じて設定可能
        }

        private static void FaceDirection(Transform t, float sign)
        {
            Vector3 s = t.localScale;
            t.localScale = new Vector3(Mathf.Abs(s.x) * sign, s.y, s.z);
        }

        void OnGUI()
        {
            EnsureStyles();

            // 試合中
            if (phase == Phase.Fighting)
            {
                if (GUI.Button(new Rect(Screen.width - 170f, 10f, 160f, 28f), "[R] キャラ選択へ戻る", restartButtonStyle))
                {
                    ResetToCharacterSelect();
                }

                // サンドバッグ操作パネル（対戦中でもいつでも通常/スペシャル/停止を切り替え可能）
                SandbagAI sb = currentP2 != null ? currentP2.GetComponent<SandbagAI>() : null;
                if (sb != null)
                {
                    GUI.Box(new Rect(15f, 10f, 360f, 90f), "【サンドバッグ攻撃設定 (2秒間隔)】");

                    Color origColor = GUI.color;

                    GUI.color = sb.attackMode == SandbagAI.AttackMode.Normal ? Color.green : Color.white;
                    if (GUI.Button(new Rect(25f, 35f, 100f, 26f), sb.attackMode == SandbagAI.AttackMode.Normal ? "★通常攻撃" : "通常攻撃"))
                    {
                        sb.SetAttackMode(SandbagAI.AttackMode.Normal);
                        opponentMode = OpponentMode.SandbagNormal;
                    }

                    GUI.color = sb.attackMode == SandbagAI.AttackMode.Special ? Color.yellow : Color.white;
                    if (GUI.Button(new Rect(135f, 35f, 115f, 26f), sb.attackMode == SandbagAI.AttackMode.Special ? "★スペシャル" : "スペシャル攻撃"))
                    {
                        sb.SetAttackMode(SandbagAI.AttackMode.Special);
                        opponentMode = OpponentMode.SandbagSpecial;
                    }

                    GUI.color = sb.attackMode == SandbagAI.AttackMode.None ? new Color(0.8f, 0.8f, 0.8f) : Color.white;
                    if (GUI.Button(new Rect(260f, 35f, 95f, 26f), sb.attackMode == SandbagAI.AttackMode.None ? "★停止" : "攻撃停止"))
                    {
                        sb.SetAttackMode(SandbagAI.AttackMode.None);
                        opponentMode = OpponentMode.SandbagNone;
                    }

                    GUI.color = origColor;

                    string statusStr = sb.attackMode == SandbagAI.AttackMode.Normal ? "現在: 通常攻撃を2秒ごとにリピート中" :
                                      (sb.attackMode == SandbagAI.AttackMode.Special ? "現在: スペシャル攻撃を2秒ごとにリピート中" : "現在: 攻撃停止中（棒立ち）");
                    GUI.Label(new Rect(25f, 65f, 340f, 22f), statusStr);
                }
                return;
            }

            // タイトル
            GUI.Label(new Rect(0, 10f, Screen.width, 36f), "キャラクターセレクト", titleStyle);

            // サンドバッグ／2Pモード選択バー（キャラクター選択画面上部に大きく配置）
            float modeBarY = 48f;
            GUI.Label(new Rect(0, modeBarY, Screen.width, 22f), "【2P / サンドバッグの動作選択】", turnStyle);

            float btnW = 155f;
            float totalBtnW = btnW * 3 + 20f;
            float startBtnX = (Screen.width - totalBtnW) / 2f;

            Color oldColor = GUI.color;

            // 通常攻撃リピート
            GUI.color = (opponentMode == OpponentMode.SandbagNormal) ? Color.green : Color.white;
            if (GUI.Button(new Rect(startBtnX, modeBarY + 24f, btnW, 28f), (opponentMode == OpponentMode.SandbagNormal ? "★ " : "") + "サンドバッグ: 通常攻撃"))
            {
                opponentMode = OpponentMode.SandbagNormal;
            }

            // スペシャル攻撃リピート
            GUI.color = (opponentMode == OpponentMode.SandbagSpecial) ? Color.yellow : Color.white;
            if (GUI.Button(new Rect(startBtnX + btnW + 10f, modeBarY + 24f, btnW, 28f), (opponentMode == OpponentMode.SandbagSpecial ? "★ " : "") + "サンドバッグ: スペシャル"))
            {
                opponentMode = OpponentMode.SandbagSpecial;
            }

            // 通常2P操作
            GUI.color = (opponentMode == OpponentMode.Player2) ? Color.cyan : Color.white;
            if (GUI.Button(new Rect(startBtnX + (btnW + 10f) * 2, modeBarY + 24f, btnW, 28f), (opponentMode == OpponentMode.Player2 ? "★ " : "") + "2Pプレイヤー操作"))
            {
                opponentMode = OpponentMode.Player2;
            }
            GUI.color = oldColor;

            // ターン案内（1P/2P）
            string turnLabel;
            if (phase == Phase.SelectP1)
            {
                turnLabel = $"1P  キャラクターを選んでください (1〜{weapons.Length})";
            }
            else
            {
                if (opponentMode == OpponentMode.SandbagNormal)
                    turnLabel = $"サンドバッグ (通常攻撃 2秒間隔) の武器を選んでください (1〜{weapons.Length})";
                else if (opponentMode == OpponentMode.SandbagSpecial)
                    turnLabel = $"サンドバッグ (スペシャル攻撃 2秒間隔) の武器を選んでください (1〜{weapons.Length})";
                else
                    turnLabel = $"2P  キャラクターを選んでください (1〜{weapons.Length})";
            }
            GUI.Label(new Rect(0, modeBarY + 58f, Screen.width, 24f), turnLabel, turnStyle);

            bool hasPickedText = (phase == Phase.SelectP2 && p1Choice != null);
            if (hasPickedText)
            {
                GUI.Label(new Rect(0, modeBarY + 82f, Screen.width, 20f), $"1P: {p1Choice.displayName}  に決定！", pickedStyle);
            }

            // ボタン一覧（行間・高さを抑えて全体的にコンパクトに）
            float rowHeight = 30f;
            float boxWidth = 320f;
            float boxX = Screen.width / 2f - boxWidth / 2f;
            float startY = hasPickedText ? (modeBarY + 106f) : (modeBarY + 90f);

            for (int i = 0; i < weapons.Length; i++)
            {
                Rect r = new Rect(boxX, startY + i * rowHeight, boxWidth, 25f);
                string label = $"[{i + 1}]   {weapons[i].displayName}";
                if (GUI.Button(r, label, buttonStyle))
                {
                    OnWeaponChosen(weapons[i]);
                }
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;

            turnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter
            };
            turnStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

            pickedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            pickedStyle.normal.textColor = new Color(0.4f, 1f, 0.5f);

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };

            restartButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
