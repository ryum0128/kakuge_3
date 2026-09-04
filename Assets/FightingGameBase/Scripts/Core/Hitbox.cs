using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【Hitbox（ヒットボックス・攻撃判定）】
    // このスクリプトはキャラクターの「拳」や「武器」などの攻撃が当たる部分にくっつけます。
    // [RequireComponent(typeof(Collider2D))] と書くと、このスクリプトを付けたときに
    // 自動的に「Collider2D（当たり判定の枠）」も一緒に追加してくれます！
    // =================================================================================
    [RequireComponent(typeof(Collider2D))]
    public class Hitbox : MonoBehaviour
    {
        [Tooltip("攻撃力（この攻撃が当たったらどれくらいダメージを与えるか）")]
        public int damage = 10;
        
        [Tooltip("誰の攻撃判定か（1ならプレイヤー1。自分自身には当たらないようにするためです）")]
        public int ownerPlayerID = 1;

        [Tooltip("ガードされた時の体幹値（Posture）蓄積倍率")]
        public float postureDamageMultiplier = 1.5f;

        [Tooltip("通常攻撃であるかどうか（当たった時のマナ回復判定に用います）")]
        public bool isNormalAttack = true;

        [Tooltip("これが飛び道具（遠距離攻撃）かどうか")]
        public bool isProjectile = false;

        // =========================================================
        // 距離ベースダメージ補正（鞭など、先端が強く根元が弱い武器用）
        // =========================================================
        [Header("距離ベースダメージ補正")]
        [Tooltip("有効にすると、攻撃の当たった位置がオーナーから遠いほどダメージが高く、近いほど低くなります")]
        public bool useDistanceScaling = false;

        [Tooltip("根元（最も近い位置）でのダメージ倍率（例: 0.4 = 40%のダメージ）")]
        public float nearDamageMultiplier = 0.4f;

        [Tooltip("先端（最も遠い位置）でのダメージ倍率（例: 1.8 = 180%のダメージ）")]
        public float farDamageMultiplier = 1.8f;

        [Tooltip("距離補正の最大有効距離。この距離以上で先端倍率が適用されます")]
        public float maxScalingDistance = 4.0f;

        // =========================================================
        // 時間経過ダメージ補正
        // =========================================================
        [Header("時間経過ダメージ補正")]
        [Tooltip("有効にすると、攻撃判定が出現してからの時間が経つほどダメージが変化します")]
        public bool useTimeScaling = false;

        [Tooltip("時間経過の最大時間（この時間で最終倍率になります）")]
        public float maxTimeScalingDuration = 0.6f;

        [Tooltip("出現直後のダメージ倍率")]
        public float startDamageMultiplier = 1.0f;

        [Tooltip("最大時間経過時のダメージ倍率（例: 0.2 = 20%まで低下）")]
        public float endDamageMultiplier = 0.3f;

        private float activeStartTime;

        [HideInInspector]
        public CharacterBase owner; // CharacterBase（攻撃側の本体）への参照

        // 攻撃が相手に当たったときに呼び出されるコールバック
        public System.Action<Hurtbox, int> OnHitLanded;

        // 1回の攻撃アクティブ期間中に、すでに攻撃が当たったキャラクターを記録するリスト
        private System.Collections.Generic.List<CharacterBase> hitCharacters = new System.Collections.Generic.List<CharacterBase>();

        private void OnEnable()
        {
            activeStartTime = Time.time;
            hitCharacters.Clear();
            CheckOverlap();
        }

        private void OnDisable()
        {
            hitCharacters.Clear();
        }

        // 攻撃判定が有効化した瞬間に重なっている敵を即座に検出する処理
        // （Unity物理エンジンのタイミングによってOnTriggerEnter2Dが発火しないバグを防ぎます）
        public void CheckOverlap()
        {
            // 飛び道具（弾）は生成位置で自身に即時ヒットすることを防ぐため、出現時の即時重なり判定は行いません
            if (isProjectile) return;

            Collider2D col = GetComponent<Collider2D>();
            if (col == null || !col.enabled) return;

            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = true;

            System.Collections.Generic.List<Collider2D> results = new System.Collections.Generic.List<Collider2D>();
            int count = col.Overlap(filter, results);

            for (int i = 0; i < count; i++)
            {
                OnTriggerEnter2D(results[i]);
            }
        }

        // OnTriggerEnter2D は、この「攻撃判定」が「他の誰かの判定」に重なった瞬間に
        // Unityが自動的に呼び出してくれる便利なメソッド（機能）です！
        private void OnTriggerEnter2D(Collider2D other)
        {
            // ぶつかった相手（other）から、やられ判定（Hurtbox）のスクリプトを探します
            Hurtbox hurtbox = other.GetComponent<Hurtbox>();
            
            if (owner == null) owner = GetComponentInParent<CharacterBase>();
            if (owner != null) ownerPlayerID = owner.playerID;

            // 持ち主が未設定かつownerPlayerIDが無効（0以下）な場合は判定しない
            if (owner == null && ownerPlayerID <= 0) return;

            // やられ判定の確認
            if (hurtbox != null && hurtbox.owner != null)
            {
                // 自分自身（持ち主）には絶対に当たらない
                if (owner != null && hurtbox.owner == owner) return;

                // 持ち主と同じプレイヤーID（味方や発射した本人）には当たらない
                if (owner != null && hurtbox.owner.playerID == owner.playerID) return;
                if (ownerPlayerID > 0 && hurtbox.owner.playerID == ownerPlayerID) return;

                if (!hitCharacters.Contains(hurtbox.owner))
                {
                    hitCharacters.Add(hurtbox.owner);

                    // 実際に与えるダメージを計算（距離ベース補正がある場合はそれを適用）
                    int finalDamage = CalculateDamage(hurtbox.transform.position);

                    // 相手にダメージを与えます！
                    hurtbox.TakeDamage(finalDamage, this);

                    // ヒットストップの適用 (ダメージ量に応じた停止フレーム数)
                    if (HUDManager.Instance != null)
                    {
                        int hitStopFrames = 3;
                        if (damage >= 40) hitStopFrames = 8;       // 重攻撃/必殺技
                        else if (damage >= 25) hitStopFrames = 5;  // 中攻撃/特殊攻撃
                        else if (damage < 15) hitStopFrames = 2;   // 軽攻撃
                        
                        HUDManager.Instance.TriggerHitStop(hitStopFrames);
                    }

                    // 通常攻撃かつ攻撃主が存在する場合、攻撃主のマナを少量（10f）回復する
                    if (isNormalAttack && owner != null)
                    {
                        owner.AddMana(10f);
                    }

                    // 攻撃が当たったことを通知します
                    OnHitLanded?.Invoke(hurtbox, finalDamage);

                    // 【改造のヒント】
                    // もし「攻撃が当たったときに火花を出したい！」「ドカンという音を鳴らしたい！」
                    // という場合は、ここにそのプログラムを追加します！
                }
            }
        }

        // =========================================================
        // 距離・時間に応じたダメージ計算
        // =========================================================

        /// <summary>
        /// 距離・時間ベースのダメージ補正を適用した最終ダメージを計算します。
        /// </summary>
        /// <param name="hitPosition">攻撃が当たった相手の位置</param>
        /// <returns>補正済みの最終ダメージ値</returns>
        public int CalculateDamage(Vector3 hitPosition)
        {
            float distanceMultiplier = 1.0f;
            float timeMultiplier = 1.0f;

            // 距離ベース補正
            if (useDistanceScaling && owner != null)
            {
                float distance = Mathf.Abs(hitPosition.x - owner.transform.position.x);
                float normalizedDistance = Mathf.Clamp01(distance / maxScalingDistance);
                distanceMultiplier = Mathf.Lerp(nearDamageMultiplier, farDamageMultiplier, normalizedDistance);
            }

            // 時間ベース補正
            if (useTimeScaling)
            {
                float timeActive = Time.time - activeStartTime;
                float normalizedTime = Mathf.Clamp01(timeActive / maxTimeScalingDuration);
                timeMultiplier = Mathf.Lerp(startDamageMultiplier, endDamageMultiplier, normalizedTime);
            }

            // 最終ダメージ（最低1ダメージ保証）
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * distanceMultiplier * timeMultiplier));

            return finalDamage;
        }

#if UNITY_EDITOR
        // OnDrawGizmos は、Unityのシーン（作成画面）で目印の図形を描くための機能です。
        // ゲームプレイ中には見えません。
        private void OnDrawGizmos()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                // 攻撃判定は「薄い赤色」で表示して、わかりやすくしています。
                Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
                Gizmos.DrawCube(col.bounds.center, col.bounds.size); // 塗りつぶし
                
                // 箱の枠線を少し濃い赤色で描きます。
                Gizmos.color = new Color(1f, 0f, 0f, 1f);
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size); // 枠線
            }
        }
#endif
    }
}
