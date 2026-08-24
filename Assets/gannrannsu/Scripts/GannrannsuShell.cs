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
    public class GannrannsuShell : MonoBehaviour
    {
        [Tooltip("弾が自動的に消えるまでの時間（秒）")]
        public float lifetime = 3f;

        // --- プログラム内部で使う変数 ---
        private float moveDirection;  // 飛ぶ方向（1で右、-1で左）
        private float speed;          // 飛ぶスピード
        private int damage;           // この弾のダメージ
        private int ownerPlayerID;    // 誰が撃った弾か（自分に当たらないようにする）
        private System.Action onHitCallback; // 弾が当たったときのコールバック

        /// <summary>
        /// 弾を初期化するメソッド。GannrannsuCharacterから呼ばれます。
        /// </summary>
        public void Initialize(float direction, float shellSpeed, int shellDamage, int playerID, System.Action onHit = null)
        {
            moveDirection = direction;
            speed = shellSpeed;
            damage = shellDamage;
            ownerPlayerID = playerID;
            onHitCallback = onHit;

            // 物理エンジンの設定（弾は重力で落ちないようにする）
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f; // 重力をゼロにする
            rb.linearVelocity = new Vector2(moveDirection * speed, 0f); // 前方にまっすぐ飛ばす

            // コライダーをトリガーに設定する（すり抜けてダメージだけ与える）
            BoxCollider2D col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;

            // 弾の向きを飛ぶ方向に合わせる
            transform.localScale = new Vector3(moveDirection, 1f, 1f);

            // 一定時間後に自動で消す（画面外に飛んでいっても安心！）
            Hitbox hb = gameObject.AddComponent<Hitbox>();
            hb.damage = damage;
            hb.ownerPlayerID = ownerPlayerID;
            hb.isProjectile = true;
            hb.isNormalAttack = false;

            Destroy(gameObject, lifetime);
        }

        // 何かに触れた瞬間に呼ばれます
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 相手のやられ判定（Hurtbox）を探します
            Hurtbox hurtbox = other.GetComponent<Hurtbox>();

            // 相手がHurtboxを持っていて、自分以外のキャラクターなら…
            if (hurtbox != null && hurtbox.owner != null && hurtbox.owner.playerID != ownerPlayerID)
            {
                // ダメージを与えます！
                hurtbox.TakeDamage(damage, GetComponent<Hitbox>());
                Debug.Log($"砲撃弾が命中！{damage}ダメージ！");

                // コールバックを呼び出します
                onHitCallback?.Invoke();

                // 弾は当たったら消えます
                Destroy(gameObject);
            }
        }

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
