using UnityEngine;
using System.Collections;

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
        public bool isStunned;       // スタン（行動不能）状態かどうか

        // --- スタンスキル用の内部変数 ---
        private bool hasUsedStun = false;       // スタンスキルを使ったかどうか（1試合に1回）
        private Coroutine stunCoroutine = null;  // スタン解除用のコルーチン

        [Header("スタンスキル設定")]
        [Tooltip("チャージゲージが満タンになるまでの時間（秒）")]
        public float stunChargeTime = 10f;      // ゲージが0から100%になるまでの時間

        [Tooltip("現在のチャージゲージ量（0～1、0=空、1=満タン）")]
        public float stunChargeGauge = 0f;      // 現在のチャージ量（0.0～1.0）

        /// <summary>
        /// スタンゲージが満タンかどうかを確認できるプロパティ
        /// </summary>
        public bool IsStunReady => stunChargeGauge >= 1f && !hasUsedStun;

        /// <summary>
        /// スタンスキルが使用済みかどうかを確認できるプロパティ
        /// </summary>
        public bool HasUsedStun => hasUsedStun;

        // --- プログラム内で使う部品（コンポーネント） ---
        private Rigidbody2D rb;      // 物理エンジン（重力や移動を計算する機能）
        private Animator animator;   // アニメーションを再生する機能

        void Start()
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

            // スタンチャージゲージを溜める（まだ使用済みでなく、満タンでない場合）
            if (!hasUsedStun && stunChargeGauge < 1f)
            {
                stunChargeGauge += Time.deltaTime / stunChargeTime;
                stunChargeGauge = Mathf.Clamp01(stunChargeGauge); // 0～1の範囲に収める
            }

            // スタン中は移動を停止する（入力を無視してキャラクターを止める）
            if (isStunned)
            {
                Rigidbody2D rbRef = GetComponent<Rigidbody2D>();
                if (rbRef != null)
                {
                    rbRef.linearVelocity = new Vector2(0, rbRef.linearVelocity.y);
                }
                if (animator != null)
                {
                    animator.SetFloat("Speed", 0f);
                }
            }

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
            if (isDead || isStunned) return; // スタン中は移動できない

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
            // 倒れているか、スタン中か、または空中にいるときはジャンプできません
            if (isDead || isStunned || !isGrounded) return;

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
        
        public virtual void AttackNormal()
        {
            if (isDead || isStunned) return; // スタン中は攻撃できない
            
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

        public virtual void AttackSpecial()
        {
            if (isDead || isStunned) return; // スタン中は攻撃できない
            
            // 特殊攻撃のアニメーションを再生
            if (animator != null) animator.SetTrigger("AttackSpecial");
            Debug.Log("特殊攻撃発動！");
        }

        public virtual void AttackUltimate()
        {
            if (isDead || isStunned) return; // スタン中は攻撃できない
            
            // 必殺技のアニメーションを再生
            if (animator != null) animator.SetTrigger("AttackUltimate");
            Debug.Log("必殺攻撃（同時押し）発動！！");
        }

        // =========================================================
        // ダメージとゲームオーバーの処理
        // =========================================================

        public virtual void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isDead) return;

            // スタン中に攻撃を受けたら、スタンを解除する
            if (isStunned)
            {
                RemoveStun();
            }

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

        // =========================================================
        // スタン（行動不能）スキル
        // =========================================================

        /// <summary>
        /// スタンスキルを発動します。
        /// 相手キャラクターを探して2秒間行動不能にします。（1試合に1回のみ）
        /// </summary>
        public virtual void AttackStun()
        {
            if (isDead || isStunned) return;

            // チャージゲージが満タンでない場合は使えない
            if (stunChargeGauge < 1f)
            {
                Debug.Log($"スタンゲージが足りません！（{stunChargeGauge * 100f:F0}%）");
                return;
            }

            // すでに使用済みなら発動しない（1回限り）
            if (hasUsedStun)
            {
                Debug.Log("スタンスキルはもう使えません！（1回限り）");
                return;
            }

            hasUsedStun = true; // 使用済みにする
            stunChargeGauge = 0f; // ゲージをリセット

            // スタンスキルのアニメーション（あれば再生）
            if (animator != null) animator.SetTrigger("AttackStun");
            Debug.Log("スタンスキル発動！ 敵を2秒間行動不能にする！");

            // 相手キャラクターを探してスタンを付与する
            CharacterBase[] allCharacters = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (CharacterBase target in allCharacters)
            {
                // 自分以外のキャラクター（＝敵）にスタンを付与
                if (target.playerID != this.playerID && !target.isDead)
                {
                    target.ApplyStun(2.0f); // 2秒間スタン
                }
            }
        }

        /// <summary>
        /// このキャラクターをスタン（行動不能）状態にします。
        /// 実行中のアクション（コルーチン）をすべて強制中断します。
        /// </summary>
        public void ApplyStun(float duration)
        {
            if (isDead || isStunned) return;

            // 実行中のすべてのコルーチン（攻撃アニメーションなど）を強制停止する
            StopAllCoroutines();

            // アニメーターの状態をリセットする（攻撃モーション等を中断）
            if (animator != null)
            {
                animator.ResetTrigger("AttackNormal");
                animator.ResetTrigger("AttackSpecial");
                animator.ResetTrigger("AttackUltimate");
                animator.SetFloat("Speed", 0f);
                animator.SetTrigger("Stunned"); // スタン用のアニメーション（あれば再生）
            }

            // 移動を止める
            Rigidbody2D rbRef = GetComponent<Rigidbody2D>();
            if (rbRef != null)
            {
                rbRef.linearVelocity = new Vector2(0, rbRef.linearVelocity.y);
            }

            isStunned = true;
            Debug.Log($"プレイヤー{playerID} がスタン状態になった！（{duration}秒間）");

            // 指定時間後に自動でスタン解除するコルーチンを開始
            stunCoroutine = StartCoroutine(StunTimer(duration));
        }

        /// <summary>
        /// スタン状態を解除します。
        /// </summary>
        public void RemoveStun()
        {
            if (!isStunned) return;

            // スタン解除タイマーが動いていたら止める
            if (stunCoroutine != null)
            {
                StopCoroutine(stunCoroutine);
                stunCoroutine = null;
            }

            isStunned = false;
            Debug.Log($"プレイヤー{playerID} のスタンが解除された！");

            // スタン解除のアニメーション処理
            if (animator != null)
            {
                animator.ResetTrigger("Stunned");
            }
        }

        /// <summary>
        /// スタンの持続時間を管理するコルーチン（時間経過で自動解除）
        /// </summary>
        private IEnumerator StunTimer(float duration)
        {
            yield return new WaitForSeconds(duration);
            RemoveStun(); // 時間が来たらスタン解除
        }
    }
}
