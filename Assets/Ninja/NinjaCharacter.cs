using System.Collections;
using UnityEngine;

namespace FightingGameBase
{
    /// <summary>
    /// Ninja (忍者) キャラクターコントローラー。
    /// CharacterBaseを継承し、通常攻撃で「特大青玉スラム」、スペシャル攻撃で「黄色いクナイ（飛雷神・瞬間移動）」を行います。
    /// 
    /// 【通常攻撃】
    /// - 1回目入力: 青い球を前方にチャージ（1.5秒で特大サイズ・最大威力25に成長）。
    /// - 2回目入力: 背後へ大きく溜めた後、パリー予兆閃光＆告知音（ピキィィン！）を経て前方地面へドォン！！と叩きつける。
    ///   ダメージ判定は振り下ろして地面に着弾した瞬間のみ発生し、予兆に反応してパリーしやすい設計。
    /// 
    /// 【スペシャル攻撃（黄色いクナイ / 厳格な3段階順序 1入力＝1アクション）】
    /// 1. 「投げる」（1回目入力）: 前方へ黄色いクナイを水平に投擲。敵に当たれば10ダメージ、パリーされれば消滅。
    /// 2. 「設置」（2回目入力）: 飛行中のクナイがその場で真下へ急速落下し、地面にグサッと突き刺さって設置（Placed）される。
    /// 3. 「瞬間移動」（3回目入力）: 地面に設置されたクナイへ、黄色い閃光とともに忍者が瞬間移動する。
    ///    ★クナイがすでに設置されていれば、通常攻撃の発動中（チャージ中・スラム中）でもいつでも瞬間移動可能！
    ///    ★同時に複数のステップを進めたり、通常攻撃と同時に投げたりする複合マクロは一切排除し、1回の入力で1つのステップのみを確実に実行。
    /// </summary>
    public class NinjaCharacter : CharacterBase
    {
        [Header("=== Ninja Normal Attack (Blue Orb Slam) ===")]
        [SerializeField] private int normalAttackDamage = 25;       // 通常攻撃威力
        [SerializeField] private float chargeDuration = 1.5f;       // チャージ完了時間（1.5秒）
        [SerializeField] private float minOrbScale = 0.5f;          // チャージ初期サイズ
        [SerializeField] private float maxOrbScale = 2.5f;          // チャージ最大サイズ（通常球の約2倍の特大サイズ）
        [SerializeField] private float slamDistance = 1.6f;         // 前方への叩きつけ着弾距離
        [SerializeField] private float slamDuration = 3.0f;         // スラム全体の所要時間（約3.0秒）
        [SerializeField] private bool allowEarlyRelease = true;     // 1.5秒未満でも再入力で即時スラム可能

        [Header("=== Ninja Special Attack (Yellow Kunai) ===")]
        [SerializeField] private int kunaiDamage = 10;              // クナイのダメージ（10）
        [SerializeField] private float kunaiFlySpeed = 16.0f;       // クナイの飛行速度

        [Header("=== Parry & Block Settings ===")]
        [SerializeField] private float parryWindow = 0.25f;         // パリー受付時間（0.25秒）
        [SerializeField] private float parryCooldown = 0.6f;        // パリー失敗時のクールダウン
        [SerializeField] private float guardBreakDuration = 2.5f;   // ガードブレイク気絶時間

        [Header("=== Component References ===")]
        [SerializeField] private GameObject blueOrbObj;
        [SerializeField] private Hitbox blueOrbHitbox;
        [SerializeField] private Collider2D blueOrbCollider;
        [SerializeField] private SpriteRenderer blueOrbMainRenderer;
        [SerializeField] private SpriteRenderer blueOrbCoreRenderer;
        [SerializeField] private SpriteRenderer blueOrbAuraRenderer;
        [SerializeField] private GameObject blockShield;
        [SerializeField] private Transform visualsTransform;

        // 状態フラグ
        public bool isCharging { get; private set; } = false;
        public bool isSlamming { get; private set; } = false;
        public float currentChargeTimer { get; private set; } = 0f;
        public bool isChargeMax => currentChargeTimer >= chargeDuration;

        // クナイ状態
        private NinjaYellowKunai activeKunai;
        private float lastKunaiActionTime = -10f;
        private const float KunaiActionInterval = 0.20f; // ステップ間の確実な受付インターバル（二重入力・連打暴発防止）

