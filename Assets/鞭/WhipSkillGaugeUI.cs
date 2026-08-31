using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【WhipSkillGaugeUI（鞭キャラ固有スキルゲージの画面表示）】
    // 鞭キャラクターのスキルゲージを画面に表示するスクリプトです。
    // OnGUI（Unity組み込みの簡易UI機能）を使って描画します。
    // ゲージが満タンになると「READY!」と表示され、発動中は残り時間を表示します。
    // =================================================================================
    public class WhipSkillGaugeUI : MonoBehaviour
    {
        [Header("表示設定")]
        [Tooltip("ゲージの幅（ピクセル）")]
        public float gaugeWidth = 200f;

        [Tooltip("ゲージの高さ（ピクセル）")]
        public float gaugeHeight = 20f;

        [Tooltip("画面下端からの距離（ピクセル）")]
        public float bottomMargin = 100f;

        [Tooltip("ラベルのフォントサイズ")]
        public int fontSize = 14;

        // 内部で使うテクスチャ
        private Texture2D bgTexture;           // ゲージの背景
        private Texture2D chargeFillTexture;   // チャージ中の色（紫）
        private Texture2D readyTexture;        // 満タン時の色（マゼンタ）
        private Texture2D activeFillTexture;   // 発動中の残り時間表示色（ピンク）
        private Texture2D outlineTexture;      // 枠線の色
        private GUIStyle labelStyle;           // ラベル用のスタイル
        private GUIStyle gaugeTextStyle;       // ゲージ内テキスト用のスタイル

        void Start()
        {
            // 各色のテクスチャを作成
            bgTexture = MakeTexture(new Color(0.15f, 0.15f, 0.15f, 0.8f));
            chargeFillTexture = MakeTexture(new Color(0.7f, 0.3f, 0.9f, 1f));   // 紫
            readyTexture = MakeTexture(new Color(1f, 0f, 0.6f, 1f));            // マゼンタ
            activeFillTexture = MakeTexture(new Color(1f, 0.5f, 0.7f, 1f));     // ピンク
            outlineTexture = MakeTexture(new Color(0.9f, 0.7f, 1f, 0.6f));      // 薄紫の枠線

            // ラベルスタイル
            labelStyle = new GUIStyle();
            labelStyle.fontSize = fontSize;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            // ゲージ内テキスト
            gaugeTextStyle = new GUIStyle();
            gaugeTextStyle.fontSize = fontSize - 2;
            gaugeTextStyle.fontStyle = FontStyle.Bold;
            gaugeTextStyle.normal.textColor = Color.white;
            gaugeTextStyle.alignment = TextAnchor.MiddleCenter;
        }

        void OnGUI()
        {
            if (labelStyle == null) return;

            // シーン上のすべての鞭キャラクターを探す
            WhipCharacter[] whipCharacters = FindObjectsByType<WhipCharacter>(FindObjectsSortMode.None);

            foreach (WhipCharacter whip in whipCharacters)
            {
                if (whip.isDead) continue;
                
                // スキル回数を使い切り、かつ発動も終了している場合はゲージを非表示（消失）にする
                if (whip.SkillUsesRemaining <= 0 && !whip.IsSkillActive) continue;

                DrawSkillGauge(whip);
            }
        }

        /// <summary>
        /// 1人分のスキルゲージを描画します
        /// </summary>
        private void DrawSkillGauge(WhipCharacter whip)
        {
            // ゲージのX座標を計算（1Pは左側、2Pは右側）
            float xPos;
            string label;

            if (whip.playerID == 1)
            {
                xPos = 20f;
                label = "1P 蛇鞭覚醒";
            }
            else
            {
                xPos = Screen.width - gaugeWidth - 20f;
                label = "2P 蛇鞭覚醒";
            }

            // ゲージのY座標（画面下部、スタンゲージの下）
            float yPos = Screen.height - bottomMargin;

            // --- ラベルを描画 ---
            Rect labelRect = new Rect(xPos, yPos - 22f, gaugeWidth, 20f);
            GUI.Label(labelRect, label, labelStyle);

            // --- ゲージ背景を描画 ---
            Rect bgRect = new Rect(xPos, yPos, gaugeWidth, gaugeHeight);
            GUI.DrawTexture(bgRect, bgTexture);

            // --- ゲージの中身を描画 ---
            if (whip.IsSkillActive)
            {
                // 発動中: 残り時間を表示（スキル効果の残り時間バーとして表示）
                // SkillGaugeは発動中0のため、別途残り時間の概算を表示
                Rect fillRect = new Rect(xPos, yPos, gaugeWidth, gaugeHeight);
                GUI.DrawTexture(fillRect, activeFillTexture);

                gaugeTextStyle.normal.textColor = Color.white;
                GUI.Label(bgRect, "覚醒中！", gaugeTextStyle);
            }
            else if (whip.IsSkillReady)
            {
                // ゲージ満タン: READY!
                Rect fillRect = new Rect(xPos, yPos, gaugeWidth, gaugeHeight);
                GUI.DrawTexture(fillRect, readyTexture);

                // 点滅演出（READY!テキスト）
                float blink = Mathf.PingPong(Time.time * 3f, 1f);
                gaugeTextStyle.normal.textColor = Color.Lerp(Color.white, Color.yellow, blink);
                GUI.Label(bgRect, "READY! [スキルキーで発動]", gaugeTextStyle);
            }
            else
            {
                // チャージ中
                float fillAmount = Mathf.Clamp01(whip.SkillGauge);
                if (fillAmount > 0f)
                {
                    Rect fillRect = new Rect(xPos, yPos, gaugeWidth * fillAmount, gaugeHeight);
                    GUI.DrawTexture(fillRect, chargeFillTexture);
                }

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
