using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【GannrannsuShell（ガンランスの砲撃弾）】
    // ガンランスの特殊攻撃で飛ばす弾のスクリプトです。
    // 前方に一定速度で飛んでいき、敵に当たるとダメージを与えて消えます。
    // 一定時間経つと自動で消えます（画面外に飛んでいっても大丈夫なように）。
    // =================================================================================
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(Hitbox))]
    public class GannrannsuShell : MonoBehaviour
    {
        [Tooltip("弾が自動的に消えるまでの時間（秒）")]
        public float lifetime = 3f;

        // --- プログラム内部で使う変数 ---
        private float moveDirection;  // 飛ぶ方向（1で右、-1で左）
        private float speed;          // 飛ぶスピード
        private int damage;           // この弾のダメージ
        private int ownerPlayerID;    // 誰が撃った弾か（自分に当たらないようにする）
        private CharacterBase owner;  // 撃ったキャラクター本体
        private System.Action onHitCallback; // 弾が当たったときのコールバック

        void Awake()
        {
            // 生成直後の誤判定を防ぐため、事前に飛び道具フラグを設定
            Hitbox hitbox = GetComponent<Hitbox>();
            if (hitbox != null)
            {
                hitbox.isProjectile = true;
                hitbox.ownerPlayerID = 0;
            }
        }

        /// <summary>
        /// 弾を初期化するメソッド。GannrannsuCharacterから呼ばれます。
        /// </summary>
        public void Initialize(float direction, float shellSpeed, int shellDamage, int playerID, CharacterBase ownerCharacter = null, System.Action onHit = null)
        {
            moveDirection = direction;
            speed = shellSpeed;
            damage = shellDamage;
            ownerPlayerID = playerID;
            owner = ownerCharacter;
            onHitCallback = onHit;

            // 物理エンジンの設定（弾は重力で落ちないようにする）
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f; // 重力をゼロにする
            rb.linearVelocity = new Vector2(moveDirection * speed, 0f); // 前方にまっすぐ飛ばす

            // コライダーをトリガーに設定する（すり抜けてダメージだけ与える）
            BoxCollider2D col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;

            // 発射主（ガンランス）の全コライダーとの接触を物理レベルで無効化
            if (owner != null)
            {
                Collider2D[] ownerCols = owner.GetComponentsInChildren<Collider2D>(true);
                foreach (var oc in ownerCols)
                {
                    if (oc != null && oc != col)
                    {
                        Physics2D.IgnoreCollision(col, oc, true);
                    }
                }
            }

            // Hitboxコンポーネントのセットアップ
            Hitbox hitbox = GetComponent<Hitbox>();
            hitbox.damage = damage;
            hitbox.owner = owner;
            hitbox.ownerPlayerID = ownerPlayerID;
            hitbox.isNormalAttack = false;
            hitbox.isProjectile = true;

            // 命中時のコールバックを登録して弾を消滅させる
            hitbox.OnHitLanded = (hurtbox, dmg) =>
            {
                Debug.Log($"砲撃弾が命中！{dmg}ダメージ！");
                onHitCallback?.Invoke();
                Destroy(gameObject);
            };

            // 弾の向きを飛ぶ方向に合わせる
            transform.localScale = new Vector3(moveDirection, 1f, 1f);

            // 一定時間後に自動で消す（画面外に飛んでいっても安心！）
            Destroy(gameObject, lifetime);
        }

        // 衝突判定はHitboxコンポーネントで一元管理されるため、OnTriggerEnter2Dは不要になります。

#if UNITY_EDITOR
        // シーン画面で弾を見やすくするための表示（オレンジ色の丸）
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f); // オレンジ色
            Gizmos.DrawSphere(transform.position, 0.15f);
        }
#endif
    }
}
