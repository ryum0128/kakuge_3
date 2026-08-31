using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【StunGaugeUI（スタンゲージの画面表示）】
    // このスクリプトをシーン内の任意のGameObjectにアタッチすると、
    // 各プレイヤーのスタンチャージゲージが画面に表示されます。
    // Canvas不要で、OnGUI（Unity組み込みの簡易UI機能）を使って描画します。
    // =================================================================================
    public class StunGaugeUI : MonoBehaviour
    {
        [Header("表示設定")]
        [Tooltip("ゲージの幅（ピクセル）")]
        public float gaugeWidth = 200f;

        [Tooltip("ゲージの高さ（ピクセル）")]
        public float gaugeHeight = 20f;

        [Tooltip("画面下端からの距離（ピクセル）")]
        public float bottomMargin = 60f;

        [Tooltip("ラベルのフォントサイズ")]
        public int fontSize = 14;

        // 内部で使うテクスチャ（ゲージの色）
        private Texture2D bgTexture;       // ゲージの背景（暗い色）
        private Texture2D fillTexture;     // チャージ中の色（黄色）
        private Texture2D readyTexture;    // 満タン時の色（緑）
        private Texture2D usedTexture;     // 使用済みの色（灰色）
        private Texture2D outlineTexture;  // 枠線の色
        private GUIStyle labelStyle;       // ラベル用のスタイル
        private GUIStyle gaugeTextStyle;   // ゲージ内テキスト用のスタイル

        void Start()
        {
            // 各色のテクスチャ（1x1ピクセルの塗りつぶし画像）を作成します
            bgTexture = MakeTexture(new Color(0.15f, 0.15f, 0.15f, 0.8f));
            fillTexture = MakeTexture(new Color(1f, 0.85f, 0f, 1f));
            readyTexture = MakeTexture(new Color(0f, 1f, 0.3f, 1f));
            usedTexture = MakeTexture(new Color(0.4f, 0.4f, 0.4f, 0.6f));
            outlineTexture = MakeTexture(new Color(0.8f, 0.8f, 0.8f, 0.5f));

            // ラベル用のスタイルを設定
            labelStyle = new GUIStyle();
            labelStyle.fontSize = fontSize;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            // ゲージ内テキスト用のスタイル
            gaugeTextStyle = new GUIStyle();
            gaugeTextStyle.fontSize = fontSize - 2;
            gaugeTextStyle.fontStyle = FontStyle.Bold;
            gaugeTextStyle.normal.textColor = Color.white;
            gaugeTextStyle.alignment = TextAnchor.MiddleCenter;
        }

        void OnGUI()
        {
            if (labelStyle == null) return; // Start前に呼ばれた場合の対策

            // シーン上のすべてのキャラクターを探す
            CharacterBase[] characters = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);

            foreach (CharacterBase character in characters)
            {
                // スタンスキルを使えるキャラクター（ライトセーバー）のみゲージを表示
                if (!character.CanUseStunSkill) continue;

                // 全回数使用済みならゲージを完全に消失させる
                if (character.HasUsedStun) continue;

                DrawStunGauge(character);
            }
        }

        /// <summary>
        /// 1人分のスタンチャージゲージを描画します
        /// </summary>
        private void DrawStunGauge(CharacterBase character)
        {
            // ゲージのX座標を計算（1Pは左側、2Pは右側）
            float xPos;
            string label;

            if (character.playerID == 1)
            {
                xPos = 20f;        // 1P: 画面左側
                label = $"1P スタン (残り{character.StunUsesRemaining}回)";
            }
            else
            {
                xPos = Screen.width - gaugeWidth - 20f; // 2P: 画面右側
                label = $"2P スタン (残り{character.StunUsesRemaining}回)";
            }

            // ゲージのY座標（画面下部）
            float yPos = Screen.height - bottomMargin;

            // --- ラベルを描画 ---
            Rect labelRect = new Rect(xPos, yPos - 22f, gaugeWidth, 20f);
            GUI.Label(labelRect, label, labelStyle);

            // --- ゲージ背景を描画 ---
            Rect bgRect = new Rect(xPos, yPos, gaugeWidth, gaugeHeight);
            GUI.DrawTexture(bgRect, bgTexture);

            // --- ゲージの中身を描画 ---
            float fillAmount = Mathf.Clamp01(character.stunChargeGauge);

            if (fillAmount >= 1f)
            {
                // 満タン → 緑色で全体を塗る
                Rect fillRect = new Rect(xPos, yPos, gaugeWidth, gaugeHeight);
                GUI.DrawTexture(fillRect, readyTexture);

                // テキスト表示：「READY!」
                gaugeTextStyle.normal.textColor = new Color(0.8f, 1f, 0.8f);
                GUI.Label(bgRect, "READY!", gaugeTextStyle);
            }
            else
            {
                // チャージ中 → 黄色でゲージを途中まで塗る
                if (fillAmount > 0f)
                {
                    Rect fillRect = new Rect(xPos, yPos, gaugeWidth * fillAmount, gaugeHeight);
                    GUI.DrawTexture(fillRect, fillTexture);
                }

                // テキスト表示：パーセント
                gaugeTextStyle.normal.textColor = Color.white;
                GUI.Label(bgRect, $"{fillAmount * 100f:F0}%", gaugeTextStyle);
            }

            // --- 枠線を描画 ---
            float lw = 2f;
            GUI.DrawTexture(new Rect(bgRect.x, bgRect.y, bgRect.width, lw), outlineTexture);                          // 上
            GUI.DrawTexture(new Rect(bgRect.x, bgRect.y + bgRect.height - lw, bgRect.width, lw), outlineTexture);     // 下
            GUI.DrawTexture(new Rect(bgRect.x, bgRect.y, lw, bgRect.height), outlineTexture);                          // 左
            GUI.DrawTexture(new Rect(bgRect.x + bgRect.width - lw, bgRect.y, lw, bgRect.height), outlineTexture);     // 右
        }

        /// <summary>
        /// 1x1ピクセルの単色テクスチャを作るヘルパー関数
        /// </summary>
        private Texture2D MakeTexture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