        // 攻撃中フラグ: 外部AIや判定用
        public override bool IsAttacking => isSlamming || (customSpecialSkill != null && customSpecialSkill.IsPerformingSpecial);

        // ★通常攻撃中でもスペシャル入力を許可するのは「クナイが設置完了している時（瞬間移動可能時）」のみ！
        public override bool CanInputSpecialDuringAttack =>
            (customSpecialSkill != null && customSpecialSkill.IsActive)
                ? customSpecialSkill.CanInputSpecialDuringAttack
                : (activeKunai != null && activeKunai.CurrentState == NinjaYellowKunai.KunaiState.Placed);

        public bool isBlocking { get; private set; } = false;
        public bool isParrying { get; private set; } = false;
        public bool isGuardBroken { get; private set; } = false;

        public override bool IsInParryWindow() => isParrying;
        public override bool IsInGuardBreak() => isGuardBroken;
        public override float GetParryCooldownRemaining() => Mathf.Max(0f, parryCooldown - (Time.time - lastParryTime));

        private float lastParryTime = -10f;
        private Vector3 defaultOrbLocalPos = new Vector3(0.95f, 0.15f, 0f);

        private Coroutine chargeCoroutine;
        private Coroutine slamCoroutine;
        private Coroutine parryWindowCoroutine;

        // 静的サークルテクスチャキャッシュ
        private static Sprite cachedCircleSprite;

        protected override void Start()
        {
            base.Start();
            SetupComponentsIfNeeded();
        }

        private void SetupComponentsIfNeeded()
        {
            if (visualsTransform == null)
            {
                Transform v = transform.Find("Visuals");
                visualsTransform = v != null ? v : transform;
            }

            if (blueOrbObj == null)
            {
                Transform t = transform.Find("BlueOrb");
                if (t != null) blueOrbObj = t.gameObject;
            }

            if (blockShield == null)
            {
                Transform t = transform.Find("BlockShield");
                if (t != null) blockShield = t.gameObject;
            }

            if (blockShield == null)
            {
                SetupBlockShield();
            }

            if (blueOrbObj == null)
            {
                SetupBlueOrb();
            }
            else
            {
                CacheOrbComponents();
            }
        }

        private void SetupBlockShield()
        {
            blockShield = new GameObject("BlockShield");
            blockShield.transform.SetParent(transform, false);
            blockShield.transform.localPosition = new Vector3(0.55f, 0f, 0f);
            blockShield.transform.localScale = new Vector3(0.35f, 1.8f, 1f);

            SpriteRenderer sr = blockShield.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateCircleSprite();
            sr.color = new Color(0.3f, 0.8f, 1f, 0.75f);
            sr.sortingOrder = 5;

            blockShield.SetActive(false);
        }

        private void SetupBlueOrb()
        {
            blueOrbObj = new GameObject("BlueOrb");
            blueOrbObj.transform.SetParent(transform, false);
            blueOrbObj.transform.localPosition = defaultOrbLocalPos;

            // 多層構造の発光オーブ
            // 1. 外層 (Aura)
            GameObject auraObj = new GameObject("OrbAura");
            auraObj.transform.SetParent(blueOrbObj.transform, false);
            blueOrbAuraRenderer = auraObj.AddComponent<SpriteRenderer>();
            blueOrbAuraRenderer.sprite = GetOrCreateCircleSprite();
            blueOrbAuraRenderer.color = new Color(0.0f, 0.35f, 1.0f, 0.45f);
            blueOrbAuraRenderer.sortingOrder = 8;
            auraObj.transform.localScale = Vector3.one * 1.35f;

            // 2. 主体 (Main Sphere)
            GameObject mainObj = new GameObject("OrbMain");
            mainObj.transform.SetParent(blueOrbObj.transform, false);
            blueOrbMainRenderer = mainObj.AddComponent<SpriteRenderer>();
            blueOrbMainRenderer.sprite = GetOrCreateCircleSprite();
            blueOrbMainRenderer.color = new Color(0.05f, 0.65f, 1.0f, 0.90f);
            blueOrbMainRenderer.sortingOrder = 9;
            mainObj.transform.localScale = Vector3.one * 1.0f;

            // 3. 中心核 (Core / White-Cyan glow)
            GameObject coreObj = new GameObject("OrbCore");
            coreObj.transform.SetParent(blueOrbObj.transform, false);
            blueOrbCoreRenderer = coreObj.AddComponent<SpriteRenderer>();
            blueOrbCoreRenderer.sprite = GetOrCreateCircleSprite();
            blueOrbCoreRenderer.color = new Color(0.88f, 0.98f, 1.0f, 0.95f);
            blueOrbCoreRenderer.sortingOrder = 10;
            coreObj.transform.localScale = Vector3.one * 0.55f;

            // 4. 当たり判定（Hitbox & Trigger Collider）
            CircleCollider2D circleCol = blueOrbObj.AddComponent<CircleCollider2D>();
            circleCol.isTrigger = true;
            circleCol.radius = 0.55f;
            circleCol.enabled = false;
            blueOrbCollider = circleCol;

            blueOrbHitbox = blueOrbObj.AddComponent<Hitbox>();
            blueOrbHitbox.damage = normalAttackDamage;
            blueOrbHitbox.ownerPlayerID = playerID;
            blueOrbHitbox.owner = this;
            blueOrbHitbox.isNormalAttack = true;
            blueOrbHitbox.postureDamageMultiplier = 1.5f;

            blueOrbObj.transform.localScale = Vector3.one * minOrbScale;
            blueOrbObj.SetActive(false);
        }

