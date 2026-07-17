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

        public void AttackUltimate()
        {
            if (isDead || isStunned) return;
            if (animator != null) animator.SetTrigger("AttackUltimate");
            Debug.Log("Ultimate attack triggered.");
        }

        public virtual void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isDead || isInvincible) return;

            lastHurtTime = Time.time;

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
    }
}
