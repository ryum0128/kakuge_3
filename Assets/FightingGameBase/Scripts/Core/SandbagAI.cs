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

        [Tooltip("If true, the dummy is completely fixed in place and cannot move.")]
        public bool isFixedInPlace = true;

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
        private Vector3 fixedPosition;

        private static Sprite squareSprite;

        void Start()
        {
            dummyCharacter = GetComponent<CharacterBase>();
            fixedPosition = transform.position;
            
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

            // Ensure weapon skin visual is attached and visible
            EnsureWeaponVisual();

            if (isFixedInPlace)
            {
                Rigidbody2D rb = GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }

            // Start battle automatically in GameManager if present
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                GameManager.Instance.StartBattle();
            }

            nextAttackTime = Time.time + attackInterval;
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
                if (!dummyCharacter.isStunned && !dummyCharacter.isBlockingState && !dummyCharacter.IsHurtLocked)
                {
                    Debug.Log("[SandbagAI] Dummy triggers auto-attack normal.");
                    dummyCharacter.AttackNormal();
                    TriggerWeaponSwing();
                }
                nextAttackTime = Time.time + attackInterval;
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
            Vector3 weaponOffset = new Vector3(0.6f, 0.3f, 0f);

            SpriteRenderer weaponSr = weaponTrans.GetComponent<SpriteRenderer>();
            Color origColor = weaponSr != null ? weaponSr.color : Color.white;

            float duration = 0.28f; // Dynamic 0.28s slash
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Wide arc slash from +60 degrees (high over head) to -90 degrees (ground slash)
                float angle = Mathf.Lerp(60f, -90f, t);
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

        void LateUpdate()
        {
            if (isFixedInPlace && dummyCharacter != null)
            {
                transform.position = new Vector3(fixedPosition.x, transform.position.y, transform.position.z);
                Rigidbody2D rb = GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }
        }
    }
}
