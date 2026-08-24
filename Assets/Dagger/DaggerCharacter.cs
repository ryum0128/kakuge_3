using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // =================================================================================
    // 【DaggerCharacter（ダガーキャラクター用のカスタム挙動）】
    // CharacterBase を継承し、ダガーの素早い攻撃と移動ステップ攻撃を行います。
    // =================================================================================
    public class DaggerCharacter : CharacterBase
    {
        [Header("ダガーの通常攻撃設定")]
        [Tooltip("通常攻撃の振り下ろし開始時の回転角度")]
        public float swingStartAngle = 30f;
        [Tooltip("通常攻撃の振り下ろしきった時の回転角度")]
        public float swingEndAngle = -45f;
        [Tooltip("通常攻撃の突き出し距離")]
        public float thrustDistance = 0.3f;
        [Tooltip("通常攻撃にかかる時間（秒）")]
        public float swingDuration = 0.06f;
        [Tooltip("元の位置に戻る時間（秒）")]
        public float recoverDuration = 0.08f;

        [Header("ダガーの特殊攻撃（３連突きステップ）設定")]
        [Tooltip("1歩あたりの突進速度")]
        public float specialStepSpeed = 8f;
        [Tooltip("1歩あたりの突進時間（秒）")]
        public float specialStepDuration = 0.12f;
        [Tooltip("ステップ間のディレイ（秒）")]
        public float specialStepDelay = 0.08f;
        [Tooltip("特殊攻撃の1発あたりのダメージ")]
        public int specialSlashDamage = 8;

        private Transform visualsTransform;
        private GameObject blockShield;
        private bool isSwinging = false;
        private bool isSpecialAttacking = false;
        private bool isGuardBroken = false;

        private Coroutine normalAttackCoroutine;
        private Coroutine specialAttackCoroutine;

        public override bool IsAttacking => isSwinging || isSpecialAttacking;
        private float guardBreakDuration = 1.5f;
        
        // DeepWoken Parry Cooldown variables
        private float lastParryTime = -10f;
        private float parryCooldown = 0.35f;

        protected override void Start()
        {
            base.Start();

            // 武器オブジェクト（Visuals/Weapon）のTransformを特定し、回転・移動の対象にします（キャラクターは動かさず武器だけ動かすため）
            visualsTransform = transform.Find("Visuals/Weapon");
            if (visualsTransform == null)
            {
                // バックアップとして従来のVisualsやHoverEffectを使用します
                HoverEffect hover = GetComponentInChildren<HoverEffect>(true);
                if (hover != null)
                {
                    visualsTransform = hover.transform;
                }
                else
                {
                    visualsTransform = transform.Find("Visuals");
                }
            }

            if (visualsTransform == null)
            {
                Debug.LogError($"[DaggerCharacter] 見た目オブジェクト（Visuals）が見つかりませんでした！");
            }
            else
            {
                // Automatically adjust weapon sprite size to match the PunchHitbox collider size at runtime
                Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
                if (hitbox != null)
                {
                    BoxCollider2D boxCol = hitbox.GetComponent<BoxCollider2D>();
                    if (boxCol != null)
                    {
                        SpriteRenderer weaponSr = visualsTransform.GetComponent<SpriteRenderer>();
                        if (weaponSr != null)
                        {
                            visualsTransform.localScale = Vector3.one;
                            weaponSr.drawMode = SpriteDrawMode.Sliced;
                            weaponSr.size = boxCol.size;
                            visualsTransform.localPosition = hitbox.transform.localPosition;
                        }
                    }
                }
            }

            // Find the block shield GameObject using deep search to avoid hierarchy mismatch.
            Transform shieldTrans = FindDeepChild(transform, "BlockShield");
            if (shieldTrans != null)
            {
                blockShield = shieldTrans.gameObject;
                blockShield.SetActive(false);
            }
            else
            {
                // Fallback: If not pre-configured in prefab, create it dynamically
                Transform parent = visualsTransform != null ? visualsTransform.parent : transform;
                GameObject shield = new GameObject("BlockShield");
                shield.transform.SetParent(parent);
                shield.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
                shield.transform.localPosition = new Vector3(0.2f, 0.2f, 0f);

                SpriteRenderer shieldSr = shield.AddComponent<SpriteRenderer>();
                SpriteRenderer charSr = GetComponentInChildren<SpriteRenderer>();
                if (charSr != null)
                {
                    shieldSr.sprite = charSr.sprite;
                    shieldSr.drawMode = SpriteDrawMode.Sliced;
                }
                shieldSr.color = new Color(0.3f, 0.8f, 1f, 0.55f);
                shieldSr.sortingOrder = 1;

                blockShield = shield;
                blockShield.SetActive(false);
            }
        }

        // 移動制御のオーバーライド: 特殊攻撃中はプレイヤーの移動入力を無視します
        public override void Move(float direction)
        {
            if (isSpecialAttacking) return;
            base.Move(direction);
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

        private Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform result = FindDeepChild(child, name);
                if (result != null) return result;
            }
            return null;
        }

        // 通常攻撃（AttackNormal）
        public override void AttackNormal()
        {
            if (isDead || isSwinging || isSpecialAttacking || isDashingOrEvading || IsHurtLocked) return;

            Debug.Log("ダガー・通常攻撃（予備動作あり斬撃）発動！");
            
            if (animator != null)
            {
                animator.SetTrigger("AttackNormal");
            }

            Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
            normalAttackCoroutine = StartCoroutine(NormalAttackRoutine(hitbox));
        }

        private IEnumerator NormalAttackRoutine(Hitbox hitbox)
        {
            isSwinging = true;
            float elapsed = 0f;
            Vector3 originalLocalPos = visualsTransform != null ? visualsTransform.localPosition : Vector3.zero;

            // 1. 予備動作（ウィンドアップ）: ダガーを後ろに引く (0.15秒)
            float windupDuration = 0.15f;
            while (elapsed < windupDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / windupDuration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f); // イージング

                float angle = Mathf.Lerp(0f, -30f, t);
                float posX = Mathf.Lerp(originalLocalPos.x, originalLocalPos.x - 0.15f, t);

                if (visualsTransform != null)
                {
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                    visualsTransform.localPosition = new Vector3(posX, visualsTransform.localPosition.y, visualsTransform.localPosition.z);
                }
                yield return null;
            }

            // 2. 攻撃判定の有効化 (予備動作の直後に発生！)
            if (hitbox != null)
            {
                StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, 0.12f));
            }

            // 3. 素早く突き刺す＆振る (0.12秒)
            elapsed = 0f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / swingDuration;
                t = t * t; // イージング

                float angle = Mathf.Lerp(-30f, swingEndAngle, t);
                float posX = Mathf.Lerp(originalLocalPos.x - 0.15f, originalLocalPos.x + thrustDistance, t);

                if (visualsTransform != null)
                {
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                    visualsTransform.localPosition = new Vector3(posX, visualsTransform.localPosition.y, visualsTransform.localPosition.z);
                }
                yield return null;
            }

            if (visualsTransform != null)
            {
                visualsTransform.localRotation = Quaternion.Euler(0, 0, swingEndAngle);
                visualsTransform.localPosition = new Vector3(originalLocalPos.x + thrustDistance, visualsTransform.localPosition.y, visualsTransform.localPosition.z);
            }

            yield return new WaitForSeconds(0.02f); // 突き刺し位置で極小ディレイ

            // 4. 元に戻る (0.15秒)
            elapsed = 0f;
            while (elapsed < recoverDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / recoverDuration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f);

                float angle = Mathf.Lerp(swingEndAngle, 0f, t);
                float posX = Mathf.Lerp(originalLocalPos.x + thrustDistance, originalLocalPos.x, t);

                if (visualsTransform != null)
                {
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                    visualsTransform.localPosition = new Vector3(posX, visualsTransform.localPosition.y, visualsTransform.localPosition.z);
                }
                yield return null;
            }

            if (visualsTransform != null)
            {
                visualsTransform.localRotation = Quaternion.identity;
                visualsTransform.localPosition = originalLocalPos;
            }

            isSwinging = false;
        }

        // 特殊攻撃（AttackSpecial）
        // 前に３歩進んで１歩ずつに斬撃を入れる（通常攻撃を３回するようなもの）
        public override void AttackSpecial()
        {
            if (isDead || isSwinging || isSpecialAttacking || isDashingOrEvading || IsHurtLocked) return;

            if (currentMana < 20f)
            {
                Debug.Log("Not enough Mana for Special Attack!");
                return;
            }
            currentMana -= 20f;

            Debug.Log("Dagger Special Attack triggered.");
            specialAttackCoroutine = StartCoroutine(SpecialStepSlashRoutine());
        }

        private IEnumerator SpecialStepSlashRoutine()
        {
            isSpecialAttacking = true;
            isSwinging = true;

            if (animator != null)
            {
                animator.SetTrigger("AttackSpecial");
            }

            Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
            int originalDamage = 10;
            if (hitbox != null)
            {
                originalDamage = hitbox.damage;
                hitbox.damage = specialSlashDamage; // 特殊攻撃用にダメージを設定
                hitbox.isNormalAttack = false; // 特殊攻撃中は通常攻撃としてのマナ回復を無効化
            }

            Vector3 originalLocalPos = visualsTransform != null ? visualsTransform.localPosition : Vector3.zero;

            // 1. 予備動作（ウィンドアップ・溜めフェーズ）: 武器を後ろに大きく引いて溜める (0.35秒)
            float chargeDuration = 0.35f;
            float elapsedCharge = 0f;
            while (elapsedCharge < chargeDuration)
            {
                elapsedCharge += Time.deltaTime;
                float t = elapsedCharge / chargeDuration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f); // イージング

                float angle = Mathf.Lerp(0f, -45f, t);
                float posX = Mathf.Lerp(originalLocalPos.x, originalLocalPos.x - 0.3f, t);

                if (visualsTransform != null)
                {
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                    visualsTransform.localPosition = new Vector3(posX, visualsTransform.localPosition.y, visualsTransform.localPosition.z);
                }
                yield return null;
            }

            // 2. 3連撃の開始！
            for (int step = 0; step < 3; step++)
            {
                if (isDead) break;

                Debug.Log($"ダガー特殊攻撃: ステップ {step + 1} / 3");

                // 向きの取得 (1 または -1)
                float faceDir = transform.localScale.x;

                // 物理エンジンで突進スピードを適用
                rb.linearVelocity = new Vector2(faceDir * specialStepSpeed, rb.linearVelocity.y);

                // 攻撃判定をアクティブにする
                if (hitbox != null)
                {
                    StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, specialStepDuration + 0.05f));
                }

                // 突進しながら斬りつける（ビジュアルの動き）
                float elapsed = 0f;
                while (elapsed < specialStepDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / specialStepDuration;
                    
                    // 斬りつけ中のダガーの回転と前進
                    float angle = Mathf.Lerp(swingStartAngle, swingEndAngle, t);
                    float posX = Mathf.Lerp(originalLocalPos.x, originalLocalPos.x + thrustDistance * 1.5f, t);

                    if (visualsTransform != null)
                    {
                        visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                        visualsTransform.localPosition = new Vector3(posX, visualsTransform.localPosition.y, visualsTransform.localPosition.z);
                    }
                    yield return null;
                }

                // 一時的に移動を止める
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

                // ダガーを素早く元の位置に戻す
                elapsed = 0f;
                float resetDuration = specialStepDelay;
                while (elapsed < resetDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / resetDuration;
                    float angle = Mathf.Lerp(swingEndAngle, swingStartAngle, t); // 次の突きのために開始角度へ
                    float posX = Mathf.Lerp(originalLocalPos.x + thrustDistance * 1.5f, originalLocalPos.x, t);

                    if (visualsTransform != null)
                    {
                        visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                        visualsTransform.localPosition = new Vector3(posX, visualsTransform.localPosition.y, visualsTransform.localPosition.z);
                    }
                    yield return null;
                }
            }

            // 終了処理: ビジュアルの完全なリセット
            if (visualsTransform != null)
            {
                visualsTransform.localRotation = Quaternion.identity;
                visualsTransform.localPosition = originalLocalPos;
            }

            if (hitbox != null)
            {
                hitbox.damage = originalDamage; // ダメージを元に戻す
                hitbox.isNormalAttack = true; // 通常攻撃判定に戻す
            }

            isSpecialAttacking = false;
            isSwinging = false;
            Debug.Log("ダガー・特殊攻撃（３連突きステップ）完了！");
        }

        // =========================================================
        // ブロック・パリー設定（ダガー用: 素早さを活かした受け流し）
        // =========================================================
        [Header("ブロック・パリー設定")]
        [Tooltip("ブロック時の角度")]
        public float blockAngle = 75f;
        [Tooltip("ブロック姿勢に移行する時間")]
        public float blockEnterDuration = 0.05f;
        [Tooltip("パリー受付窓の長さ（秒）")]
        public float parryWindow = 0.2f;

        private bool isBlocking = false;
        private bool isParrying = false;
        private Coroutine blockCoroutine;

        public override void StartBlock()
        {
            if (isDead || isSwinging || isSpecialAttacking || isGuardBroken) return;

            isBlocking = true;
            isBlockingState = true;

            // DeepWoken Block Spam Prevention: Check parry cooldown
            if (Time.time - lastParryTime < parryCooldown)
            {
                isParrying = false;
                Debug.Log("ダガーブロック開始（パリークールダウン中 - ブロックのみ）");
            }
            else
            {
                isParrying = true;
                lastParryTime = Time.time;
                Debug.Log("ダガーブロック開始！パリー受付窓オープン！");
                StartCoroutine(CloseParryWindow());
            }

            if (blockCoroutine != null) StopCoroutine(blockCoroutine);
            blockCoroutine = StartCoroutine(BlockEnterRoutine());

            if (blockShield != null)
            {
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr == null)
                {
                    shieldSr = blockShield.AddComponent<SpriteRenderer>();
                }

                if (shieldSr.sprite == null)
                {
                    SpriteRenderer charSr = GetComponentInChildren<SpriteRenderer>();
                    if (charSr != null)
                    {
                        shieldSr.sprite = charSr.sprite;
                    }
                }
                
                shieldSr.drawMode = SpriteDrawMode.Sliced;
                shieldSr.size = new Vector2(0.8f, 2.0f); // Cover the front of the character (1x2 size)
                // Vivid blue if parrying, dull blue if block-only (same as Taiken)
                shieldSr.color = isParrying ? new Color(0.3f, 0.8f, 1f, 0.6f) : new Color(0.3f, 0.5f, 0.7f, 0.45f);
                shieldSr.sortingOrder = 10; // Bring to absolute front

                // Position shield right in front of the character
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
            Debug.Log("ダガーブロック解除。");

            if (blockCoroutine != null) StopCoroutine(blockCoroutine);
            blockCoroutine = StartCoroutine(BlockExitRoutine());

            if (blockShield != null)
            {
                blockShield.SetActive(false);
            }
        }

        private void CancelAttacks()
        {
            isSwinging = false;
            isSpecialAttacking = false;

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
                visualsTransform.localRotation = Quaternion.identity;
                visualsTransform.localPosition = new Vector3(0.5f, 0f, 0f);
            }
        }

        public override void ResetActionStates()
        {
            base.ResetActionStates();
            isSwinging = false;
            isSpecialAttacking = false;
        }

        public override void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isDead || isInvincible) return;

            CancelAttacks();

            // Find opponent (attacker)
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
                    Debug.Log("★ダガーパリー成功！ノーダメージ！");
                    
                    // Reset parry cooldown immediately to reward player for successful parry!
                    lastParryTime = -10f;

                    // Recover posture by the attack's damage as a reward
                    currentPosture -= damage;
                    if (currentPosture < 0f) currentPosture = 0f;

                    // パリー成功報酬としてマナを少量(6f)回復する
                    AddMana(6f);

                    StartCoroutine(ParrySuccessRoutine());
                }
                else
                {
                    Debug.Log("ダガーブロック成功！ダメージを防いだ！");
                    
                    // Accumulate posture based on damage (using custom multiplier if hit by a specific hitbox)
                    float postureMultiplier = attackerHitbox != null ? attackerHitbox.postureDamageMultiplier : 1.5f;
                    currentPosture += damage * postureMultiplier;

                    if (currentPosture >= maxPosture)
                    {
                        StartCoroutine(GuardBreakRoutine());
                    }
                    else
                    {
                        StartCoroutine(BlockHitRoutine());
                    }
                }
                return;
            }

            if (isBlocking && isBackAttack)
            {
                Debug.Log("Blocked from behind! Guard bypassed!");
            }

            base.TakeDamage(damage, attackerHitbox);
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
            Debug.Log("!! DAGGER GUARD BREAK !!");

            if (blockShield != null)
            {
                blockShield.SetActive(false);
            }

            // Visually turn character red-orange
            SpriteRenderer bodySr = GetComponentInChildren<SpriteRenderer>();
            Color originalColor = bodySr != null ? bodySr.color : Color.white;
            if (bodySr != null)
            {
                bodySr.color = new Color(0.9f, 0.3f, 0.1f, 1f);
            }

            // Stun for 1.5 seconds
            Stun(guardBreakDuration);

            yield return new WaitForSeconds(guardBreakDuration);

            if (bodySr != null)
            {
                bodySr.color = originalColor;
            }
            currentPosture = 0f;
            isGuardBroken = false;
            Debug.Log("Dagger Guard Break Stun finished.");
        }

        private IEnumerator BlockEnterRoutine()
        {
            float elapsed = 0f;
            float startAngle = visualsTransform != null ? visualsTransform.localRotation.eulerAngles.z : 0f;
            if (startAngle > 180f) startAngle -= 360f;

            while (elapsed < blockEnterDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / blockEnterDuration;
                t = 1f - Mathf.Pow(1f - t, 3f);
                float angle = Mathf.Lerp(startAngle, blockAngle, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.Euler(0, 0, blockAngle);
        }

        private IEnumerator BlockExitRoutine()
        {
            float elapsed = 0f;
            float exitDuration = 0.1f;

            while (elapsed < exitDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Sin((elapsed / exitDuration) * Mathf.PI * 0.5f);
                float angle = Mathf.Lerp(blockAngle, 0f, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.identity;
        }

        private IEnumerator CloseParryWindow()
        {
            yield return new WaitForSeconds(parryWindow);
            if (isBlocking)
            {
                isParrying = false;
                Debug.Log("ダガーパリー窓クローズ（通常ブロック状態へ）");
            }
        }

        private IEnumerator ParrySuccessRoutine()
        {
            SpriteRenderer bodySr = GetComponentInChildren<SpriteRenderer>();
            Color originalColor = bodySr != null ? bodySr.color : new Color(0.2f, 0.6f, 0.9f, 1f);
            if (bodySr != null)
            {
                bodySr.color = Color.white; // 体を白くフラッシュ
            }

            if (blockShield != null)
            {
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr != null)
                {
                    shieldSr.color = new Color(1f, 1f, 1f, 0.9f); // シールドも白くフラッシュ
                }
            }

            float elapsed = 0f;
            float bounceDuration = 0.04f;
            while (elapsed < bounceDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / bounceDuration;
                float angle = Mathf.Lerp(blockAngle, blockAngle - 30f, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            elapsed = 0f;
            float flashDuration = 0.08f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flashDuration;
                float angle = Mathf.Lerp(blockAngle - 30f, blockAngle + 20f, t * t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (bodySr != null)
            {
                bodySr.color = originalColor; // 体の色を復元
            }

            if (blockShield != null)
            {
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr != null)
                {
                    shieldSr.color = isParrying ? new Color(0.3f, 0.8f, 1f, 0.6f) : new Color(0.3f, 0.5f, 0.7f, 0.45f);
                }
            }

            elapsed = 0f;
            float returnDuration = 0.1f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Sin((elapsed / returnDuration) * Mathf.PI * 0.5f);
                float angle = Mathf.Lerp(blockAngle + 20f, blockAngle, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.Euler(0, 0, blockAngle);
        }

        private IEnumerator BlockHitRoutine()
        {
            float elapsed = 0f;
            float shakeDuration = 0.06f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shakeDuration;
                float angle = blockAngle - Mathf.Sin(t * Mathf.PI) * 10f;
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.Euler(0, 0, blockAngle);
        }
    }
}
