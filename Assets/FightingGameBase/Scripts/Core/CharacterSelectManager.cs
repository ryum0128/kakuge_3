using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGameBase
{
    // =================================================================================
    // 【CharacterSelectManager（キャラクターセレクト画面）】
    // 数字キー(1〜6)を使って、1P→2Pの順に武器を選択します。
    // 2P側も選び終わったら、自動で2人を配置して対戦を開始します。
    // =================================================================================
    public class CharacterSelectManager : MonoBehaviour
    {
        [System.Serializable]
        public class WeaponEntry
        {
            public string displayName;
            public GameObject prefab;
        }

        [Header("選択できる武器一覧 (1〜6キーに対応)")]
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
            // ゲーム起動・再開時：シーン上にすでに存在しているキャラクターや弾をすべて消去
            ClearAllExistingCharacters();
            ClearAllProjectiles();
        }

        void Start()
        {
            EnsureWeaponsLoaded();
            // Startでも既存キャラクターを消去して完全クリーンな状態で選択開始
            ClearAllExistingCharacters();
            ClearAllProjectiles();
        }

        public void EnsureWeaponsLoaded()
        {
#if UNITY_EDITOR
            if (weapons == null || weapons.Length == 0)
            {
                weapons = new[]
                {
                    MakeEntry("TAIKEN", "Assets/TAIKEN/Taiken.prefab"),
                    MakeEntry("ダガー", "Assets/Dagger/Dagger.prefab"),
                    MakeEntry("ライトセーバー", "Assets/ライトセーバー/LightsaberCharacter.prefab"),
                    MakeEntry("鞭", "Assets/鞭/WhipCharacter.prefab"),
                    MakeEntry("ハンマー", "Assets/hammer/Prefabs/HammerCharacter.prefab"),
                    MakeEntry("ガンランス", "Assets/gannrannsu/Prefabs/GannrannsuCharacter.prefab"),
                    MakeEntry("炎", "Assets/Flame/FlameCharacter.prefab"),
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
            string[] keywords = { "Flame", "TAIKEN", "Dagger", "Hammer", "Whip", "Lightsaber", "Gannrannsu", "Template" };
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

            p1Go.name = $"{p1.displayName}_P1";
            p2Go.name = $"{p2.displayName}_P2";

            ConfigureCharacterPlayerID(p1Go, 1);
            ConfigureCharacterPlayerID(p2Go, 2);

            // 1Pは右向き、2Pは左向き(お互いに向き合う)。動けば自動で向き直る。
            FaceDirection(p1Go.transform, 1f);
            FaceDirection(p2Go.transform, -1f);

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

            // GannrannsuInputController
            var gic = go.GetComponent<GannrannsuInputController>();
            if (gic != null)
            {
                if (playerID == 1)
                {
                    gic.leftKey = Key.A;
                    gic.rightKey = Key.D;
                    gic.jumpKey = Key.W;
                    gic.normalAttackKey = Key.K;
                    gic.specialAttackKey = Key.L;
                }
                else
                {
                    gic.leftKey = Key.LeftArrow;
                    gic.rightKey = Key.RightArrow;
                    gic.jumpKey = Key.UpArrow;
                    gic.normalAttackKey = Key.I;
                    gic.specialAttackKey = Key.O;
                }
            }

            // HammerInputController
            var hic = go.GetComponent<HammerInputController>();
            if (hic != null)
            {
                if (playerID == 1)
                {
                    hic.leftKey = Key.A;
                    hic.rightKey = Key.D;
                    hic.jumpKey = Key.W;
                    hic.normalAttackKey = Key.K;
                    hic.specialAttackKey = Key.L;
                }
                else
                {
                    hic.leftKey = Key.LeftArrow;
                    hic.rightKey = Key.RightArrow;
                    hic.jumpKey = Key.UpArrow;
                    hic.normalAttackKey = Key.I;
                    hic.specialAttackKey = Key.O;
                }
            }
        }

        private static void FaceDirection(Transform t, float sign)
        {
            Vector3 s = t.localScale;
            t.localScale = new Vector3(Mathf.Abs(s.x) * sign, s.y, s.z);
        }

        void OnGUI()
        {
            EnsureStyles();

            // 試合中：画面右上に「[R] キャラ選択へ戻る」ボタンを表示
            if (phase == Phase.Fighting)
            {
                if (GUI.Button(new Rect(Screen.width - 170f, 10f, 160f, 28f), "[R] キャラ選択へ戻る", restartButtonStyle))
                {
                    ResetToCharacterSelect();
                }
                return;
            }

            // タイトル（コンパクト配置）
            GUI.Label(new Rect(0, 15f, Screen.width, 36f), "キャラクターセレクト", titleStyle);

            // ターン案内（1P/2P）
            string turnLabel = phase == Phase.SelectP1
                ? $"1P  キャラクターを選んでください (1〜{weapons.Length})"
                : $"2P  キャラクターを選んでください (1〜{weapons.Length})";
            GUI.Label(new Rect(0, 52f, Screen.width, 24f), turnLabel, turnStyle);

            bool hasPickedText = (phase == Phase.SelectP2 && p1Choice != null);
            if (hasPickedText)
            {
                GUI.Label(new Rect(0, 78f, Screen.width, 20f), $"1P: {p1Choice.displayName}  に決定！", pickedStyle);
            }

            // ボタン一覧（行間・高さを抑えて全体的にコンパクトに）
            float rowHeight = 30f;
            float boxWidth = 300f;
            float boxX = Screen.width / 2f - boxWidth / 2f;
            float startY = hasPickedText ? 104f : 88f;

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
