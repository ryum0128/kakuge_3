using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // Custom behavior for Taiken (Greatsword) character.
    public class TaikenCharacter : CharacterBase
    {
        [Header("Taiken Swing Settings")]
        [Tooltip("Rotation angle at the start of swing.")]
        public float swingStartAngle = 45f;
        [Tooltip("Rotation angle at the end of swing.")]
        public float swingEndAngle = -90f;
        [Tooltip("Duration of the swing down phase.")]
        public float swingDuration = 0.12f;
        [Tooltip("Duration of the recover phase.")]
        public float recoverDuration = 0.28f;

        [Header("DeepWoken Block Settings")]
        public float guardBreakDuration = 1.5f; // Stun duration when guard is broken
        private bool isGuardBroken = false;

        // Verified weapon parameters to guarantee correct visual sizes using Sliced Sprite size
        private readonly Vector2 normalWeaponSize = new Vector2(1.2f, 0.8f);
        private readonly Vector3 normalWeaponPos = new Vector3(0.7f, 0.6f, 0f);
        private readonly Vector2 specialWeaponSize = new Vector2(2.5f, 1.0f);
        private readonly Vector3 specialWeaponPos = new Vector3(1.6f, 0.6f, 0f);

        private Transform visualsTransform;
        private bool isSwinging = false;
        private bool isSpecialSwinging = false;
        private GameObject blockShield;

        private Coroutine normalAttackCoroutine;
        private Coroutine specialAttackCoroutine;

        public override bool IsAttacking => isSwinging || isSpecialSwinging;

        // DeepWoken Parry Cooldown variables
        private float lastParryTime = -10f;
        private float parryCooldown = 0.35f;

        private static Sprite defaultSquareSprite;
        private static Sprite defaultCircleSprite;

        private static Sprite GetDefaultSquareSprite()
        {
            if (defaultSquareSprite != null) return defaultSquareSprite;
            Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Color[] colors = new Color[32 * 32];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            defaultSquareSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            return defaultSquareSprite;
        }

        private static Sprite GetDefaultCircleSprite()
        {
            if (defaultCircleSprite != null) return defaultCircleSprite;
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            float center = (size - 1) / 2f;
            float radius = size / 2f - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 1f - dist);
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();

            defaultCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return defaultCircleSprite;
        }

        protected override void Start()
        {
            base.Start();

            // Locate or auto-create the weapon visual object (Visuals/Weapon) for swing rotations.
            Transform visualsRoot = transform.Find("Visuals");
            if (visualsRoot == null)
            {
                GameObject visObj = new GameObject("Visuals");
                visObj.transform.SetParent(transform, false);
                visualsRoot = visObj.transform;
            }

            visualsTransform = visualsRoot.Find("Weapon");
            if (visualsTransform == null)
            {
                GameObject weaponObj = new GameObject("Weapon");
                weaponObj.transform.SetParent(visualsRoot, false);
                weaponObj.transform.localPosition = normalWeaponPos;
                weaponObj.transform.localScale = Vector3.one;

                SpriteRenderer weaponSr = weaponObj.AddComponent<SpriteRenderer>();
                weaponSr.sprite = GetDefaultSquareSprite();
                weaponSr.color = new Color(0.75f, 0.78f, 0.85f, 1f); // Metallic silver
                weaponSr.sortingOrder = 5;
                visualsTransform = weaponObj.transform;
            }
            else
            {
                visualsTransform.localPosition = normalWeaponPos;
                SpriteRenderer weaponSr = visualsTransform.GetComponent<SpriteRenderer>();
                if (weaponSr == null)
                {
                    weaponSr = visualsTransform.gameObject.AddComponent<SpriteRenderer>();
                }
                if (weaponSr.sprite == null)
                {
                    weaponSr.sprite = GetDefaultSquareSprite();
                    weaponSr.color = new Color(0.75f, 0.78f, 0.85f, 1f);
                }
                weaponSr.sortingOrder = 5;
            }

            // Find or auto-create the block shield GameObject
            Transform shieldTrans = FindDeepChild(transform, "BlockShield");
            if (shieldTrans != null)
            {
                blockShield = shieldTrans.gameObject;
            }
            else
            {
                GameObject shield = new GameObject("BlockShield");
                shield.transform.SetParent(visualsRoot, false);
                shield.transform.localPosition = new Vector3(0.2f, 0.2f, 0f);

                SpriteRenderer shieldSr = shield.AddComponent<SpriteRenderer>();
                shieldSr.sprite = GetDefaultCircleSprite();
                shieldSr.color = new Color(0.3f, 0.8f, 1f, 0.6f);
                shieldSr.sortingOrder = 10;

                blockShield = shield;
            }

            SpriteRenderer sSr = blockShield.GetComponent<SpriteRenderer>();
            if (sSr == null)
            {
                sSr = blockShield.AddComponent<SpriteRenderer>();
            }
            if (sSr.sprite == null)
            {
                sSr.sprite = GetDefaultCircleSprite();
            }
            sSr.sortingOrder = 10;
            blockShield.SetActive(false);
        }

        private Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindDeepChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        protected override void Update()
        {
            base.Update();
            if (isDead)
            {
                if (blockShield != null && blockShield.activeSelf)
                {
                    blockShield.SetActive(false);
                }
                return;
            }
        }

        public override void Move(float direction)
        {
            // 特殊攻撃中（スペシャル攻撃中）またはガードブレイク・スタン中のみ移動を強制停止する（通常攻撃中は移動可能）
            if (isSpecialSwinging || isGuardBroken || isDead || isStunned)
            {
                if (rb != null)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
                return;
            }

            base.Move(direction);
        }

        public override void Stun(float duration)
        {
            CancelAttacks();
            base.Stun(duration);
        }

        // Override AttackNormal for Taiken custom swing.
        public override void AttackNormal()
        {
            if (isDead || isSwinging || isBlocking || isGuardBroken || isDashingOrEvading || IsHurtLocked) return;

            Debug.Log("Taiken Normal Attack triggered.");
            
            if (animator != null)
            {
                animator.SetTrigger("AttackNormal");
            }

            Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
            normalAttackCoroutine = StartCoroutine(SwingRoutine(hitbox));
        }

        private IEnumerator SwingRoutine(Hitbox hitbox)
        {
            isSwinging = true;
            Debug.Log("Taiken animation: windup start.");

            Vector3 handPivot = new Vector3(0.2f, 0.2f, 0f);
            Vector3 weaponOffset = normalWeaponPos - handPivot;

            SpriteRenderer weaponSr = visualsTransform != null ? visualsTransform.GetComponent<SpriteRenderer>() : null;
            Color origColor = weaponSr != null ? weaponSr.color : Color.white;

            float elapsed = 0f;

            // 1. 予備動作（ウィンドアップ）: 武器を肩上に高く振りかぶる (0.18秒)
            float windupDuration = 0.18f;
            while (elapsed < windupDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / windupDuration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f);

                float angle = Mathf.Lerp(0f, 75f, t); // 高く振りかぶる (+75度)
                if (visualsTransform != null)
                {
                    Quaternion rot = Quaternion.Euler(0, 0, angle);
                    visualsTransform.localRotation = rot;
                    visualsTransform.localPosition = handPivot + rot * weaponOffset;
                }
                yield return null;
            }

            // 2. 振り下ろしの瞬間に攻撃判定（Hitbox）を有効化！スキンと同じサイズ（1.2 x 0.8）に設定！
            if (hitbox != null)
            {
                hitbox.transform.localPosition = normalWeaponPos;
                BoxCollider2D col = hitbox.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    col.size = normalWeaponSize; // 武器スキンと同じ大きさと長さに同期！
                }
                StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, swingDuration));
            }

            // 3. 振り下ろしフェーズ: 地面に向かって前方に超大弧一気斬り (-95度) (0.16秒)
            elapsed = 0f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / swingDuration;
                t = t * t; 
                
                float angle = Mathf.Lerp(75f, -95f, t); // +75度から-95度まで170度の大斬撃！
                if (visualsTransform != null)
                {
                    Quaternion rot = Quaternion.Euler(0, 0, angle);
                    visualsTransform.localRotation = rot;
                    visualsTransform.localPosition = handPivot + rot * weaponOffset;
                    if (weaponSr != null)
                    {
                        weaponSr.color = Color.Lerp(new Color(1f, 0.9f, 0.2f, 1f), origColor, t);
                    }
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.04f);

            // 4. 回収（フォロースルー）フェーズ (0.22秒)
            elapsed = 0f;
            while (elapsed < recoverDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / recoverDuration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                
                float angle = Mathf.Lerp(-95f, 0f, t);
                if (visualsTransform != null)
                {
                    Quaternion rot = Quaternion.Euler(0, 0, angle);
                    visualsTransform.localRotation = rot;
                    visualsTransform.localPosition = handPivot + rot * weaponOffset;
                    if (weaponSr != null)
                    {
                        weaponSr.color = Color.Lerp(origColor, Color.white, t);
                    }
                }
                yield return null;
            }

            if (visualsTransform != null)
            {
                visualsTransform.localRotation = Quaternion.identity;
                visualsTransform.localPosition = normalWeaponPos;
                if (weaponSr != null) weaponSr.color = origColor;
            }

            Debug.Log("Taiken animation: swing completed.");
            isSwinging = false;
        }

        [Header("Taiken Special Attack Settings")]
        [Tooltip("Duration of the charging phase.")]
        public float chargeWindupDuration = 0.7f;
        [Tooltip("Weapon rotation angle during charge.")]
        public float chargeWindupAngle = 120f;
        [Tooltip("Duration of the release swing phase.")]
        public float chargeReleaseDuration = 0.06f;
        [Tooltip("Weapon rotation angle after release.")]
        public float chargeReleaseAngle = -130f;
        [Tooltip("Damage of the special attack.")]
        public int chargeSlashDamage = 30;

        public override void AttackSpecial()
        {
            if (isDead || isSwinging || isBlocking || isGuardBroken || isDashingOrEvading || IsHurtLocked) return;
            if (currentMana < 30f)
            {
                Debug.Log("Not enough Mana for Special Attack!");
                return;
            }
            currentMana -= 30f;
            Debug.Log("Taiken Special Attack triggered.");
            specialAttackCoroutine = StartCoroutine(ChargedSwingRoutine());
        }

        private IEnumerator ChargedSwingRoutine()
        {
            isSwinging = true;
            isSpecialSwinging = true;

            Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
            BoxCollider2D col = hitbox != null ? hitbox.GetComponent<BoxCollider2D>() : null;
            SpriteRenderer weaponSr = visualsTransform != null ? visualsTransform.GetComponent<SpriteRenderer>() : null;

            int originalDamage = hitbox != null ? hitbox.damage : 15;
            Vector3 origPos = hitbox != null ? hitbox.transform.localPosition : Vector3.zero;
            Vector2 origSize = col != null ? col.size : Vector2.zero;

            // Enlarge weapon visual to match special attack size exactly
            ApplyWeaponVisualSize(specialWeaponSize, specialWeaponPos);

            // Phase 1: Charging
            Debug.Log("Charged Swing: Phase 1 (Charge) started.");
            float elapsed = 0f;

            while (elapsed < chargeWindupDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Pow(elapsed / chargeWindupDuration, 0.6f);
                float angle = Mathf.Lerp(0f, chargeWindupAngle, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.Euler(0, 0, chargeWindupAngle);
            yield return new WaitForSeconds(0.05f);

            // Phase 2: Release
            Debug.Log("Charged Swing: Phase 2 (Release) started.");

            if (hitbox != null)
            {
                hitbox.damage = chargeSlashDamage;
                hitbox.postureDamageMultiplier = 4.0f; // 大幅に体幹ダメージを増加 (4.0x)
                hitbox.isNormalAttack = false; // 特殊攻撃中は通常攻撃としてのマナ回復を無効化

                if (col != null)
                {
                    hitbox.transform.localPosition = specialWeaponPos;
                    col.size = specialWeaponSize;
                }

                StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, chargeReleaseDuration + 0.08f));
                StartCoroutine(RestoreDamageAndSize(hitbox, col, weaponSr, originalDamage, origPos, origSize, chargeReleaseDuration + 0.1f));
            }

            elapsed = 0f;
            while (elapsed < chargeReleaseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Pow(elapsed / chargeReleaseDuration, 4f);
                float angle = Mathf.Lerp(chargeWindupAngle, chargeReleaseAngle, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.Euler(0, 0, chargeReleaseAngle);
            yield return new WaitForSeconds(0.08f);

            // Phase 3: Recovery
            Debug.Log("Charged Swing: Phase 3 (Recovery) started.");
            elapsed = 0f;
            float recoverTime = recoverDuration * 1.3f;
            while (elapsed < recoverTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Sin((elapsed / recoverTime) * Mathf.PI * 0.5f);
                float angle = Mathf.Lerp(chargeReleaseAngle, 0f, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.identity;

            Debug.Log("Charged Swing completed.");
            isSpecialSwinging = false;
            isSwinging = false;
        }

        private void ApplyWeaponVisualSize(Vector2 targetSize, Vector3 targetPos)
        {
            if (visualsTransform == null) return;

            visualsTransform.localPosition = targetPos;
            SpriteRenderer weaponSr = visualsTransform.GetComponent<SpriteRenderer>();

            if (weaponSr != null)
            {
                if (weaponSr.drawMode == SpriteDrawMode.Sliced && weaponSr.sprite != null && weaponSr.sprite.border != Vector4.zero)
                {
                    visualsTransform.localScale = Vector3.one;
                    weaponSr.size = targetSize;
                }
                else
                {
                    weaponSr.drawMode = SpriteDrawMode.Simple;
                    float baseW = (weaponSr.sprite != null && weaponSr.sprite.bounds.size.x > 0) ? weaponSr.sprite.bounds.size.x : 1.2f;
                    float baseH = (weaponSr.sprite != null && weaponSr.sprite.bounds.size.y > 0) ? weaponSr.sprite.bounds.size.y : 0.8f;
                    visualsTransform.localScale = new Vector3(targetSize.x / baseW, targetSize.y / baseH, 1f);
                }
            }
            else
            {
                visualsTransform.localScale = new Vector3(targetSize.x / normalWeaponSize.x, targetSize.y / normalWeaponSize.y, 1f);
            }
        }

        private IEnumerator RestoreDamageAndSize(Hitbox hitbox, BoxCollider2D col, SpriteRenderer weaponSr, int originalDamage, Vector3 originalPos, Vector2 originalSize, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (hitbox != null)
            {
                hitbox.damage = originalDamage;
                hitbox.transform.localPosition = originalPos;
                hitbox.postureDamageMultiplier = 1.5f; // 通常の蓄積倍率に戻す
                hitbox.isNormalAttack = true; // 通常攻撃判定に戻す
            }
            if (col != null)
            {
                col.size = originalSize;
            }
            ApplyWeaponVisualSize(normalWeaponSize, normalWeaponPos);
        }

        [Header("Block & Parry Settings")]
        [Tooltip("Duration of the parry window.")]
        public float parryWindow = 0.25f;

        private bool isBlocking = false;
        private bool isParrying = false;
        private Coroutine blockCoroutine;

        public override void StartBlock()
        {
            if (isDead || isSwinging || isGuardBroken) return;

            // DeepWoken Block Spam Prevention: Check parry cooldown
            if (Time.time - lastParryTime < parryCooldown)
            {
                isParrying = false;
                isBlocking = false;
                isBlockingState = false;
                Debug.Log("Block start ignored (Parry on Cooldown - Shield disabled).");
                return;
            }

            isBlocking = true;
            isBlockingState = true;
            isParrying = true;
            lastParryTime = Time.time;
            Debug.Log("Block start (Parry Window Active).");
            StartCoroutine(CloseParryWindow());

            if (blockShield != null)
            {
                // Force-override shield visual parameters to ensure it is 100% visible on block.
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr == null)
                {
                    shieldSr = blockShield.AddComponent<SpriteRenderer>();
                }

                if (shieldSr.sprite == null)
                {
                    shieldSr.sprite = GetDefaultCircleSprite();
                }
                
                shieldSr.drawMode = SpriteDrawMode.Sliced;
                shieldSr.size = new Vector2(0.8f, 2.0f); // Cover the front of the character (1x2 size)
                // Vivid blue if parrying, dull blue if block-only
                shieldSr.color = isParrying ? new Color(0.3f, 0.8f, 1f, 0.6f) : new Color(0.3f, 0.5f, 0.7f, 0.45f);
                shieldSr.sortingOrder = 10; // Bring to absolute front

                // Position shield right in front of the character's face/body.
                blockShield.transform.localPosition = new Vector3(0.7f, 0f, 0f);
                blockShield.transform.localScale = Vector3.one;

                blockShield.SetActive(true);
            }
        }

        public override void StopBlock()
        {
            if (!isBlocking) return;

            isBlocking = false;
            isBlockingState = false;
            isParrying = false;
            Debug.Log("Block stop.");

            if (blockShield != null)
            {
                blockShield.SetActive(false);
            }
        }

        private void CancelAttacks()
        {
            isSwinging = false;
            isSpecialSwinging = false;

            if (normalAttackCoroutine != null)
            {
                StopCoroutine(normalAttackCoroutine);
                normalAttackCoroutine = null;
            }
            if (specialAttackCoroutine != null)
            {
                StopCoroutine(specialAttackCoroutine);
                specialAttackCoroutine = null;
            }

            Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
            if (hitbox != null)
            {
                hitbox.gameObject.SetActive(false);
            }

            // Restore weapon visual properties
            if (visualsTransform != null)
            {
                visualsTransform.localPosition = normalWeaponPos;
                visualsTransform.localRotation = Quaternion.identity;
                visualsTransform.localScale = Vector3.one;

                SpriteRenderer weaponSr = visualsTransform.GetComponent<SpriteRenderer>();
                if (weaponSr != null)
                {
                    weaponSr.drawMode = SpriteDrawMode.Sliced;
                    weaponSr.size = normalWeaponSize;
                }
            }
        }

        public override void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isDead || isInvincible) return;

            CancelAttacks();

            // Locate the opponent (attacker)
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

            bool isBackAttack = false;
            if (opponent != null)
            {
                float faceDir = transform.localScale.x; // 1 = right, -1 = left
                float relativeX = opponent.transform.position.x - transform.position.x;
                
                // If facing right but opponent is on left, or facing left but opponent is on right -> Back attack!
                if ((faceDir > 0 && relativeX < 0) || (faceDir < 0 && relativeX > 0))
                {
                    isBackAttack = true;
                }
            }

            if (isBlocking && !isBackAttack)
            {
                if (isParrying)
                {
                    // --- DeepWoken Parry Success ---
                    Debug.Log("★ PARRY SUCCESS! (DeepWoken Style) ★");
                    
                    // Reset parry cooldown immediately to reward player for successful parry!
                    lastParryTime = -10f;

                    // Recover posture by the attack's damage as a reward
                    currentPosture -= damage;
                    if (currentPosture < 0f) currentPosture = 0f;

                    // パリー成功報酬としてマナを少量(6f)回復する
                    AddMana(6f);

                    if (opponent != null)
                    {
                        // Stun attacker for 0.6 seconds
                        opponent.Stun(0.6f);

                        // Apply knockback to attacker (bypassing protected member constraints)
                        Rigidbody2D oppRb = opponent.GetComponent<Rigidbody2D>();
                        if (oppRb != null)
                        {
                            float pushDir = (opponent.transform.position.x > transform.position.x) ? 1.0f : -1.0f;
                            oppRb.linearVelocity = new Vector2(pushDir * 8f, oppRb.linearVelocity.y);
                        }
                    }

                    StartCoroutine(ParrySuccessRoutine());
                }
                else
                {
                    // --- DeepWoken Block Success ---
                    Debug.Log("Block success. Posture accumulating.");
                    
                    // Accumulate posture based on damage (using custom multiplier if hit by a specific hitbox)
                    float postureMultiplier = attackerHitbox != null ? attackerHitbox.postureDamageMultiplier : 1.5f;
                    currentPosture += damage * postureMultiplier;

                    if (currentPosture >= maxPosture)
                    {
                        // GUARD BREAK!
                        StartCoroutine(GuardBreakRoutine());
                    }
                    else
                    {
                        StartCoroutine(BlockHitRoutine());
                    }
                }
                return; // Negate damage
            }

            if (blockShield != null)
            {
                blockShield.SetActive(false);
            }
            isBlockingState = false;

            if (isBlocking && isBackAttack)
            {
                Debug.Log("Blocked from behind! Guard bypassed!");
            }

            base.TakeDamage(damage, attackerHitbox);

            if (isDead && blockShield != null)
            {
                blockShield.SetActive(false);
            }
        }

        private IEnumerator GuardBreakRoutine()
        {
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerHitStop(2); // Freeze frame for 2 frames
            }

            isGuardBroken = true;
            isBlocking = false;
            isBlockingState = false;
            isParrying = false;
 
            Debug.Log("!! GUARD BREAK !!");

            // Visually turn shield red and flash, then crush it
            if (blockShield != null)
            {
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr != null)
                {
                    shieldSr.color = new Color(1f, 0.2f, 0.2f, 0.8f);
                }
                blockShield.SetActive(true);
                yield return new WaitForSeconds(0.2f);
                blockShield.SetActive(false);
            }

            // Stun character and visual color cue (orange-red)
            SpriteRenderer bodySr = GetComponentInChildren<SpriteRenderer>();
            Color originalColor = bodySr != null ? bodySr.color : Color.white;
            if (bodySr != null)
            {
                bodySr.color = new Color(0.9f, 0.3f, 0.1f, 1f);
            }

            yield return new WaitForSeconds(guardBreakDuration);

            // Recover
            if (bodySr != null)
            {
                bodySr.color = originalColor;
            }
            currentPosture = 0f;
            isGuardBroken = false;
            Debug.Log("Guard Break Stun finished.");
        }

        private IEnumerator BlockEnterRoutine()
        {
            yield break;
        }

        private IEnumerator BlockExitRoutine()
        {
            yield break;
        }

        private IEnumerator CloseParryWindow()
        {
            yield return new WaitForSeconds(parryWindow);
            if (isBlocking)
            {
                isParrying = false;
                Debug.Log("Parry window closed.");
            }
        }

        private IEnumerator ParrySuccessRoutine()
        {
            SpriteRenderer bodySr = GetComponentInChildren<SpriteRenderer>();
            Color originalColor = bodySr != null ? bodySr.color : new Color(0.8f, 0.2f, 0.2f, 1f);
            if (bodySr != null)
            {
                bodySr.color = Color.white; // 体を白くフラッシュ
            }

            // Flash shield white for parry feedback
            if (blockShield != null)
            {
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr != null)
                {
                    shieldSr.color = new Color(1f, 1f, 1f, 0.9f); // Pure white flash
                }
            }
                
            yield return new WaitForSeconds(0.1f);

            if (bodySr != null)
            {
                bodySr.color = originalColor; // 元の色に戻す
            }

            if (blockShield != null)
            {
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr != null)
                {
                    shieldSr.color = isParrying ? new Color(0.3f, 0.8f, 1f, 0.6f) : new Color(0.3f, 0.5f, 0.7f, 0.45f);
                }
            }

            float elapsed = 0f;
            float bounceDuration = 0.06f;
            while (elapsed < bounceDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / bounceDuration;
                float angle = blockShield != null ? 15f * Mathf.Sin(t * Mathf.PI) : 0f;
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.identity;
        }

        private IEnumerator BlockHitRoutine()
        {
            float elapsed = 0f;
            float shakeDuration = 0.08f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shakeDuration;
                float angle = -15f * Mathf.Sin(t * Mathf.PI);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.identity;
        }
    }
}
