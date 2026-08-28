using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // High-level Versus CPU AI for 2D Fighting Game.
    // Features intelligent spacing, attack combinations, special attacks, and reactive parrying/blocking.
    [RequireComponent(typeof(CharacterBase))]
    public class VersusCPUAI : MonoBehaviour
    {
        public enum AIDifficulty
        {
            Easy,       // Slower reaction, low parry chance, casual spacing
            Normal,     // Balanced reaction, moderate parries, active spacing
            Hard,       // Fast reaction, high parry chance, aggressive combos
            Nightmare   // Frame-perfect reaction, master parries, deadly special attacks
        }

        [Header("AI Configuration")]
        public AIDifficulty difficulty = AIDifficulty.Normal;
        public CharacterBase targetPlayer;
        public bool autoFindTarget = true;

        [Header("Combat Spacing")]
        public float minIdealDistance = 1.4f;
        public float maxIdealDistance = 2.4f;

        private CharacterBase myCharacter;
        private float nextAttackTime = 0f;
        private float parryReactionRate = 0.6f;
        private float attackCooldown = 1.1f;
        private bool isReactingToAttack = false;

        private static Sprite squareSprite;

        void Start()
        {
            myCharacter = GetComponent<CharacterBase>();

            if (myCharacter != null)
            {
                myCharacter.playerID = 2; // Default to Player 2

                // Sync all child Hitboxes to owner to guarantee hits register
                Hitbox[] hitboxes = GetComponentsInChildren<Hitbox>(true);
                foreach (Hitbox h in hitboxes)
                {
                    h.owner = myCharacter;
                    h.ownerPlayerID = myCharacter.playerID;
                }
            }

            EnsureWeaponVisual();
            ApplyDifficultySettings();
            FindTarget();
        }

        private void EnsureWeaponVisual()
        {
            Transform visuals = transform.Find("Visuals");
            if (visuals == null)
            {
                GameObject visObj = new GameObject("Visuals");
                visObj.transform.SetParent(transform, false);
                visuals = visObj.transform;
            }

            Transform weapon = visuals.Find("Weapon");
            if (weapon == null)
            {
                GameObject weaponObj = new GameObject("Weapon");
                weaponObj.transform.SetParent(visuals, false);
                weaponObj.transform.localPosition = new Vector3(0.8f, 0.5f, 0f);
                weaponObj.transform.localScale = Vector3.one;
                weapon = weaponObj.transform;
            }

            SpriteRenderer weaponSr = weapon.GetComponent<SpriteRenderer>();
            if (weaponSr == null)
            {
                weaponSr = weapon.gameObject.AddComponent<SpriteRenderer>();
            }

            if (weaponSr.sprite == null)
            {
                weaponSr.sprite = GetSquareSprite();
            }

            weaponSr.drawMode = SpriteDrawMode.Sliced;
            weaponSr.size = new Vector2(1.2f, 0.8f);
            weaponSr.color = new Color(0.85f, 0.88f, 0.95f, 1f); // Metallic silver blade skin
            weaponSr.sortingOrder = 5;
        }

        private static Sprite GetSquareSprite()
        {
            if (squareSprite != null) return squareSprite;
            Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Color[] colors = new Color[32 * 32];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            squareSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            return squareSprite;
        }

        public void ApplyDifficultySettings()
        {
            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    parryReactionRate = 0.3f;
                    attackCooldown = 1.8f;
                    break;
                case AIDifficulty.Normal:
                    parryReactionRate = 0.6f;
                    attackCooldown = 1.1f;
                    break;
                case AIDifficulty.Hard:
                    parryReactionRate = 0.85f;
                    attackCooldown = 0.6f;
                    break;
                case AIDifficulty.Nightmare:
                    parryReactionRate = 0.98f;
                    attackCooldown = 0.35f;
                    break;
            }
        }

        private void FindTarget()
        {
            if (!autoFindTarget && targetPlayer != null) return;

            CharacterBase[] allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (CharacterBase c in allChars)
            {
                if (c != myCharacter && !c.isDead)
                {
                    targetPlayer = c;
                    break;
                }
            }
        }

        void Update()
        {
            if (myCharacter == null || myCharacter.isDead) return;

            if (targetPlayer == null || targetPlayer.isDead)
            {
                FindTarget();
                if (targetPlayer == null) return;
            }

            float dx = targetPlayer.transform.position.x - transform.position.x;
            float absDist = Mathf.Abs(dx);
            float moveDir = Mathf.Sign(dx);

            // 1. Defense / Reactive Parry Logic
            HandleParryReaction(absDist);

            // 2. Movement & Spacing Logic
            HandleMovement(absDist, moveDir);

            // 3. Attack Execution Logic
            HandleAttacking(absDist);
        }

        private void HandleParryReaction(float distance)
        {
            if (targetPlayer == null) return;

            // Detect if target player has just started an attack in close range
            if (targetPlayer.IsAttacking && distance <= maxIdealDistance + 0.8f)
            {
                if (!isReactingToAttack && !myCharacter.isBlockingState && !myCharacter.isStunned)
                {
                    isReactingToAttack = true;
                    if (Random.value <= parryReactionRate)
                    {
                        Debug.Log("[VersusCPUAI] Defensive reaction: Triggering Parry/Block!");
                        myCharacter.StartBlock();
                        StartCoroutine(ReleaseBlockRoutine(0.35f));
                    }
                }
            }
            else
            {
                isReactingToAttack = false;
            }
        }

        private IEnumerator ReleaseBlockRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (myCharacter != null && myCharacter.isBlockingState)
            {
                myCharacter.StopBlock();
            }
        }

        private void HandleMovement(float distance, float moveDir)
        {
            if (myCharacter.isStunned || myCharacter.isBlockingState || myCharacter.IsAttacking) return;

            if (distance > maxIdealDistance)
            {
                // Too far -> Advance toward player
                myCharacter.Move(moveDir);
            }
            else if (distance < minIdealDistance)
            {
                // Too close -> Back away
                myCharacter.Move(-moveDir);
            }
            else
            {
                // In ideal range -> Face player
                myCharacter.Move(0f);
                transform.localScale = new Vector3(moveDir, 1f, 1f);
            }
        }

        private void HandleAttacking(float distance)
        {
            if (myCharacter.isStunned || myCharacter.isBlockingState || myCharacter.IsAttacking) return;

            if (distance <= maxIdealDistance + 0.3f && Time.time >= nextAttackTime)
            {
                // 35% chance to use Special Attack if Mana >= 30, otherwise use Normal Attack
                if (myCharacter.currentMana >= 30f && Random.value < 0.35f)
                {
                    Debug.Log("[VersusCPUAI] Offense: Special Attack!");
                    myCharacter.AttackSpecial();
                    TriggerWeaponSwing();
                }
                else
                {
                    Debug.Log("[VersusCPUAI] Offense: Normal Attack!");
                    myCharacter.AttackNormal();
                    TriggerWeaponSwing();
                }

                nextAttackTime = Time.time + attackCooldown + Random.Range(-0.15f, 0.25f);
            }
        }

        private Coroutine swingCoroutine;

        private void TriggerWeaponSwing()
        {
            Transform visuals = transform.Find("Visuals/Weapon");
            if (visuals == null) visuals = transform.Find("Visuals");
            if (visuals != null)
            {
                if (swingCoroutine != null) StopCoroutine(swingCoroutine);
                swingCoroutine = StartCoroutine(WeaponSwingRoutine(visuals));
            }
        }

        private IEnumerator WeaponSwingRoutine(Transform weaponTrans)
        {
            Vector3 handPivot = new Vector3(0.2f, 0.2f, 0f);
            Vector3 weaponOffset = new Vector3(0.8f, 0.5f, 0f) - handPivot;

            SpriteRenderer weaponSr = weaponTrans.GetComponent<SpriteRenderer>();
            Color origColor = weaponSr != null ? weaponSr.color : Color.white;

            float duration = 0.28f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float angle = Mathf.Lerp(75f, -95f, t);
                Quaternion rot = Quaternion.Euler(0, 0, angle);

                weaponTrans.localRotation = rot;
                weaponTrans.localPosition = handPivot + rot * weaponOffset;

                if (weaponSr != null)
                {
                    weaponSr.color = Color.Lerp(new Color(1f, 0.9f, 0.2f, 1f), origColor, t);
                }

                yield return null;
            }

            weaponTrans.localRotation = Quaternion.identity;
            weaponTrans.localPosition = handPivot + weaponOffset;
            if (weaponSr != null) weaponSr.color = origColor;
        }
    }
}