        private void CacheOrbComponents()
        {
            if (blueOrbObj == null) return;

            if (blueOrbCollider == null) blueOrbCollider = blueOrbObj.GetComponent<Collider2D>();
            if (blueOrbHitbox == null) blueOrbHitbox = blueOrbObj.GetComponent<Hitbox>();

            Transform auraT = blueOrbObj.transform.Find("OrbAura");
            if (auraT != null) blueOrbAuraRenderer = auraT.GetComponent<SpriteRenderer>();

            Transform mainT = blueOrbObj.transform.Find("OrbMain");
            if (mainT != null) blueOrbMainRenderer = mainT.GetComponent<SpriteRenderer>();

            Transform coreT = blueOrbObj.transform.Find("OrbCore");
            if (coreT != null) blueOrbCoreRenderer = coreT.GetComponent<SpriteRenderer>();
        }

        // =========================================================================
        // 通常攻撃ロジック
        // =========================================================================
        public override void AttackNormal()
        {
            if (isDead || isSlamming || isGuardBroken || isStunned) return;

            if (!isCharging)
            {
                StartCharge();
            }
            else
            {
                ReleaseSlam();
            }
        }

        private void StartCharge()
        {
            isCharging = true;
            currentChargeTimer = 0f;

            if (blueOrbObj == null) SetupBlueOrb();

            if (chargeCoroutine != null) StopCoroutine(chargeCoroutine);
            chargeCoroutine = StartCoroutine(ChargeRoutine());

            Debug.Log("[Ninja] Normal attack charge start");
        }

        private IEnumerator ChargeRoutine()
        {
            if (blueOrbObj != null)
            {
                blueOrbObj.transform.localPosition = defaultOrbLocalPos;
                blueOrbObj.transform.localRotation = Quaternion.identity;
                blueOrbObj.transform.localScale = Vector3.one * minOrbScale;
                blueOrbObj.SetActive(true);
            }
            if (blueOrbCollider != null) blueOrbCollider.enabled = false;

            bool playedMaxEffect = false;

            while (isCharging)
            {
                currentChargeTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(currentChargeTimer / chargeDuration);

                float currentScale = Mathf.Lerp(minOrbScale, maxOrbScale, progress);

                if (blueOrbObj != null)
                {
                    float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 16f);
                    blueOrbObj.transform.localScale = Vector3.one * (currentScale * pulse);

                    blueOrbObj.transform.Rotate(0f, 0f, 220f * Time.deltaTime);

                    if (blueOrbMainRenderer != null)
                    {
                        blueOrbMainRenderer.color = Color.Lerp(
                            new Color(0.1f, 0.5f, 1.0f, 0.85f),
                            new Color(0.0f, 0.85f, 1.0f, 0.98f),
                            progress
                        );
                    }
                    if (blueOrbCoreRenderer != null)
                    {
                        blueOrbCoreRenderer.color = Color.Lerp(
                            new Color(0.7f, 0.9f, 1.0f, 0.85f),
                            Color.white,
                            progress
                        );
                    }
                }

                if (progress >= 1f && !playedMaxEffect)
                {
                    playedMaxEffect = true;
                    Debug.Log("[Ninja] Max charge complete");
                    if (HUDManager.Instance != null)
                    {
                        HUDManager.Instance.TriggerScreenShake(0.12f, 0.05f);
                    }
                }

                yield return null;
            }
        }

