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

        [Tooltip("これが飛び道具（遠距離攻撃）かどうか")]
        public bool isProjectile = false;

        // OnTriggerEnter2D は、この「攻撃判定」が「他の誰かの判定」に重なった瞬間に
        // Unityが自動的に呼び出してくれる便利なメソッド（機能）です！
        private void OnTriggerEnter2D(Collider2D other)
        {
            // ぶつかった相手（other）から、やられ判定（Hurtbox）のスクリプトを探します
            Hurtbox hurtbox = other.GetComponent<Hurtbox>();
            
            // もし相手がやられ判定（Hurtbox）を持っていて、さらに「自分自身」ではない場合...
            if (hurtbox != null && hurtbox.owner != null && hurtbox.owner.playerID != ownerPlayerID)
            {
                // 相手にダメージを与えます！
                hurtbox.TakeDamage(this);
                
                // 【改造のヒント】
                // もし「攻撃が当たったときに火花を出したい！」「ドカンという音を鳴らしたい！」
                // という場合は、ここにそのプログラムを追加します！
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
