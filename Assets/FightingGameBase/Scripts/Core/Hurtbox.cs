using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【Hurtbox（やられ判定・食らい判定）】
    // このスクリプトはキャラクターの「体（ダメージを受ける部分）」にくっつけます。
    // [RequireComponent]によって、このスクリプトを付けると自動的に「Collider2D」も付きます。
    // =================================================================================
    [RequireComponent(typeof(Collider2D))]
    public class Hurtbox : MonoBehaviour
    {
        [Tooltip("このやられ判定の持ち主（誰がダメージを受けるか）")]
        public CharacterBase owner; // CharacterBase（キャラクター本体）への参照

        void Start()
        {
            // もし「持ち主」が設定されていなかったら...
            if (owner == null)
            {
                // 自分自身の親オブジェクトから CharacterBase を探して自動でセットします！
                // これにより、手動でセットし忘れてもエラーになりにくくなります。
                owner = GetComponentInParent<CharacterBase>();
            }
        }

        // 相手の攻撃（Hitbox）が当たったときに、相手から呼び出されるメソッドです
        public void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (owner != null)
            {
                // 持ち主の TakeDamage（ダメージを受ける処理）を実行して、HPを減らします！
                owner.TakeDamage(damage, attackerHitbox);
            }
        }

#if UNITY_EDITOR
        // OnDrawGizmos は、Unityのシーン（作成画面）で目印の図形を描くための機能です。
        private void OnDrawGizmos()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                // やられ判定は「薄い黄緑色」で表示して、わかりやすくしています。
                Gizmos.color = new Color(0.5f, 1f, 0f, 0.4f);
                Gizmos.DrawCube(col.bounds.center, col.bounds.size); // 塗りつぶし
                
                // 箱の枠線を少し濃い黄緑色で描きます。
                Gizmos.color = new Color(0.5f, 1f, 0f, 1f);
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size); // 枠線
            }
        }
#endif
    }
}
