using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // =================================================================================
    // 【KenshiCharacter（剣士キャラクター）】
    // このスクリプトは CharacterBase を継承した、剣士タイプのオリジナルキャラクターです。
    //
    // ■ 特徴
    // - 通常攻撃：発生が早く、威力が高め（ダメージ18）。どこに当てても均一ダメージ。
    // - 特殊攻撃（Xキー）：「完璧なタイミング」でしか発動しないカウンター（大ダメージ60）
    //     ・構え中 → 防御ポーズのスプライトに切り替え
    //     ・カウンター成功 → フラッシュ → 反撃斬りスプライト → 通常に戻る
    //     ・タイミングを外すと失敗し、通常通りダメージを受ける
    // - 体力が95%以上の時、敵の飛び道具（isProjectile == true）を完全に無効化する
    // - 体力が低い（HP85）分、素早い攻撃と強力なカウンターが武器！
    // =================================================================================
    public class KenshiCharacter : CharacterBase
    {
        // =============================================
        // カウンターの設定
        // =============================================
        [Header("カウンターの調整")]
        [Tooltip("カウンターの構えをとる最大時間（秒）。この時間内に攻撃を受けないとカウンター失敗。")]
        public float counterDuration = 0.5f;

        [Tooltip("完璧なタイミングの受付時間（秒）。構え開始からこの秒数以内に攻撃を受けた時だけカウンターが成立。短いほど難しい！")]
        public float perfectCounterWindow = 0.15f;

        [Tooltip("カウンター成功時に相手に与えるダメージ")]
        public int counterDamage = 60;

        // =============================================
        // カウンターモーション用スプライト
        // =============================================
        [Header("カウンターモーション用スプライト")]
        [Tooltip("通常時のスプライト（KenshiCharacterSprite.png）")]
        public Sprite spriteNormal;

        [Tooltip("カウンター構え中のスプライト（KenshiSprite_CounterStance.png）")]
        public Sprite spriteCounterStance;

        [Tooltip("カウンター成功フラッシュのスプライト（KenshiSprite_CounterFlash.png）")]
        public Sprite spriteCounterFlash;

        [Tooltip("カウンター反撃時のスプライト（KenshiSprite_CounterAttack.png）")]
        public Sprite spriteCounterAttack;

        // =============================================
        // 内部変数（プログラム内部でだけ使う変数）
        // =============================================
        private bool isCounterStance = false;    // 現在カウンター構え中かどうか
        private float counterStartTime = -1f;    // カウンター構えを始めた時刻（Time.time）
        private SpriteRenderer spriteRenderer;   // 見た目（スプライト）を操作するためのコンポーネント

        void Awake()
        {
            // 子オブジェクト（Visuals）に付いている SpriteRenderer を取得しておく
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // スプライトを安全に切り替えるユーティリティメソッド
        private void SetSprite(Sprite sprite)
        {
            if (spriteRenderer != null && sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        // =============================================
        // 特殊攻撃（Xキー）でカウンター構えを起動
        // =============================================
        public override void AttackSpecial()
        {
            if (isDead) return;

            Debug.Log("【剣士・構え】カウンターの構えに入った！（完璧なタイミングで攻撃を受けろ！）");

            // コルーチンを使って一定時間だけカウンター受付状態にする
            StartCoroutine(StartCounterStance());
        }

        private IEnumerator StartCounterStance()
        {
            // カウンター状態をオン・開始時刻を記録
            isCounterStance = true;
            counterStartTime = Time.time;

            // ★ 構えモーションのスプライトに切り替え
            SetSprite(spriteCounterStance);

            // counterDuration 秒だけ待つ（この間に攻撃を受けたらカウンターが成立する可能性がある）
            yield return new WaitForSeconds(counterDuration);

            // 時間切れでカウンターが発動しなかった場合は通常スプライトに戻す
            isCounterStance = false;
            counterStartTime = -1f;
            SetSprite(spriteNormal);
        }

        // =============================================
        // 被ダメージ処理（オーバーライドして特殊能力を割り込む）
        // =============================================
        public override void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isDead) return;

            // --- 能力1：体力95%以上の時、飛び道具（isProjectile）を全て無効化 ---
            if (attackerHitbox != null && attackerHitbox.isProjectile)
            {
                float hpPercent = (float)currentHP / (stats != null ? stats.maxHP : 100);
                if (hpPercent >= 0.95f)
                {
                    Debug.Log("【剣士・無効化】体力が95%以上のため、飛び道具を剣で弾き返した！");

                    // 飛び道具のオブジェクトを破壊する
                    if (attackerHitbox.gameObject != null)
                    {
                        Destroy(attackerHitbox.gameObject);
                    }
                    return; // ダメージなしでリターン
                }
            }

            // --- 能力2：完璧なタイミングのカウンター判定 ---
            if (isCounterStance && counterStartTime > 0f)
            {
                float timePassed = Time.time - counterStartTime;

                if (timePassed <= perfectCounterWindow)
                {
                    // ✅ 完璧なタイミング！（perfectCounterWindow 秒以内）カウンター発動！
                    Debug.Log($"【剣士・カウンター発動！】完璧なタイミング（被弾まで {timePassed:F3} 秒）で攻撃を受け流した！");

                    damage = 0;                  // このダメージは0にする（食らわない！）
                    isCounterStance = false;
                    counterStartTime = -1f;

                    // ★ カウンター成功のモーションシーケンスを再生してから反撃
                    StartCoroutine(PlayCounterAnimation());
                    return;
                }
                else
                {
                    // ❌ タイミングを外した → 失敗。通常のダメージを受ける。
                    Debug.Log($"【剣士・カウンター失敗】受け流すタイミングが遅かった（被弾まで {timePassed:F3} 秒）。ダメージを受けます。");
                    isCounterStance = false;
                    counterStartTime = -1f;
                    SetSprite(spriteNormal);
                }
            }

            // 特殊能力で無効化されなかった場合は、ベースの被ダメージ処理を実行
            base.TakeDamage(damage, attackerHitbox);
        }

        // =============================================
        // カウンター成功時のモーションシーケンス
        // =============================================
        private IEnumerator PlayCounterAnimation()
        {
            // 1. フラッシュ（受け流した瞬間の光の爆発）
            SetSprite(spriteCounterFlash);
            yield return new WaitForSeconds(0.1f);

            // 2. 反撃攻撃スプライトに切り替えて、同タイミングで相手にダメージを与える
            SetSprite(spriteCounterAttack);
            TriggerPerfectCounterAttack();

            yield return new WaitForSeconds(0.25f);

            // 3. 通常スプライトに戻る
            SetSprite(spriteNormal);
        }

        // 相手を探して大ダメージを与える反撃メソッド
        private void TriggerPerfectCounterAttack()
        {
            CharacterBase[] players = Object.FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            CharacterBase targetOpponent = null;

            foreach (var player in players)
            {
                if (player.playerID != this.playerID)
                {
                    targetOpponent = player;
                    break;
                }
            }

            if (targetOpponent != null)
            {
                Debug.Log($"【剣士・反撃】プレイヤー {targetOpponent.playerID} に {counterDamage} ダメージの反撃斬り！");
                targetOpponent.TakeDamage(counterDamage, null);
            }
        }
    }
}