        private void ReleaseSlam()
        {
            isCharging = false;
            if (chargeCoroutine != null)
            {
                StopCoroutine(chargeCoroutine);
                chargeCoroutine = null;
            }

            if (slamCoroutine != null) StopCoroutine(slamCoroutine);
            slamCoroutine = StartCoroutine(SlamRoutine());
        }

        // =========================================================================
        // 【スラム攻撃コルーチン（全体速度 約3.0秒）】
        // =========================================================================
        private IEnumerator SlamRoutine()
        {
            isSlamming = true;

            if (blueOrbObj == null) SetupBlueOrb();
            if (blueOrbObj != null) blueOrbObj.SetActive(true);
            if (blueOrbCollider != null) blueOrbCollider.enabled = false;

            int finalDamage = normalAttackDamage;
            if (allowEarlyRelease && currentChargeTimer < chargeDuration)
            {
                float ratio = Mathf.Clamp01(currentChargeTimer / chargeDuration);
                finalDamage = Mathf.RoundToInt(Mathf.Lerp(12f, normalAttackDamage, ratio));
            }

            if (blueOrbHitbox != null)
            {
                blueOrbHitbox.damage = finalDamage;
                blueOrbHitbox.ownerPlayerID = playerID;
                blueOrbHitbox.owner = this;
                blueOrbHitbox.isNormalAttack = true;
                blueOrbHitbox.postureDamageMultiplier = 1.5f;
            }

            float chargeProgress = Mathf.Clamp01(currentChargeTimer / chargeDuration);
            float baseScale = Mathf.Lerp(minOrbScale, maxOrbScale, chargeProgress);
            if (blueOrbObj != null)
            {
                blueOrbObj.transform.localScale = Vector3.one * baseScale;
            }

            Vector3 readyPos = defaultOrbLocalPos;
            Vector3 backWindupPos = new Vector3(-1.45f, 1.15f, 0f);
            Vector3 slamHitPos = new Vector3(slamDistance, 0.05f, 0f);
            Quaternion origVisualRot = visualsTransform != null ? visualsTransform.localRotation : Quaternion.identity;

            float windupTime = slamDuration * 0.50f; // 1.50s
            float elapsed = 0f;
            bool warningTriggered = false;

            Debug.Log("[Ninja] Slam windup start");

            while (elapsed < windupTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / windupTime);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                Vector3 arc = Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.55f);
                if (blueOrbObj != null)
                {
                    blueOrbObj.transform.localPosition = Vector3.Lerp(readyPos, backWindupPos, ease) + arc;
                    float spinSpeed = Mathf.Lerp(240f, 600f, t);
                    blueOrbObj.transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

                    float chargePulse = 1f + 0.08f * Mathf.Sin(Time.time * 20f);
                    blueOrbObj.transform.localScale = Vector3.one * (baseScale * chargePulse);
                }

                if (visualsTransform != null)
                {
                    visualsTransform.localRotation = Quaternion.Euler(0f, 0f, -10f * ease);
                }

                if (t >= 0.88f && !warningTriggered)
                {
                    warningTriggered = true;
                    TriggerParryWarning(baseScale);
                }

