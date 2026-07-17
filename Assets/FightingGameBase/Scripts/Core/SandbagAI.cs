using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // A training dummy script that behaves as a sandbag (immortal) and automatically attacks the player.
    [RequireComponent(typeof(CharacterBase))]
    public class SandbagAI : MonoBehaviour
    {
        [Header("Sandbag Settings")]
        [Tooltip("Force HP to remain at max.")]
        public bool isImmortal = true;

        [Header("Auto Attack Settings")]
        [Tooltip("If true, the dummy will automatically attack at intervals.")]
        public bool autoAttack = true;
        [Tooltip("Interval between attacks in seconds.")]
        public float attackInterval = 2.5f;

        private CharacterBase dummyCharacter;
        private float nextAttackTime;
        
        private int lastHP;
        private float lastDamageTime;
        private float regenDelay = 3.0f; // 3 seconds delay before healing
        private float regenSpeed = 300f; // 300 HP per second recovery speed

        void Start()
        {
            dummyCharacter = GetComponent<CharacterBase>();
            
            // Configure dummy stats for training purposes
            if (dummyCharacter != null)
            {
                dummyCharacter.playerID = 2; // Treat dummy as Player 2
                dummyCharacter.maxHP = 999;
                dummyCharacter.currentHP = 999;
                lastHP = dummyCharacter.currentHP;
                lastDamageTime = -regenDelay;

                // Sync all child Hitboxes to use owner's playerID to ensure hits register!
                Hitbox[] hitboxes = GetComponentsInChildren<Hitbox>(true);
                foreach (Hitbox h in hitboxes)
                {
                    h.owner = dummyCharacter;
                    h.ownerPlayerID = dummyCharacter.playerID;
                }
            }

            // Start battle automatically in GameManager if present
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                GameManager.Instance.StartBattle();
            }

            nextAttackTime = Time.time + attackInterval;
        }

        void Update()
        {
            if (dummyCharacter == null || dummyCharacter.isDead) return;

            // Monitor HP changes
            if (dummyCharacter.currentHP < lastHP)
            {
                lastDamageTime = Time.time;
                lastHP = dummyCharacter.currentHP;
            }

            // Immortal (Sandbag) logic: regenerate HP back to max after inactivity
            if (isImmortal)
            {
                if (dummyCharacter.currentHP < dummyCharacter.maxHP && Time.time - lastDamageTime >= regenDelay)
                {
                    float targetHP = dummyCharacter.currentHP + regenSpeed * Time.deltaTime;
                    dummyCharacter.currentHP = Mathf.Min(dummyCharacter.maxHP, Mathf.RoundToInt(targetHP));
                    lastHP = dummyCharacter.currentHP;
                }
            }
            else
            {
                lastHP = dummyCharacter.currentHP;
            }

            // Auto Attack (Parry Trainer) logic
            if (autoAttack && Time.time >= nextAttackTime)
            {
                if (!dummyCharacter.isStunned && !dummyCharacter.isBlockingState)
                {
                    Debug.Log("[SandbagAI] Dummy triggers auto-attack normal.");
                    dummyCharacter.AttackNormal();
                }
                nextAttackTime = Time.time + attackInterval;
            }
        }
    }
}
