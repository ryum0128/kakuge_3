using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【CharacterBase（キャラクターの基本システム）】
    // このスクリプトはキャラクターの「体力」「移動」「ジャンプ」「攻撃」といった
    // 根本的な仕組みをまとめたものです。
    // =================================================================================
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterBase : MonoBehaviour
    {
        [Header("キャラクター設定")]
        [Tooltip("ステータス（体力やスピード）の設定データファイル")]
        public CharacterStats stats; // CharacterStatsスクリプタブルオブジェクトをセットします
        
        [Tooltip("プレイヤー番号（1なら1P、2なら2P）")]
        public int playerID = 1; 

        [Header("現在の状態（ゲーム中に変化します）")]
        public int currentHP;        // 今の体力
        public bool isGrounded;      // 地面に足がついているか（trueならジャンプ可能）
        public bool isDead;          // 倒れているか

        // --- プログラム内で使う部品（コンポーネント） ---
        private Rigidbody2D rb;      // 物理エンジン（重力や移動を計算する機能）
        private Animator animator;   // アニメーションを再生する機能

        protected virtual void Start()
        {
            // 自分自身についている Rigidbody2D を取得します
            rb = GetComponent<Rigidbody2D>();
            
            // 子オブジェクト（Visualsなど）についている Animator を取得します
            animator = GetComponentInChildren<Animator>(); 
            
            // もしステータス（CharacterStats）がセットされていれば、最初の体力を最大HPにします
            if (stats != null)
            {
                currentHP = stats.maxHP;
            }
        }

        void Update()
        {
            // すでに倒れているか、またはゲームが始まっていない場合は何もせず終了します
            if (isDead || GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            // --- 接地判定（地面にいるかどうか） ---
            // Y軸の速度（上下の動き）が 0.1 より小さければ、地面にいるとみなします。
            // （Unityの物理演算のブレを考慮して少し余裕を持たせています）
            isGrounded = Mathf.Abs(rb.linearVelocity.y) < 0.1f;
            
            // アニメーターがあれば、地面にいるかどうかを伝えます（落下アニメーションなどのため）
            if (animator != null)
            {
                animator.SetBool("IsGrounded", isGrounded);
            }
        }

        // =========================================================
        // アクション（移動・ジャンプ・攻撃など）
        // ※ここは PlayerInputController（入力）から呼ばれます
        // =========================================================

        public void Move(float direction)
        {
            if (isDead) return;

            // direction は -1(左) から 1(右) の値になります。
            // statsがセットされていなければ、仮のスピード「5」を使います。
            float speed = stats != null ? stats.moveSpeed : 5f;
            
            // 物理エンジンを使って、キャラクターを左右に動かします（Y軸の落下速度はそのまま）
            rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);

            // キャラクターの向き（画像）を反転する処理
            if (direction != 0)
            {
                // 右(1)か左(-1)に合わせて、スケール（大きさ）のXのプラスマイナスを切り替えます
                transform.localScale = new Vector3(Mathf.Sign(direction), 1, 1);
            }

            // アニメーターに「今どれくらいの速さで動いているか」を伝えます（歩きアニメーションのため）
            if (animator != null)
            {
                animator.SetFloat("Speed", Mathf.Abs(direction));
            }
        }

        public void Jump()
        {
            // 倒れているか、または空中にいるときはジャンプできません
            if (isDead || !isGrounded) return;

            // statsがセットされていなければ、仮のジャンプ力「12」を使います。
            float force = stats != null ? stats.jumpForce : 12f;
            
            // 上方向（Y軸）に力を加えてジャンプさせます
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
            
            // ジャンプのアニメーションを再生するように伝えます
            if (animator != null)
            {
                animator.SetTrigger("Jump");
            }
        }

        // --- 攻撃処理 ---
        
        public void AttackNormal()
        {
            if (isDead) return;
            
            // 通常攻撃のアニメーションを再生
            if (animator != null) animator.SetTrigger("AttackNormal");
            Debug.Log("通常攻撃発動！");

            // 子オブジェクトからHitbox（攻撃判定）を探して、一時的にオン（有効）にします！
            Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
            if (hitbox != null)
            {
                // コルーチンという機能を使って、0.2秒間だけ判定を出します
                StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, 0.2f));
            }
        }

        // 時間差で処理を行うための仕組み（コルーチン）です
        private System.Collections.IEnumerator ActivateHitboxTemporarily(GameObject hitboxObj, float duration)
        {
            hitboxObj.SetActive(true); // 攻撃判定を出す（赤い箱が現れる）
            yield return new WaitForSeconds(duration); // 指定した時間（今回は0.2秒）だけ待つ
            hitboxObj.SetActive(false); // 攻撃判定を消す
        }

        public void AttackSpecial()
        {
            if (isDead) return;
            
            // 特殊攻撃のアニメーションを再生
            if (animator != null) animator.SetTrigger("AttackSpecial");
            Debug.Log("特殊攻撃発動！");
        }

        public void AttackUltimate()
        {
            if (isDead) return;
            
            // 必殺技のアニメーションを再生
            if (animator != null) animator.SetTrigger("AttackUltimate");
            Debug.Log("必殺攻撃（同時押し）発動！！");
        }

        // =========================================================
        // ダメージとゲームオーバーの処理
        // =========================================================

        public void TakeDamage(int damage)
        {
            if (isDead) return;

            // ダメージの分だけ体力を減らします
            currentHP -= damage;
            if (currentHP < 0) currentHP = 0; // 体力がマイナスにならないようにする

            // ダメージを受けたアニメーションを再生します
            if (animator != null)
            {
                animator.SetTrigger("Damage");
            }

            // 体力がゼロになったら倒れる処理（Die）へ進みます
            if (currentHP == 0)
            {
                Die();
            }
        }

        private void Die()
        {
            isDead = true; // 倒れたフラグをオンにする
            
            // 倒れるアニメーションを再生します
            if (animator != null)
            {
                animator.SetBool("IsDead", true);
            }
            
            // GameManager（試合を管理するシステム）に、自分が倒れたことを通知します
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCharacterDied(playerID);
            }
        }
    }
}