                yield return null;
            }

            float strikeTime = slamDuration * 0.12f; // 0.36s
            elapsed = 0f;

            if (rb != null)
            {
                float facing = transform.localScale.x >= 0 ? 1f : -1f;
                rb.linearVelocity = new Vector2(facing * 7.5f, rb.linearVelocity.y);
            }

            while (elapsed < strikeTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / strikeTime);
                float ease = t * t;

                Vector3 strikeArc = Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.95f);
                if (blueOrbObj != null)
                {
                    blueOrbObj.transform.localPosition = Vector3.Lerp(backWindupPos, slamHitPos, ease) + strikeArc;
                    blueOrbObj.transform.Rotate(0f, 0f, 720f * Time.deltaTime);
                }

                if (visualsTransform != null)
                {
                    visualsTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-10f, 16f, t));
                }

                if (blueOrbCollider != null) blueOrbCollider.enabled = false;

                yield return null;
            }

            if (blueOrbObj != null)
            {
                blueOrbObj.transform.localPosition = slamHitPos;
                blueOrbObj.transform.localScale = new Vector3(baseScale * 1.55f, baseScale * 0.85f, 1f);
            }

            if (blueOrbCollider != null) blueOrbCollider.enabled = true;
            if (blueOrbHitbox != null) blueOrbHitbox.CheckOverlap();

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.35f, 0.20f);
            }

            Debug.Log("[Ninja] Slam impact");

            float activeHitDuration = 0.14f;
            float activeElapsed = 0f;
            while (activeElapsed < activeHitDuration)
            {
                activeElapsed += Time.deltaTime;
                if (blueOrbHitbox != null) blueOrbHitbox.CheckOverlap();
                yield return null;
            }

            if (blueOrbCollider != null) blueOrbCollider.enabled = false;

            float holdTime = (slamDuration * 0.18f) - activeHitDuration;
            if (holdTime > 0f)
            {
                yield return new WaitForSeconds(holdTime);
            }

            float fadeTime = slamDuration * 0.20f;
            elapsed = 0f;
            Vector3 impactScale = blueOrbObj != null ? blueOrbObj.transform.localScale : Vector3.one * baseScale;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeTime);
                if (blueOrbObj != null)
                {
                    blueOrbObj.transform.localScale = Vector3.Lerp(impactScale, Vector3.zero, t);
                }
                if (visualsTransform != null)
                {
                    visualsTransform.localRotation = Quaternion.Lerp(Quaternion.Euler(0f, 0f, 16f), origVisualRot, t);
                }
                yield return null;
            }

            if (visualsTransform != null) visualsTransform.localRotation = origVisualRot;

            if (blueOrbObj != null)
            {
                blueOrbObj.SetActive(false);
                blueOrbObj.transform.localPosition = defaultOrbLocalPos;
                blueOrbObj.transform.localScale = Vector3.one * minOrbScale;
            }

            currentChargeTimer = 0f;
            isSlamming = false;
            slamCoroutine = null;
        }

        private void TriggerParryWarning(float baseScale)
        {
            AudioManager.PlayWarningSound();
            StartCoroutine(ParryWarningFlashRoutine(baseScale));
        }

        private IEnumerator ParryWarningFlashRoutine(float baseScale)
        {
            if (blueOrbCoreRenderer != null) blueOrbCoreRenderer.color = Color.white;
            if (blueOrbMainRenderer != null) blueOrbMainRenderer.color = new Color(0.9f, 0.98f, 1.0f, 1.0f);
            if (blueOrbAuraRenderer != null) blueOrbAuraRenderer.color = new Color(0.4f, 0.85f, 1.0f, 0.95f);

            GameObject ringObj = new GameObject("WarningRing");
            ringObj.transform.SetParent(blueOrbObj != null ? blueOrbObj.transform : transform, false);
            ringObj.transform.localPosition = Vector3.zero;
            SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
            ringSr.sprite = GetOrCreateCircleSprite();
            ringSr.color = new Color(1.0f, 1.0f, 0.85f, 0.85f);
            ringSr.sortingOrder = 12;

            float ringDuration = 0.22f;
            float rElapsed = 0f;
            while (rElapsed < ringDuration)
            {
                rElapsed += Time.deltaTime;
                float rt = Mathf.Clamp01(rElapsed / ringDuration);
                if (ringObj != null)
                {
                    ringObj.transform.localScale = Vector3.one * Mathf.Lerp(1.0f, 2.4f, rt);
                    ringSr.color = new Color(1.0f, 1.0f, 0.85f, Mathf.Lerp(0.85f, 0f, rt));
                }
                yield return null;
            }

            if (ringObj != null) Destroy(ringObj);

            if (blueOrbCoreRenderer != null) blueOrbCoreRenderer.color = new Color(0.88f, 0.98f, 1.0f, 0.95f);
            if (blueOrbMainRenderer != null) blueOrbMainRenderer.color = new Color(0.05f, 0.65f, 1.0f, 0.90f);
            if (blueOrbAuraRenderer != null) blueOrbAuraRenderer.color = new Color(0.0f, 0.35f, 1.0f, 0.45f);
        }

        public void CancelCharge()
        {
            if (isCharging)
            {
                isCharging = false;
                currentChargeTimer = 0f;

                if (chargeCoroutine != null)
                {
                    StopCoroutine(chargeCoroutine);
                    chargeCoroutine = null;
                }
                if (blueOrbCollider != null) blueOrbCollider.enabled = false;
                if (blueOrbObj != null)
                {
                    blueOrbObj.SetActive(false);
                    blueOrbObj.transform.localPosition = defaultOrbLocalPos;
                }
                if (visualsTransform != null)
                {
                    visualsTransform.localRotation = Quaternion.identity;
                }
            }
        }

        // =========================================================================
        // 【スペシャル攻撃: 厳格な3段階順序 1入力＝1アクション】
        // 1. 投げる
        // 2. 設置
        // 3. 瞬間移動
        // =========================================================================
        public override void AttackSpecial()
        {
            if (isDead || isStunned || isBlockingState) return;
            if (customSpecialSkill != null && customSpecialSkill.IsActive)
            {
                customSpecialSkill.ExecuteSpecial();
                return;
            }
            if (isDead || isGuardBroken || isStunned) return;

            if (IsSpecialSilenced())
            {
                Debug.Log("[Ninja] Silenced by shadow domain");
                return;
            }

            // 二重入力・重複フレームによる多重進行を防止するためのインターバルガード
            if (Time.time - lastKunaiActionTime < KunaiActionInterval)
            {
                return;
            }

            // 【ステップ3: 瞬間移動】
            // クナイが「地面に設置完了（Placed）」している場合のみ瞬間移動を発動！
            // ★クナイが設置済みであれば、通常攻撃中（チャージ中・スラム振り下ろし中）でも瞬間移動可能！
            if (activeKunai != null && activeKunai.CurrentState == NinjaYellowKunai.KunaiState.Placed)
            {
                lastKunaiActionTime = Time.time;
                TeleportToKunai();
                return;
            }

            // 【落下中（Dropping）の保護ガード】
            // クナイが真下へ降下中（まだ地面に着いていない）場合は、瞬間移動も再設置も受け付けず地面着弾を待つ
            if (activeKunai != null && activeKunai.CurrentState == NinjaYellowKunai.KunaiState.Dropping)
            {
                return;
            }

            // 【ステップ2: 設置】
            // クナイが「飛行中（Flying）」の場合、2回目の入力でその場から真下へ急速落下させて地面に設置
            if (activeKunai != null && activeKunai.CurrentState == NinjaYellowKunai.KunaiState.Flying)
            {
                // 通常攻撃の発動中（チャージ中・スラム中）は設置操作は不可（1入力1アクションの原則）
                if (isCharging || isSlamming) return;

                lastKunaiActionTime = Time.time;
                activeKunai.DropAndPlant();
                return;
            }

            // 【ステップ1: 投げる】
            // クナイが存在しない（または消滅済み）場合のみ、1回目の入力として前方に投擲
            if (activeKunai == null || activeKunai.CurrentState == NinjaYellowKunai.KunaiState.Dispelled)
            {
                // 通常攻撃の発動中は投擲不可（通常攻撃と同時に投げることはできない）
                if (isCharging || isSlamming) return;

                lastKunaiActionTime = Time.time;
                ExecuteThrowKunai();
                return;
            }
        }

        /// <summary>
        /// 必殺技（同時押し）入力時の処理。
        /// 「1.投げる 2.設置 3.瞬間移動」の厳格な順序と1入力1アクションの原則を守るため、
        /// 複合マクロ（同時投げ＋同時攻撃など）は行わず、単一入力として通常攻撃のみを処理します。
        /// </summary>
        public override void AttackUltimate()
        {
            if (isDead || isGuardBroken || isStunned) return;

            // 複合アクションは実行せず、通常攻撃チャージまたはスラムのみを実行
            AttackNormal();
        }

        /// <summary>
        /// ステップ1: ワンタップでの黄色いクナイ投擲処理
        /// </summary>
        private void ExecuteThrowKunai()
        {
            float facing = transform.localScale.x >= 0 ? 1f : -1f;
            Vector3 spawnPos = transform.position + new Vector3(facing * 0.9f, 0.45f, 0f);

            GameObject kunaiGo = new GameObject("NinjaYellowKunai");
            kunaiGo.transform.position = spawnPos;

            activeKunai = kunaiGo.AddComponent<NinjaYellowKunai>();
            activeKunai.Initialize(this, facing, kunaiFlySpeed);
            activeKunai.damage = kunaiDamage;

            // 消滅時に参照をクリア
            activeKunai.OnDispelled += (k) =>
            {
                if (activeKunai == k) activeKunai = null;
            };

            // 投擲時の効果音
            AudioManager.PlayWarningSound();

            Debug.Log($"[Ninja] P{playerID} Step 1: Throw Kunai (Damage: {kunaiDamage})");
        }

        /// <summary>
        /// ステップ3: 黄色い閃光とともに設置されたクナイの位置へ瞬間移動（通常攻撃の発動中でも継続して実行可能）
        /// </summary>
        private void TeleportToKunai()
        {
            if (activeKunai == null) return;

            Vector3 destination = activeKunai.transform.position + Vector3.up * 0.25f;
            Vector3 origin = transform.position;

            // テレポート音の再生
            AudioManager.PlayTeleportSound();

            // 2. 瞬間移動！
            transform.position = destination;

            // スラム中・通常攻撃中の場合は、テレポート先で敵に向かって自動で向き直す
            CharacterBase opponent = FindOpponent();
            if (opponent != null)
            {
                float dx = opponent.transform.position.x - transform.position.x;
                if (Mathf.Abs(dx) > 0.1f)
                {
                    float newFacing = dx > 0 ? 1f : -1f;
                    transform.localScale = new Vector3(newFacing * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
                }
            }

            if (rb != null)
            {
                float facing = transform.localScale.x >= 0 ? 1f : -1f;
                if (isSlamming)
                {
                    rb.linearVelocity = new Vector2(facing * 6.0f, rb.linearVelocity.y);
                }
                else
                {
                    rb.linearVelocity = Vector2.zero;
                }
            }

            // 画面振動
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.20f, 0.12f);
            }

            // クナイ回収・消滅
            activeKunai.Dispel(false);
            activeKunai = null;

            Debug.Log($"[Ninja] P{playerID} Step 3: Teleport to Kunai complete");
        }

        private CharacterBase FindOpponent()
        {
            CharacterBase[] allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (CharacterBase c in allChars)
            {
                if (c != this && !c.isDead) return c;
            }
            return null;
        }

        private void SpawnYellowFlash(Vector3 position, float scale = 1.0f)
        {
            StartCoroutine(YellowFlashVFXRoutine(position, scale));
        }

        private IEnumerator YellowFlashVFXRoutine(Vector3 position, float scale)
        {
            GameObject flashBolt = new GameObject("YellowFlashBolt");
            flashBolt.transform.position = position;
            SpriteRenderer boltSr = flashBolt.AddComponent<SpriteRenderer>();
            boltSr.sprite = GetOrCreateCircleSprite();
            boltSr.color = new Color(1.0f, 0.98f, 0.35f, 0.95f);
            boltSr.sortingOrder = 16;
            flashBolt.transform.localScale = new Vector3(0.3f * scale, 2.4f * scale, 1f);
            flashBolt.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f));

            float duration = 0.22f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (flashBolt != null)
                {
                    boltSr.color = new Color(1.0f, 0.98f, 0.35f, Mathf.Lerp(0.95f, 0f, t));
                    flashBolt.transform.localScale = new Vector3(Mathf.Lerp(0.3f, 0.05f, t) * scale, (2.4f + t * 0.8f) * scale, 1f);
                }

                yield return null;
            }

            if (flashBolt != null) Destroy(flashBolt);
        }

        // =========================================================================
        // ブロック ＆ パリーシステム
        // =========================================================================
        public override void StartBlock()
        {
            if (isDead || isGuardBroken || isSlamming || isStunned) return;

            CancelCharge();

            isBlocking = true;
            isBlockingState = true;
            if (blockShield != null) blockShield.SetActive(true);

            if (Time.time - lastParryTime >= parryCooldown)
            {
                lastParryTime = Time.time;
                if (parryWindowCoroutine != null) StopCoroutine(parryWindowCoroutine);
                parryWindowCoroutine = StartCoroutine(ParryWindowRoutine());
            }
        }

        public override void StopBlock()
        {
            isBlocking = false;
            isBlockingState = false;
            isParrying = false;
            if (blockShield != null) blockShield.SetActive(false);
            if (parryWindowCoroutine != null)
            {
                StopCoroutine(parryWindowCoroutine);
                parryWindowCoroutine = null;
            }
        }

        private IEnumerator ParryWindowRoutine()
        {
            isParrying = true;
            if (blockShield != null)
            {
                SpriteRenderer sr = blockShield.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(0.9f, 0.95f, 1f, 0.95f);
            }

            yield return new WaitForSeconds(parryWindow);

            isParrying = false;
            if (blockShield != null)
            {
                SpriteRenderer sr = blockShield.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(0.3f, 0.8f, 1f, 0.75f);
            }
            parryWindowCoroutine = null;
        }

        public void TriggerGuardBreak()
        {
            if (isDead) return;

            StopBlock();
            CancelCharge();

            if (slamCoroutine != null)
            {
                StopCoroutine(slamCoroutine);
                slamCoroutine = null;
                isSlamming = false;
            }

            StartCoroutine(GuardBreakRoutine());
        }

        private IEnumerator GuardBreakRoutine()
        {
            isGuardBroken = true;
            Debug.Log($"[Ninja] P{playerID} Guard break!");

            if (visualsTransform != null)
            {
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            }

            yield return new WaitForSeconds(guardBreakDuration);

            if (visualsTransform != null)
            {
                visualsTransform.localRotation = Quaternion.identity;
            }

            isGuardBroken = false;
            Debug.Log($"[Ninja] P{playerID} Recovered from guard break");
        }

        // =========================================================================
        // 被ダメージ ＆ ガード / パリー処理
        // =========================================================================
        public override void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isDead || isInvincible) return;

            // 攻撃の飛来方向を判定（飛び道具の位置または相手の位置）
            Vector3 attackOrigin = attackerHitbox != null ? attackerHitbox.transform.position : transform.position;
            if (attackerHitbox != null && attackerHitbox.owner != null)
            {
                if (Mathf.Abs(attackerHitbox.transform.position.x - transform.position.x) < 0.1f)
                {
                    attackOrigin = attackerHitbox.owner.transform.position;
                }
            }

            float faceDir = transform.localScale.x; // 1: 右向き, -1: 左向き
            float relX = attackOrigin.x - transform.position.x;
            bool isBackAttack = (faceDir > 0 && relX < 0) || (faceDir < 0 && relX > 0);

            // 正面からの防御
            if (isBlocking)
            {
                if (isParrying)
                {
                    // パリー成功
                    Debug.Log("[Ninja] ★ PARRY SUCCESS! ★");
                    AudioManager.PlayParrySound();

                    lastParryTime = -10f;
                    currentPosture = Mathf.Max(0f, currentPosture - damage);
                    AddMana(10f);

                    if (attackerHitbox != null && attackerHitbox.owner != null)
                    {
                        attackerHitbox.owner.Stun(0.6f);
                    }

                    if (blockShield != null)
                    {
                        StartCoroutine(ParrySuccessFeedbackRoutine());
                    }
                    return;
                }
                else if (!isBackAttack)
                {
                    // 通常ガード成功
                    Debug.Log("[Ninja] Block success. Posture accumulating.");
                    AudioManager.PlayBlockSound();

                    float postureMultiplier = attackerHitbox != null ? attackerHitbox.postureDamageMultiplier : 1.2f;
                    currentPosture += damage * postureMultiplier;

                    if (currentPosture >= maxPosture)
                    {
                        TriggerGuardBreak();
                    }
                    else
                    {
                        if (blockShield != null)
                        {
                            StartCoroutine(ShieldImpactFlashRoutine());
                        }
                    }
                    return;
                }
            }

            CancelCharge();
            base.TakeDamage(damage, attackerHitbox);
        }

        private IEnumerator ParrySuccessFeedbackRoutine()
        {
            if (blockShield != null)
            {
                SpriteRenderer sr = blockShield.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = Color.white;
            }
            yield return new WaitForSeconds(0.12f);
            if (blockShield != null && isBlocking)
            {
                SpriteRenderer sr = blockShield.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = isParrying ? new Color(0.9f, 0.95f, 1f, 0.95f) : new Color(0.3f, 0.8f, 1f, 0.75f);
            }
        }

        private IEnumerator ShieldImpactFlashRoutine()
        {
            if (blockShield != null)
            {
                SpriteRenderer sr = blockShield.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(0.7f, 0.9f, 1f, 1f);
            }
            yield return new WaitForSeconds(0.08f);
            if (blockShield != null && isBlocking)
            {
                SpriteRenderer sr = blockShield.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(0.3f, 0.8f, 1f, 0.75f);
            }
        }

        public static Sprite GetOrCreateCircleSprite()
        {
            if (cachedCircleSprite != null) return cachedCircleSprite;

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = Color.clear;
            Color white = Color.white;
            float radius = (size - 2) * 0.5f;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        float alpha = Mathf.Clamp01((radius - dist) / 1.5f);
                        tex.SetPixel(x, y, new Color(white.r, white.g, white.b, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, clear);
                    }
                }
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();

            cachedCircleSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                32f
            );
            return cachedCircleSprite;
        }
    }
}
