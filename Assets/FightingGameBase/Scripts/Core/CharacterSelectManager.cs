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
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6
        };

        private enum Phase { SelectP1, SelectP2, Fighting }
        private Phase phase = Phase.SelectP1;

        private WeaponEntry p1Choice;

        private GUIStyle titleStyle;
        private GUIStyle turnStyle;
        private GUIStyle pickedStyle;
        private GUIStyle buttonStyle;

        void Update()
        {
            if (phase == Phase.Fighting) return;
            if (Keyboard.current == null) return;

            for (int i = 0; i < weapons.Length && i < NumberKeys.Length; i++)
            {
                if (Keyboard.current[NumberKeys[i]].wasPressedThisFrame)
                {
                    OnWeaponChosen(weapons[i]);
                    break;
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

            GameObject p1Go = Instantiate(p1.prefab, new Vector3(-spawnX, spawnY, 0f), Quaternion.identity);
            GameObject p2Go = Instantiate(p2.prefab, new Vector3(spawnX, spawnY, 0f), Quaternion.identity);
            p1Go.name = p1.displayName;
            p2Go.name = p2.displayName;

            // 1Pは右向き、2Pは左向き(お互いに向き合う)。動けば自動で向き直る。
            FaceDirection(p1Go.transform, 1f);
            FaceDirection(p2Go.transform, -1f);

            if (GameManager.Instance == null)
            {
                GameObject gmGo = new GameObject("GameManager");
                gmGo.AddComponent<GameManager>();
            }
        }

        private static void FaceDirection(Transform t, float sign)
        {
            Vector3 s = t.localScale;
            t.localScale = new Vector3(Mathf.Abs(s.x) * sign, s.y, s.z);
        }

        void OnGUI()
        {
            if (phase == Phase.Fighting) return;

            EnsureStyles();

            GUI.Label(new Rect(0, 40, Screen.width, 60), "キャラクターセレクト", titleStyle);

            string turnLabel = phase == Phase.SelectP1
                ? "1P  武器を選んでください (数字キー 1〜6)"
                : "2P  武器を選んでください (数字キー 1〜6)";
            GUI.Label(new Rect(0, 105, Screen.width, 36), turnLabel, turnStyle);

            if (phase == Phase.SelectP2 && p1Choice != null)
            {
                GUI.Label(new Rect(0, 140, Screen.width, 30), $"1P: {p1Choice.displayName}  に決定！", pickedStyle);
            }

            float rowHeight = 42f;
            float boxWidth = 380f;
            float boxX = Screen.width / 2f - boxWidth / 2f;
            float startY = 190f;

            for (int i = 0; i < weapons.Length; i++)
            {
                Rect r = new Rect(boxX, startY + i * rowHeight, boxWidth, rowHeight - 8f);
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
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;

            turnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter
            };
            turnStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

            pickedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            pickedStyle.normal.textColor = new Color(0.4f, 1f, 0.5f);

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
