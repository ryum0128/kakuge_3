using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // Base class for all characters in the game.
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterBase : MonoBehaviour
    {
        [Header("Character Configurations")]
        public CharacterStats stats;
        public int playerID = 1;

        [Header("Character Stats")]
        public int maxHP = 100;
        public int currentHP;
        public bool isGrounded;
        public bool isDead;
        public bool isStunned = false;

        [Header("Dash & Evade Configurations")]
        public bool isInvincible = false;
        public bool isDashingOrEvading = false;
        
        // 攻撃動作中か判定するための仮想プロパティ（派生先でオーバーライドされます）
        public virtual bool IsAttacking => false;

        [HideInInspector]
        public float lastHurtTime = -10f;

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

        // 被ダメージ後、一定時間（0.25秒）通常攻撃・特殊攻撃を出せないようにするロック判定
        public bool IsHurtLocked => Time.time - lastHurtTime < 0.25f;

        [Header("DeepWoken Posture & Mana Settings")]
        public float maxPosture = 100f;
        public float currentPosture = 0f;
        public float postureRegenSpeed = 20f;
        public float maxMana = 100f;
        public float currentMana = 50f; // Starts with 50 Mana
        public float manaRegenSpeed = 8f; // Mana recovery speed per second
        public bool isBlockingState = false;

        protected Rigidbody2D rb;
        protected Animator animator;

        protected virtual void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.mass = 1000f; // Prevent characters from easily pushing each other like lightweight blocks
            animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController == null)
            {
                animator = null;
            }

            if (stats != null)
            {
                maxHP = stats.maxHP * 2;
            }
            else
            {
                maxHP = maxHP * 2;
            }
            currentHP = maxHP;

            // Auto-instantiate HUDManager if it's missing in the scene
            if (FindAnyObjectByType<HUDManager>() == null)
            {
                GameObject hudGo = new GameObject("HUDManager");
                hudGo.AddComponent<HUDManager>();
            }

            // Sync all child Hitboxes to this character's playerID to guarantee no owner ID mismatch issues
            Hitbox[] hitboxes = GetComponentsInChildren<Hitbox>(true);
            foreach (Hitbox h in hitboxes)
            {
                h.owner = this;
                h.ownerPlayerID = playerID;
            }
        }

        protected virtual void Update()
        {
            if (isDead || (GameManager.Instance != null && !GameManager.Instance.IsPlaying)) return;

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

            // Ground check (based on vertical velocity threshold)
            isGrounded = Mathf.Abs(rb.linearVelocity.y) < 0.1f;

            if (animator != null)
            {
                animator.SetBool("IsGrounded", isGrounded);
            }

            // (Natural posture recovery when not blocking has been disabled as requested)

            // (Natural mana recovery when idle has been disabled as requested)
        }

        // 攻撃ヒット時やイベント時にマナを増加させるメソッド
        public void AddMana(float amount)
        {
            if (isDead) return;
            currentMana += amount;
            if (currentMana > maxMana) currentMana = maxMana;
        }

        public virtual void Move(float direction)
        {
            if (isDead || isStunned) return;

            float speed = stats != null ? stats.moveSpeed : 5f;
            rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);

            // Flip sprite depending on direction
            if (direction != 0)
            {
                transform.localScale = new Vector3(Mathf.Sign(direction), 1, 1);
            }

            if (animator != null)
            {
                animator.SetFloat("Speed", Mathf.Abs(direction));
            }
        }

        public void Jump()
        {
            if (isDead || !isGrounded || isStunned) return;

            float force = stats != null ? stats.jumpForce : 12f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);

            if (animator != null)
            {
                animator.SetTrigger("Jump");
            }
        }

        public virtual void AttackNormal()
        {
            if (isDead || isStunned) return;

            if (animator != null) animator.SetTrigger("AttackNormal");
            Debug.Log("Normal attack triggered.");

            Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
            if (hitbox != null)
            {
                StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, 0.2f));
            }
        }

        protected IEnumerator ActivateHitboxTemporarily(GameObject hitboxObj, float duration)
        {
            hitboxObj.SetActive(true);
            yield return new WaitForSeconds(duration);
            hitboxObj.SetActive(false);
        }

        public virtual void AttackSpecial()
        {
            if (isDead || isStunned) return;
            if (animator != null) animator.SetTrigger("AttackSpecial");
            Debug.Log("Special attack triggered.");
        }

        public virtual void AttackUltimate()
        {
            if (isDead || isStunned) return;
            if (animator != null) animator.SetTrigger("AttackUltimate");
            Debug.Log("Ultimate attack triggered.");
        }

        public virtual void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isDead || isInvincible) return;

            lastHurtTime = Time.time;

            // スタン中に攻撃を受けたら、スタンを解除する
            if (isStunned)
            {
                RemoveStun();
            }

            currentHP -= damage;
            if (currentHP < 0) currentHP = 0;

            if (animator != null)
            {
                animator.SetTrigger("Damage");
            }

            // Register hit in HUD for damage popups, combos, and HP bars
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.RegisterHit(playerID, damage, transform.position);
            }

            if (currentHP == 0)
            {
                Die();
            }
        }

        private void Die()
        {
            isDead = true;

            if (animator != null)
            {
                animator.SetBool("IsDead", true);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCharacterDied(playerID);
            }
        }

        public virtual void StartBlock() { }
        public virtual void StopBlock() { }

        public virtual void Stun(float duration)
        {
            if (isDead) return;
            StartCoroutine(StunRoutine(duration));
        }

        private IEnumerator StunRoutine(float duration)
        {
            isStunned = true;
            if (animator != null) animator.SetTrigger("Damage");
            yield return new WaitForSeconds(duration);
            isStunned = false;
        }

        protected virtual void LateUpdate()
        {
            PreventCharacterOverlap();
        }

        private void PreventCharacterOverlap()
        {
            if (isDead || rb == null || isDashingOrEvading) return;

            // Find opponent
            CharacterBase opponent = null;
            CharacterBase[] allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (CharacterBase c in allChars)
            {
                if (c != this && !c.isDead)
                {
                    opponent = c;
                    break;
                }
            }

            if (opponent == null) return;

            // Get distance
            float dx = opponent.transform.position.x - transform.position.x;
            float absDx = Mathf.Abs(dx);
            float dy = opponent.transform.position.y - transform.position.y;
            float absDy = Mathf.Abs(dy);

            // Assume standard capsule collision width sum is ~0.9f.
            // Only block horizontal movement if we are close vertically (not jumping over each other).
            float minHorizontalDistance = 0.9f;
            float minVerticalDistance = 1.6f;

            if (absDx < minHorizontalDistance && absDy < minVerticalDistance)
            {
                Vector2 vel = rb.linearVelocity;

                // Moving right towards opponent on the right -> stop
                if (dx > 0 && vel.x > 0)
                {
                    vel.x = 0f;
                    rb.linearVelocity = vel;
                }
                // Moving left towards opponent on the left -> stop
                else if (dx < 0 && vel.x < 0)
                {
                    vel.x = 0f;
                    rb.linearVelocity = vel;
                }
            }
        }

        public void TriggerDashOrEvade(float inputDir)
        {
            if (isDead || isStunned || isDashingOrEvading || IsAttacking) return;
            StartCoroutine(DashOrEvadeRoutine(inputDir));
        }

        private IEnumerator DashOrEvadeRoutine(float inputDir)
        {
            isDashingOrEvading = true;
            isInvincible = true;

            // Opponent search
            CharacterBase opponent = null;
            CharacterBase[] allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (CharacterBase c in allChars)
            {
                if (c != this && !c.isDead)
                {
                    opponent = c;
                    break;
                }
            }

            Collider2D[] myCols = GetComponentsInChildren<Collider2D>();
            Collider2D[] oppCols = opponent != null ? opponent.GetComponentsInChildren<Collider2D>() : null;

            // Ignore collision with opponent
            if (myCols != null && oppCols != null)
            {
                foreach (var cA in myCols)
                {
                    foreach (var cB in oppCols)
                    {
                        Physics2D.IgnoreCollision(cA, cB, true);
                    }
                }
            }

            // Find visuals transform (fallback to this transform if not found)
            Transform visuals = transform.Find("Visuals");
            if (visuals == null) visuals = transform;

            SpriteRenderer sr = visuals.GetComponentInChildren<SpriteRenderer>();
            Color originalColor = sr != null ? sr.color : Color.white;
            Vector3 originalScale = visuals.localScale;

            float faceDir = transform.localScale.x; // 1 = right, -1 = left
            bool isDash = inputDir != 0f;
            float dashDir = isDash ? Mathf.Sign(inputDir) : -faceDir; // Dash in input direction, Evade backward
            float duration = isDash ? 0.18f : 0.15f; // ダッシュ距離を縮小 (duration: 0.22 -> 0.18, evade: 0.18 -> 0.15)
            float speed = isDash ? 13f : 8f;         // ダッシュ速度を抑える (speed: 18 -> 13, evade: 10 -> 8)

            // Apply visual cues
            if (sr != null)
            {
                sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.45f); // Transparent ghosting effect
            }

            if (isDash)
            {
                // Dash squash-stretch scaling
                visuals.localScale = new Vector3(originalScale.x * 1.3f, originalScale.y * 0.8f, originalScale.z);
            }
            else
            {
                // Evade tilt angle rotation
                visuals.localRotation = Quaternion.Euler(0, 0, -dashDir * 15f);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (isDead) break;
                elapsed += Time.deltaTime;
                rb.linearVelocity = new Vector2(dashDir * speed, rb.linearVelocity.y);
                yield return null;
            }

            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            // Restore collisions
            if (myCols != null && oppCols != null)
            {
                foreach (var cA in myCols)
                {
                    foreach (var cB in oppCols)
                    {
                        Physics2D.IgnoreCollision(cA, cB, false);
                    }
                }
            }

            // Restore visuals
            if (sr != null)
            {
                sr.color = originalColor;
            }
            visuals.localScale = originalScale;
            visuals.localRotation = Quaternion.identity;

            isInvincible = false;
            isDashingOrEvading = false;
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
