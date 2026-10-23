using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // =================================================================================
    // 【ShadowCharacter（影の力を操るダークキャラクター）】
    // 体は他キャラクターと同じスタイリッシュなグレーの箱型。
    // 通常攻撃は2秒かけて放つ超絶威力の一撃「影絶断（ダメージ35）」。
    // ただし、強力な代償として1回攻撃を使うたびに自身の体力が5減少します。
    // スペシャル攻撃は指示待ち（未実装）。
    // ブロック・パリー仕様は他キャラクター（Taiken / Flame / Thunder / Ice）と完全共通です。
    // =================================================================================
    public class ShadowCharacter : CharacterBase
    {
        [Header("影の刃（Shadow Blade）通常攻撃設定")]
        [Tooltip("通常攻撃のダメージ（超高威力: 35）")]
        public int normalShadowDamage = 35;

        [Tooltip("攻撃1回あたりの自傷ダメージ（体力消費: 5）")]
        public int selfHpCost = 5;

        [Tooltip("予備動作・闇の凝縮時間（秒）（0.90s + 0.50s = 1.40s: パリーしやすいように溜め時間を延長）")]
        public float windupDuration = 1.40f;
        [Tooltip("振り下ろし一閃時間（秒）")]
        public float swingDuration = 0.35f;
        [Tooltip("構え戻し・反動硬直時間（秒）")]
        public float recoverDuration = 0.75f;
        // 合計所要時間: 1.40 + 0.35 + 0.75 = 2.50秒（重厚かつパリーの駆け引きが熱い一撃）

        [Tooltip("剣の振りかぶり角度")]
        public float swingStartAngle = 90f;
        [Tooltip("剣の振り下ろし終了角度")]
        public float swingEndAngle = -95f;

        [Header("影の領域（Shadow Domain）スペシャル攻撃設定")]
        [Tooltip("スペシャル攻撃のクールダウン（秒: 25秒）")]
        public float specialCooldown = 25.0f;
        [Tooltip("フィールド持続時間（秒: 10秒）")]
        public float fieldDuration = 10.0f;

        [Header("ブロック・パリー設定（全キャラ共通仕様）")]
        [Tooltip("パリー受付時間（秒）")]
        public float parryWindow = 0.25f;
        [Tooltip("パリー再使用クールダウン（秒）")]
        public float parryCooldown = 0.35f;
        [Tooltip("ガードブレイク時のスタン時間（秒）")]
        public float guardBreakDuration = 1.5f;

        // 状態フラグ
        public bool isSwinging { get; private set; } = false;
        public bool isSpecialAttacking { get; private set; } = false;
        public bool isBlocking { get; private set; } = false;
        public bool isParrying { get; private set; } = false;
        public bool isGuardBroken { get; private set; } = false;

        public override bool IsAttacking => isSwinging || isSpecialAttacking;

        private float lastParryTime = -10f;
        private float lastSpecialTime = -100f;
        private Coroutine normalAttackCoroutine;
        private Coroutine specialAttackCoroutine;
        private Coroutine parryWindowCoroutine;
        private Coroutine guardBreakCoroutine;

        // ビジュアル階層
        private Transform visualsTransform;
        private SpriteRenderer bodyRenderer;
        private Color defaultBodyColor = new Color(0.80f, 0.82f, 0.85f, 1.0f);
        private Transform shadowBladePivot;
        private GameObject shadowBladeObj;
        private SpriteRenderer shadowBladeRenderer;
        public GameObject blockShield;
        private Hitbox swordHitbox;

        private readonly Vector3 defaultBladePos = new Vector3(0.35f, 0.40f, 0f);
        private readonly Vector3 handPivot = new Vector3(0.2f, 0.2f, 0f);

        // プロシージャル生成スプライト（アセット欠損防止）
        private static Sprite cachedShadowBladeSprite;
        private static Sprite cachedCircleSprite;
        private static Sprite cachedBodySprite;
        private static Sprite cachedShadowSlashSprite;

        protected override void Start()
        {
            base.Start();

            // 1. 体（他と同じグレーの箱）のビジュアル初期化
            SetupBodyVisuals();

            // 2. 影の刃の初期化
            SetupShadowBlade();

            // 3. ブロックシールド（他キャラ完全共通仕様）の初期化
            SetupBlockShield();

            // 4. ヒットボックスの取得・初期化
            swordHitbox = GetComponentInChildren<Hitbox>(true);
            if (swordHitbox != null)
            {
                swordHitbox.damage = normalShadowDamage;
                swordHitbox.ownerPlayerID = playerID;
                swordHitbox.owner = this;
                swordHitbox.isNormalAttack = true;
                swordHitbox.postureDamageMultiplier = 2.0f; // 重厚な一撃のため体幹削りも強烈
                swordHitbox.gameObject.SetActive(false);
            }
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

            // 待機時の影の刃の妖しいゆらめき
            if (!isSwinging && !isBlocking && !isGuardBroken && shadowBladePivot != null)
            {
                float idleAngle = 20f + Mathf.Sin(Time.time * 2.5f) * 3f;
                shadowBladePivot.localRotation = Quaternion.Euler(0, 0, idleAngle);
            }
        }

        public override void Move(float direction)
        {
            // 攻撃中（通常・特殊）、またはガードブレイク・死亡・スタン中は移動を停止
            if (isSwinging || isSpecialAttacking || isGuardBroken || isDead || isStunned)
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

        public void CancelAttacks()
        {
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

            if (swordHitbox != null)
            {
                swordHitbox.gameObject.SetActive(false);
            }

            if (shadowBladePivot != null)
            {
                shadowBladePivot.localRotation = Quaternion.Euler(0, 0, 20f);
            }

            if (shadowBladeRenderer != null)
            {
                shadowBladeRenderer.color = Color.white;
            }

            isSwinging = false;
            isSpecialAttacking = false;
            SetBodyRed(false);
        }

        public override void ResetActionStates()
        {
            base.ResetActionStates();
            CancelAttacks();
            StopBlock();
        }

        // =========================================================
        // 初期化処理群
        // =========================================================

        private void SetupBodyVisuals()
        {
            visualsTransform = transform.Find("Visuals");
            if (visualsTransform == null)
            {
                GameObject visObj = new GameObject("Visuals");
                visObj.transform.SetParent(transform, false);
                visualsTransform = visObj.transform;
            }

            bodyRenderer = visualsTransform.GetComponent<SpriteRenderer>();
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponent<SpriteRenderer>();
            }
            if (bodyRenderer == null)
            {
                bodyRenderer = visualsTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            if (bodyRenderer.sprite == null)
            {
                bodyRenderer.sprite = GetDefaultBodySprite();
            }

            bodyRenderer.drawMode = SpriteDrawMode.Sliced;
            bodyRenderer.size = new Vector2(1.0f, 2.0f);
            defaultBodyColor = new Color(0.80f, 0.82f, 0.85f, 1.0f);
            bodyRenderer.color = defaultBodyColor;
            bodyRenderer.sortingOrder = 0;
        }

        /// <summary>
        /// 通常攻撃時にShadowの本体（キャラクターの箱）を赤色に変化させる
        /// </summary>
        private void SetBodyRed(bool isRed)
        {
            Color targetColor = isRed ? new Color(0.95f, 0.15f, 0.15f, 1.0f) : defaultBodyColor;
            if (bodyRenderer != null)
            {
                bodyRenderer.color = targetColor;
            }
            if (visualsTransform != null)
            {
                SpriteRenderer vsr = visualsTransform.GetComponent<SpriteRenderer>();
                if (vsr != null) vsr.color = targetColor;
            }
            SpriteRenderer rootSr = GetComponent<SpriteRenderer>();
            if (rootSr != null) rootSr.color = targetColor;
        }

        private void SetupShadowBlade()
        {
            shadowBladePivot = visualsTransform.Find("ShadowBladePivot");
            if (shadowBladePivot == null)
            {
                GameObject pivotObj = new GameObject("ShadowBladePivot");
                pivotObj.transform.SetParent(visualsTransform, false);
                pivotObj.transform.localPosition = handPivot;
                shadowBladePivot = pivotObj.transform;
            }

            Transform bladeTrans = shadowBladePivot.Find("ShadowBlade");
            if (bladeTrans == null)
            {
                shadowBladeObj = new GameObject("ShadowBlade");
                shadowBladeObj.transform.SetParent(shadowBladePivot, false);
                shadowBladeObj.transform.localPosition = defaultBladePos;
                bladeTrans = shadowBladeObj.transform;
            }
            else
            {
                shadowBladeObj = bladeTrans.gameObject;
            }

            shadowBladeRenderer = shadowBladeObj.GetComponent<SpriteRenderer>();
            if (shadowBladeRenderer == null)
            {
                shadowBladeRenderer = shadowBladeObj.AddComponent<SpriteRenderer>();
            }

            shadowBladeRenderer.sprite = GetShadowBladeSprite();
            shadowBladeRenderer.sortingOrder = 6;
            shadowBladeRenderer.color = Color.white;

            shadowBladePivot.localRotation = Quaternion.Euler(0, 0, 20f);
        }

        private void SetupBlockShield()
        {
            if (blockShield == null)
            {
                Transform existingShield = visualsTransform.Find("BlockShield");
                if (existingShield == null) existingShield = transform.Find("BlockShield");

                if (existingShield != null)
                {
                    blockShield = existingShield.gameObject;
                }
                else
                {
                    blockShield = new GameObject("BlockShield");
                    blockShield.transform.SetParent(visualsTransform, false);
                    blockShield.transform.localPosition = new Vector3(0.7f, 0f, 0f);
                    blockShield.transform.localScale = Vector3.one;

                    SpriteRenderer shieldSr = blockShield.AddComponent<SpriteRenderer>();
                    shieldSr.sprite = GetDefaultCircleSprite();
                    shieldSr.drawMode = SpriteDrawMode.Sliced;
                    shieldSr.size = new Vector2(0.8f, 2.0f);
                    shieldSr.color = new Color(0.3f, 0.8f, 1f, 0.6f); // 他キャラと同一の鮮やかな青色
                    shieldSr.sortingOrder = 10;
                }
            }

            if (blockShield != null)
            {
                blockShield.SetActive(false);
            }
        }

        // =========================================================
        // 通常攻撃: 【影絶断（2秒かけて放つダメージ35の大技 / 自傷体力-5）】
        // =========================================================

        public override void AttackNormal()
        {
            if (isDead || isSwinging || isBlocking || isGuardBroken || isDashingOrEvading || IsHurtLocked) return;

            Debug.Log("[Shadow] 影絶断 通常攻撃 発動！（2秒の渾身撃・自傷体力5消費）");
            if (animator != null) animator.SetTrigger("AttackNormal");

            if (normalAttackCoroutine != null) StopCoroutine(normalAttackCoroutine);
            normalAttackCoroutine = StartCoroutine(ShadowSlashRoutine());
        }

        private IEnumerator ShadowSlashRoutine()
        {
            isSwinging = true;
            SetBodyRed(true);

            try
            {
                // 1. 自傷ダメージ処理（発動時に自分の体力が5減る）
                ApplySelfDamage(selfHpCost);

                // 自傷により死亡した場合は即時中断
                if (isDead)
                {
                    yield break;
                }

                // 2. 予備動作（ウィンドアップフェーズ: 約1.40秒）
                // 刃を天へ高く掲げ、全身から紫黒の影のオーラを激しく凝縮（パリーを合わせやすい明確な予兆）
                float elapsed = 0f;
                Quaternion startRot = shadowBladePivot.localRotation;
                Quaternion windupRot = Quaternion.Euler(0, 0, swingStartAngle);

                while (elapsed < windupDuration)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / windupDuration);

                    // 前半（約0.8秒）で天高く構え、後半は頂点で闇を凝縮して溜める
                    float raiseProgress = Mathf.Clamp01(elapsed / (windupDuration * 0.6f));
                    float tRot = Mathf.Sin(raiseProgress * Mathf.PI * 0.5f);
                    shadowBladePivot.localRotation = Quaternion.Lerp(startRot, windupRot, tRot);

                    if (shadowBladeRenderer != null)
                    {
                        // 予備動作後半になるにつれて影の明滅速度がアップ
                        float pulseSpeed = Mathf.Lerp(6f, 16f, progress);
                        float pulse = 0.8f + Mathf.PingPong(elapsed * pulseSpeed, 0.4f);

                        // 振り下ろし直前の合図（進行度85%以上で紫紅色の鋭いフラッシュが発生し、パリーのタイミングを測りやすくする）
                        if (progress >= 0.85f)
                        {
                            shadowBladeRenderer.color = new Color(1f, 0.4f, 1f, 1f);
                        }
                        else
                        {
                            shadowBladeRenderer.color = new Color(pulse, 0.25f * pulse, pulse, 1f);
                        }
                    }
                    yield return null;
                }

                // 3. 振り下ろし一閃フェーズ: 前方地面へ闇の大弧断 (-95度) (約0.35秒)
                elapsed = 0f;
                Quaternion strikeRot = Quaternion.Euler(0, 0, swingEndAngle);

                // 攻撃判定（Hitbox）の有効化（超高威力35ダメージ！）
                if (swordHitbox != null)
                {
                    swordHitbox.damage = normalShadowDamage;
                    swordHitbox.transform.localPosition = new Vector3(1.6f, -0.1f, 0f);
                    swordHitbox.postureDamageMultiplier = 2.0f;
                    swordHitbox.isNormalAttack = true;

                    BoxCollider2D col = swordHitbox.GetComponent<BoxCollider2D>();
                    if (col != null)
                    {
                        col.size = new Vector2(2.4f, 1.6f); // 広大な闇の斬撃判定
                    }
                    StartCoroutine(ActivateHitboxTemporarily(swordHitbox.gameObject, 0.25f));
                }

                // 漆黒の影の衝撃波エフェクト
                SpawnShadowSlashArc(transform.position + new Vector3(transform.localScale.x * 1.4f, 0.0f, 0f));

                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.TriggerScreenShake(0.18f, 0.10f); // 重厚なカメラシェイク
                }

                while (elapsed < swingDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / swingDuration;
                    t = t * t; // 重く加速して地面を割るように振り抜く

                    shadowBladePivot.localRotation = Quaternion.Lerp(windupRot, strikeRot, t);

                    if (shadowBladeRenderer != null)
                    {
                        shadowBladeRenderer.color = Color.Lerp(new Color(0.9f, 0.2f, 0.9f, 1f), Color.white, t);
                    }
                    yield return null;
                }
                shadowBladePivot.localRotation = strikeRot;

                // 4. 構え戻し・反動硬直フェーズ (約0.75秒)
                elapsed = 0f;
                Quaternion readyRot = Quaternion.Euler(0, 0, 20f);
                while (elapsed < recoverDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / recoverDuration;
                    t = Mathf.Sin(t * Mathf.PI * 0.5f);

                    shadowBladePivot.localRotation = Quaternion.Lerp(strikeRot, readyRot, t);
                    yield return null;
                }

                shadowBladePivot.localRotation = readyRot;
            }
            finally
            {
                isSwinging = false;
                SetBodyRed(false);
                if (shadowBladeRenderer != null) shadowBladeRenderer.color = Color.white;
            }
        }

        /// <summary>
        /// 影の攻撃の代償として自身の体力を消費
        /// </summary>
        private void ApplySelfDamage(int amount)
        {
            currentHP -= amount;
            if (currentHP < 0) currentHP = 0;

            Debug.Log($"[Shadow] 攻撃代償で体力消費: -{amount}（残りHP: {currentHP}）");

            // HUDへの被弾・HP更新通知
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.RegisterHit(playerID, amount, transform.position + Vector3.up * 0.5f);
            }

            // 自身の赤黒い被弾フラッシュ演出
            StartCoroutine(SelfDamageFlashRoutine());

            if (currentHP == 0)
            {
                // 自傷によりHPが尽きた場合の死亡処理
                DieDirectly();
            }
        }

        private IEnumerator SelfDamageFlashRoutine()
        {
            if (visualsTransform == null || isSwinging) yield break;
            SpriteRenderer bodySr = visualsTransform.GetComponent<SpriteRenderer>();
            if (bodySr == null) yield break;

            Color orig = defaultBodyColor;
            bodySr.color = new Color(0.6f, 0.1f, 0.2f, 1f); // 禍々しい赤黒フラッシュ
            yield return new WaitForSeconds(0.12f);
            if (bodySr != null && !isSwinging) bodySr.color = orig;
        }

        private void DieDirectly()
        {
            isDead = true;
            isSwinging = false;
            SetBodyRed(false);
            remainingLives--;
            SafeSetBool("IsDead", true);
            SafeSetTrigger("Damage");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCharacterDied(playerID);
            }
        }

        // =========================================================
        // クールダウン情報取得（HUD表示用）
        // =========================================================

        public override float GetCooldownRemaining()
        {
            float elapsed = Time.time - lastSpecialTime;
            if (elapsed < specialCooldown)
            {
                return specialCooldown - elapsed;
            }
            return 0f;
        }

        public override float GetCooldownDuration()
        {
            return specialCooldown;
        }

        public override string GetCooldownStatusText()
        {
            if (IsSpecialSilenced())
            {
                return "<color=#cc44ff>封印中</color>";
            }

            float remaining = GetCooldownRemaining();
            if (remaining > 0.05f)
            {
                return $"<color=#ffaa00>{remaining:F1}s</color>";
            }
            return "<color=#00ff88>READY</color>";
        }

        public override bool IsInParryWindow() => isParrying;
        public override bool IsInGuardBreak() => isGuardBroken;
        public override float GetParryCooldownRemaining()
        {
            float elapsed = Time.time - lastParryTime;
            if (elapsed < parryCooldown) return parryCooldown - elapsed;
            return 0f;
        }

        // =========================================================
        // スペシャル攻撃: 【影の領域（沈黙フィールド展開 / 10秒持続 / 25秒クールダウン）】
        // 自分を中心にステージ半分の広さのフィールドを展開し、
        // 自身を含め領域内のすべてのキャラクターのスペシャル攻撃を封印する。
        // =========================================================

        public override void AttackSpecial()
        {
            if (isDead || isSwinging || isSpecialAttacking || isBlocking || isGuardBroken || isDashingOrEvading || IsHurtLocked || isStunned) return;

            // 1. クールダウン判定（25秒）
            float elapsedSinceSpecial = Time.time - lastSpecialTime;
            if (elapsedSinceSpecial < specialCooldown)
            {
                float remain = specialCooldown - elapsedSinceSpecial;
                Debug.Log($"[Shadow] スペシャル攻撃はクールダウン中です！（残り {remain:F1} 秒）");
                return;
            }

            // 2. 自身が沈黙フィールド内にいる場合はスペシャル使用不可（自身含め全キャラ使用不可のルール）
            if (IsSpecialSilenced())
            {
                Debug.Log("[Shadow] 自身も影の領域（沈黙フィールド）内にいるため、スペシャル攻撃を使用できません！");
                return;
            }

            lastSpecialTime = Time.time;
            Debug.Log("[Shadow] スペシャル攻撃【影の領域】発動！ ステージ半分の広さの沈黙フィールドを展開（クールダウン25秒）！");

            if (animator != null) animator.SetTrigger("AttackSpecial");

            if (specialAttackCoroutine != null) StopCoroutine(specialAttackCoroutine);
            specialAttackCoroutine = StartCoroutine(ShadowDomainCastRoutine());
        }

        private IEnumerator ShadowDomainCastRoutine()
        {
            isSpecialAttacking = true;

            // キャスト演出: 闇の刃を天へ掲げ、地面へ一気に突き立てて影の領域を解き放つ
            Quaternion readyRot = shadowBladePivot != null ? shadowBladePivot.localRotation : Quaternion.Euler(0, 0, 20f);
            Quaternion raiseRot = Quaternion.Euler(0, 0, 85f);
            Quaternion slamRot = Quaternion.Euler(0, 0, -85f);

            // 1. 掲げ（0.18秒）
            float elapsed = 0f;
            float raiseTime = 0.18f;
            while (elapsed < raiseTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / raiseTime;
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                if (shadowBladePivot != null)
                {
                    shadowBladePivot.localRotation = Quaternion.Lerp(readyRot, raiseRot, t);
                }
                if (shadowBladeRenderer != null)
                {
                    shadowBladeRenderer.color = Color.Lerp(Color.white, new Color(0.9f, 0.3f, 1f, 1f), t);
                }
                yield return null;
            }

            // 2. 地面へ突き立て（0.12秒）
            elapsed = 0f;
            float slamTime = 0.12f;
            while (elapsed < slamTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slamTime;
                t = t * t;
                if (shadowBladePivot != null)
                {
                    shadowBladePivot.localRotation = Quaternion.Lerp(raiseRot, slamRot, t);
                }
                yield return null;
            }
            if (shadowBladePivot != null) shadowBladePivot.localRotation = slamRot;

            // 3. 【影の領域（沈黙フィールド）】を自分を中心に生成！
            // 自分を中心にステージ半分（幅12.5f）のフィールドを生成し、10秒間持続
            ShadowDomainField.Spawn(transform.position, fieldDuration);

            // 漆黒の衝撃波エフェクトを発生
            SpawnShadowSlashArc(transform.position + new Vector3(0f, 0.2f, 0f));

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.25f, 0.12f);
            }

            // 突き刺し硬直（0.25秒）
            yield return new WaitForSeconds(0.25f);

            // 4. 構え戻し（0.20秒）
            elapsed = 0f;
            float recoverTime = 0.20f;
            while (elapsed < recoverTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / recoverTime;
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                if (shadowBladePivot != null)
                {
                    shadowBladePivot.localRotation = Quaternion.Lerp(slamRot, readyRot, t);
                }
                if (shadowBladeRenderer != null)
                {
                    shadowBladeRenderer.color = Color.Lerp(new Color(0.9f, 0.3f, 1f, 1f), Color.white, t);
                }
                yield return null;
            }

            if (shadowBladePivot != null) shadowBladePivot.localRotation = readyRot;
            if (shadowBladeRenderer != null) shadowBladeRenderer.color = Color.white;

            isSpecialAttacking = false;
        }

        // =========================================================
        // ブロック＆パリー処理（全キャラクター完全共通仕様）
        // =========================================================

        public override void StartBlock()
        {
            if (isDead || isSwinging || isGuardBroken || isStunned || isDashingOrEvading) return;

            // DeepWoken Block Spam Prevention: パリークールダウン判定
            if (Time.time - lastParryTime < parryCooldown)
            {
                isParrying = false;
                isBlocking = false;
                isBlockingState = false;
                Debug.Log("[Shadow] Block start ignored (Parry on Cooldown - Shield disabled).");
                return;
            }

            isBlocking = true;
            isBlockingState = true;
            isParrying = true;
            lastParryTime = Time.time;
            Debug.Log("[Shadow] Block start (Parry Window Active).");

            if (parryWindowCoroutine != null) StopCoroutine(parryWindowCoroutine);
            parryWindowCoroutine = StartCoroutine(CloseParryWindowRoutine());

            if (blockShield != null)
            {
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
                shieldSr.size = new Vector2(0.8f, 2.0f);
                // パリー中は鮮やかな青、通常ブロックは落ち着いた青（他キャラ同一）
                shieldSr.color = isParrying ? new Color(0.3f, 0.8f, 1f, 0.6f) : new Color(0.3f, 0.5f, 0.7f, 0.45f);
                shieldSr.sortingOrder = 10;

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
            Debug.Log("[Shadow] Block stop.");

            if (parryWindowCoroutine != null)
            {
                StopCoroutine(parryWindowCoroutine);
                parryWindowCoroutine = null;
            }

            if (blockShield != null)
            {
                blockShield.SetActive(false);
            }
        }

        private IEnumerator CloseParryWindowRoutine()
        {
            yield return new WaitForSeconds(parryWindow);
            if (isBlocking)
            {
                isParrying = false;
                Debug.Log("[Shadow] Parry window closed.");

                if (blockShield != null)
                {
                    SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                    if (shieldSr != null)
                    {
                        shieldSr.color = new Color(0.3f, 0.5f, 0.7f, 0.45f);
                    }
                }
            }
        }

        // =========================================================
        // 被ダメージ＆パリー判定（全キャラクター完全共通仕様）
        // =========================================================

        public override void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isDead || isInvincible) return;

            // 背後からの攻撃か判定（めくり判定）
            CharacterBase opponent = attackerHitbox != null ? attackerHitbox.owner : null;
            if (opponent == null)
            {
                CharacterBase[] allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
                foreach (CharacterBase c in allChars)
                {
                    if (c != this && !c.isDead) { opponent = c; break; }
                }
            }

            bool isBackAttack = false;
            if (opponent != null)
            {
                float faceDir = transform.localScale.x;
                float relativeX = opponent.transform.position.x - transform.position.x;
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
                    Debug.Log("★ [Shadow] PARRY SUCCESS! (DeepWoken Style) ★");
                    AudioManager.PlayParrySound();

                    lastParryTime = -10f;
                    currentPosture = Mathf.Max(0f, currentPosture - damage);
                    AddMana(6f);

                    if (opponent != null)
                    {
                        opponent.Stun(0.6f);
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
                    Debug.Log("[Shadow] Block success. Posture accumulating.");
                    AudioManager.PlayBlockSound();

                    float postureMultiplier = attackerHitbox != null ? attackerHitbox.postureDamageMultiplier : 1.5f;
                    currentPosture += damage * postureMultiplier;

                    if (currentPosture >= maxPosture)
                    {
                        StartCoroutine(GuardBreakRoutine());
                    }
                    else
                    {
                        if (blockShield != null)
                        {
                            StartCoroutine(ShieldImpactFlashRoutine());
                        }
                    }
                }
                return;
            }

            CancelAttacks();
            base.TakeDamage(damage, attackerHitbox);
        }

        private IEnumerator ParrySuccessRoutine()
        {
            if (blockShield != null)
            {
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr != null)
                {
                    shieldSr.color = Color.white;
                }

                yield return new WaitForSeconds(0.12f);

                if (shieldSr != null && isBlocking)
                {
                    shieldSr.color = new Color(0.3f, 0.8f, 1f, 0.8f);
                }
            }
        }

        private IEnumerator ShieldImpactFlashRoutine()
        {
            if (blockShield == null) yield break;
            SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
            if (shieldSr == null) yield break;

            Color currentCol = shieldSr.color;
            shieldSr.color = new Color(1f, 1f, 1f, 0.8f);
            yield return new WaitForSeconds(0.08f);
            if (isBlocking && shieldSr != null)
            {
                shieldSr.color = currentCol;
            }
        }

        private IEnumerator GuardBreakRoutine()
        {
            isGuardBroken = true;
            isBlocking = false;
            isBlockingState = false;
            isParrying = false;

            if (blockShield != null)
            {
                blockShield.SetActive(false);
            }

            currentPosture = maxPosture;

            Debug.Log("[Shadow] ★ GUARD BROKEN! Stunned for 1.5s ★");
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.35f, 0.15f);
            }

            Stun(guardBreakDuration);

            float elapsed = 0f;
            while (elapsed < guardBreakDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            currentPosture = 0f;
            isGuardBroken = false;
            Debug.Log("[Shadow] Guard break recovered.");
        }

        // =========================================================
        // エフェクト演出
        // =========================================================

        private void SpawnShadowSlashArc(Vector3 position)
        {
            GameObject arc = new GameObject("ShadowSlashArc");
            arc.transform.position = position;
            arc.transform.localScale = new Vector3(2.0f, 1.8f, 1f);

            SpriteRenderer sr = arc.AddComponent<SpriteRenderer>();
            sr.sprite = GetShadowSlashSprite();
            sr.color = new Color(0.7f, 0.15f, 0.9f, 0.95f); // 禍々しい深紫の闇刃
            sr.sortingOrder = 7;

            StartCoroutine(FadeAndScaleArc(arc, 0.28f));
        }

        private IEnumerator FadeAndScaleArc(GameObject arc, float duration)
        {
            float elapsed = 0f;
            SpriteRenderer sr = arc.GetComponent<SpriteRenderer>();
            Vector3 origScale = arc.transform.localScale;

            while (elapsed < duration && arc != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (sr != null)
                {
                    sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, (1f - t) * 0.95f);
                }
                arc.transform.localScale = origScale * (1f + t * 0.5f);
                yield return null;
            }

            if (arc != null) Destroy(arc);
        }

        // =========================================================
        // プロシージャルスプライト生成
        // =========================================================

        public static Sprite GetShadowBladeSprite()
        {
            if (cachedShadowBladeSprite != null) return cachedShadowBladeSprite;

            int w = 24, h = 80;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color empty = Color.clear;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, empty);
                }
            }

            // 柄頭（Pommel: 深紅のダークオーブ）
            for (int y = 2; y <= 6; y++)
            {
                for (int x = 10; x <= 13; x++)
                {
                    tex.SetPixel(x, y, new Color(0.6f, 0.05f, 0.15f, 1f));
                }
            }

            // 柄（Grip: 漆黒のレザー巻き）
            for (int y = 7; y <= 18; y++)
            {
                for (int x = 11; x <= 12; x++)
                {
                    bool rib = (y % 3 == 0);
                    Color gripCol = rib ? new Color(0.3f, 0.15f, 0.35f, 1f) : new Color(0.08f, 0.05f, 0.12f, 1f);
                    tex.SetPixel(x, y, gripCol);
                }
            }

            // 鍔（Crossguard: 禍々しい影の角・トゲ）
            for (int y = 19; y <= 22; y++)
            {
                for (int x = 4; x <= 19; x++)
                {
                    bool isSpike = (x <= 6 || x >= 17 || y == 22);
                    Color guardCol = isSpike ? new Color(0.8f, 0.2f, 0.9f, 1f) : new Color(0.2f, 0.08f, 0.28f, 1f);
                    tex.SetPixel(x, y, guardCol);
                }
            }

            // 刀身（Shadow Blade: 漆黒の芯と紫紅の影オーラ刃）
            Color coreDark = new Color(0.08f, 0.04f, 0.12f, 1.0f);   // 漆黒の刀身コア
            Color shadowAura = new Color(0.55f, 0.12f, 0.70f, 0.9f); // 闇の紫刃
            Color cursedEdge = new Color(0.95f, 0.25f, 0.45f, 0.95f); // 妖しい真紅のエッジ

            for (int y = 23; y < 76; y++)
            {
                float progress = (float)(y - 23) / (76 - 23);
                int halfWidth = Mathf.RoundToInt(Mathf.Lerp(5f, 1f, progress));

                int left = 12 - halfWidth;
                int right = 11 + halfWidth;

                for (int x = left; x <= right; x++)
                {
                    bool isBorder = (x == left || x == right);
                    bool isCenter = (x == 11 || x == 12);

                    if (isCenter)
                    {
                        tex.SetPixel(x, y, coreDark);
                    }
                    else if (isBorder)
                    {
                        tex.SetPixel(x, y, cursedEdge);
                    }
                    else
                    {
                        tex.SetPixel(x, y, shadowAura);
                    }
                }
            }

            // 切っ先（Sharp Tip）
            tex.SetPixel(11, 76, coreDark);
            tex.SetPixel(12, 76, coreDark);
            tex.SetPixel(11, 77, cursedEdge);
            tex.SetPixel(12, 77, cursedEdge);
            tex.SetPixel(11, 78, Color.magenta);

            tex.Apply();
            cachedShadowBladeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.15f), 32f);
            return cachedShadowBladeSprite;
        }

        private static Sprite GetShadowSlashSprite()
        {
            if (cachedShadowSlashSprite != null) return cachedShadowSlashSprite;

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color violet = new Color(0.7f, 0.15f, 0.95f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(8, 8));
                    if (dist >= 26f && dist <= 46f && x >= y - 10 && x <= y + 38)
                    {
                        float mid = 36f;
                        float diff = Mathf.Abs(dist - mid) / 10f;
                        float alpha = Mathf.Clamp01(1f - diff);
                        tex.SetPixel(x, y, new Color(violet.r, violet.g, violet.b, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            cachedShadowSlashSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return cachedShadowSlashSprite;
        }

        private static Sprite GetDefaultCircleSprite()
        {
            if (cachedCircleSprite != null) return cachedCircleSprite;

#if UNITY_EDITOR
            cachedCircleSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (cachedCircleSprite != null) return cachedCircleSprite;
#endif
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        float alpha = Mathf.Clamp01((radius - dist) + 0.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return cachedCircleSprite;
        }

        private static Sprite GetDefaultBodySprite()
        {
            if (cachedBodySprite != null) return cachedBodySprite;

#if UNITY_EDITOR
            cachedBodySprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (cachedBodySprite != null) return cachedBodySprite;
#endif
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            cachedBodySprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
            return cachedBodySprite;
        }
    }
}
