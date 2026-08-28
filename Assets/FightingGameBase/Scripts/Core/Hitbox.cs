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

        [HideInInspector]
        public CharacterBase owner; // CharacterBase（攻撃側の本体）への参照

        // 攻撃が相手に当たったときに呼び出されるコールバック
        public System.Action<Hurtbox, int> OnHitLanded;

        // 1回の攻撃アクティブ期間中に、すでに攻撃が当たったキャラクターを記録するリスト
        private System.Collections.Generic.List<CharacterBase> hitCharacters = new System.Collections.Generic.List<CharacterBase>();

        private void OnEnable()
        {
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

            if (hurtbox != null && hurtbox.owner != null && hurtbox.owner != owner && hurtbox.owner.playerID != ownerPlayerID)
            {
                if (!hitCharacters.Contains(hurtbox.owner))
                {
                    hitCharacters.Add(hurtbox.owner);

                    // 相手にダメージを与えます！
                    hurtbox.TakeDamage(damage, this);

                    // 通常攻撃かつ攻撃主が存在する場合、攻撃主のマナを少量（10f）回復する
                    if (isNormalAttack && owner != null)
                    {
                        owner.AddMana(10f);
                    }

                    // 攻撃が当たったことを通知します
                    OnHitLanded?.Invoke(hurtbox, damage);

                    // 【改造のヒント】
                    // もし「攻撃が当たったときに火花を出したい！」「ドカンという音を鳴らしたい！」
                    // という場合は、ここにそのプログラムを追加します！
                }
            }
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
