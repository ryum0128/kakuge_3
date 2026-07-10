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
            animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController == null)
            {
                animator = null;
            }

            if (stats != null)
            {
                maxHP = stats.maxHP;
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

            // Natural posture recovery when not blocking
            if (!isBlockingState && currentPosture > 0f)
            {
                currentPosture -= postureRegenSpeed * Time.deltaTime;
                if (currentPosture < 0f) currentPosture = 0f;
            }

            // Natural mana recovery
            if (currentMana < maxMana)
            {
                currentMana += manaRegenSpeed * Time.deltaTime;
                if (currentMana > maxMana) currentMana = maxMana;
            }
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

        public virtual void TakeDamage(int damage)
        {
            if (isDead) return;

            currentHP -= damage;
            if (currentHP < 0) currentHP = 0;

            if (animator != null)
            {
                animator.SetTrigger("Damage");
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
    }
}
