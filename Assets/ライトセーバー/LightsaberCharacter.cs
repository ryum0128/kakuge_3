using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // =================================================================================
    // 【LightsaberCharacter（ライトセーバー特化型キャラクター）】
    // このスクリプトは CharacterBase を継承しており、ライトセーバー特有の特殊能力を実装します。
    // - 通常攻撃は発生が早く、威力が高め（ダメージ15）で、どこに当てても均一ダメージ
    // - 特殊攻撃（Xキー）で「完璧なタイミングのみ」発動可能な大ダメージカウンター（威力50）
    //   - カウンター構え中 → 防御ポーズのイラストに切り替え
    //   - カウンター成功（被弾） → 光のフラッシュイラストに切り替え → 反撃斬りイラスト
    //   - カウンター終了 → 通常イラストに戻る
    // - 体力95%以上の時、敵の「飛び道具（isProjectile == true）」の攻撃を完全に無効化する
    // =================================================================================
    public class LightsaberCharacter : CharacterBase
    {
        [Header("カウンターの調整")]
        [Tooltip("カウンターの構えをとる最大時間（秒）")]
        public float counterDuration = 0.5f;

        [Tooltip("完璧なタイミング（構え開始から何秒以内に食らえばカウンターが成立するか。短いほど難易度高）")]
        public float perfectCounterWindow = 0.15f;

        [Tooltip("カウンター成功時に相手に与えるダメージ")]
        public int counterDamage = 50;

        [Header("カウンターモーション用スプライト")]
        [Tooltip("通常時のスプライト（LightsaberCharacterSprite.png）")]
        public Sprite spriteNormal;

        [Tooltip("カウンター構え中のスプライト（LightsaberSprite_CounterStance.png）")]
        public Sprite spriteCounterStance;

        [Tooltip("カウンター成功フラッシュのスプライト（LightsaberSprite_CounterFlash.png）")]
        public Sprite spriteCounterFlash;

        [Tooltip("カウンター反撃時のスプライト（LightsaberSprite_CounterAttack.png）")]
        public Sprite spriteCounterAttack;

        private bool isCounterStance = false;
        private float counterStartTime = -1f;
        private SpriteRenderer spriteRenderer;

        void Awake()
        {
            // Visuals 子オブジェクトの SpriteRenderer を取得しておく
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // スプライトを安全に切り替えるユーティリティ
        private void SetSprite(Sprite sprite)
        {
            if (spriteRenderer != null && sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        // 特殊攻撃 (X) でカウンターを起動
        public override void AttackSpecial()
        {
            if (isDead) return;

            Debug.Log("【構え】ライトセーバーでカウンターの構えに入った！（完璧なタイミングで攻撃を受けろ！）");
            
            // コルーチンを使って一定時間だけカウンター受付状態にする
            StartCoroutine(StartCounterStance());
        }

        private IEnumerator StartCounterStance()
        {
            isCounterStance = true;
            counterStartTime = Time.time;

            // ★ 構えモーションのスプライトに切り替え
            SetSprite(spriteCounterStance);

            yield return new WaitForSeconds(counterDuration);

            // 時間切れでカウンターが発動しなかった場合は通常スプライトに戻す
            isCounterStance = false;
            counterStartTime = -1f;
            SetSprite(spriteNormal);
        }

        // 被ダメージ処理をオーバーライドして、カウンター判定と飛び道具無効化を割り込む
        public override void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isDead) return;

            // --- 1. 体力が95%以上の時、飛び道具（isProjectile）を全て無効化する ---
            if (attackerHitbox != null && attackerHitbox.isProjectile)
            {
                float hpPercent = (float)currentHP / (stats != null ? stats.maxHP : 100);
                if (hpPercent >= 0.95f)
                {
                    Debug.Log("【無効】体力が95%以上のため、ライトセーバーで飛んできた攻撃を無効化しました！");
                    // 飛び道具のオブジェクトを破壊
                    if (attackerHitbox.gameObject != null)
                    {
                        Destroy(attackerHitbox.gameObject);
                    }
                    return; // ダメージもノックバックも発生させずリターン
                }
            }

            // --- 2. 完璧なタイミングのカウンター判定 ---
            if (isCounterStance && counterStartTime > 0f)
            {
                float timePassed = Time.time - counterStartTime;
                if (timePassed <= perfectCounterWindow)
                {
                    // 完璧なタイミング（0.15秒以内）でのみカウンターが発動！
                    Debug.Log($"【カウンター発動！】完璧なタイミング（被弾まで {timePassed:F3} 秒）で攻撃を受け止めました！");
                    
                    damage = 0;
                    isCounterStance = false;
                    counterStartTime = -1f;

                    // ★ カウンター成功モーションを再生してから反撃
                    StartCoroutine(PlayCounterAnimation());
                    return;
                }
                else
                {
                    // 完璧なタイミングを過ぎていたため失敗 → 通常スプライトに戻す
                    Debug.Log($"【カウンター失敗】受け流すタイミングが遅かったです（被弾まで {timePassed:F3} 秒）。通常通りダメージを受けます。");
                    isCounterStance = false;
                    counterStartTime = -1f;
                    SetSprite(spriteNormal);
                }
            }

            // 特殊能力で無効化されなかった場合は、通常通りベースの被ダメージ処理を実行する
            base.TakeDamage(damage, attackerHitbox);
        }

        // カウンター成功時のモーションシーケンス
        private IEnumerator PlayCounterAnimation()
        {
            // 1. フラッシュ（受け流した瞬間の光の爆発）
            SetSprite(spriteCounterFlash);
            yield return new WaitForSeconds(0.1f);

            // 2. 反撃攻撃スプライト
            SetSprite(spriteCounterAttack);

            // 反撃の実行（スプライト切り替えと同タイミングで相手にダメージ）
            TriggerPerfectCounterAttack();

            yield return new WaitForSeconds(0.25f);

            // 3. 通常スプライトに戻る
            SetSprite(spriteNormal);
        }

        // 相手のキャラクターを探して大ダメージを与える反撃メソッド
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
                Debug.Log($"【カウンター反撃】対戦相手（プレイヤー {targetOpponent.playerID}）に対して、{counterDamage} ダメージの強力なカウンター攻撃を叩き込みました！");
                targetOpponent.TakeDamage(counterDamage, null);
            }
        }
    }
}
