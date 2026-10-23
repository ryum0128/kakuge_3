using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // =================================================================================
    // 【IceCharacter（氷の剣と氷球を操るキャラクター）】
    // 体は他キャラクターと同じスタイリッシュなグレーの箱型。
    // 通常攻撃は透き通る美しい「氷の剣（Ice Blade）」を構え、
    // 約1秒間かけて重厚かつ華麗に一振りする氷刃スラッシュ（通常ダメージ: 16）。
    // スペシャル攻撃はチャージなしで放つ「アイスボール（ダメージ5）」。
    // 命中した敵を1.5秒間【水色の氷箱】で凍結拘束します（凍結効果には5秒のクールダウンあり）。
    // ブロック・パリー仕様は他キャラクター（Taiken / Flame / Thunder）と完全共通です。
    // =================================================================================
    public class IceCharacter : CharacterBase
    {
        [Header("氷の剣（Ice Blade）設定")]
        [Tooltip("剣の振りかぶり角度（肩上へ高く掲げる）")]
        public float swingStartAngle = 85f;
        [Tooltip("剣の振り下ろし終了角度（前方下への一閃）")]
        public float swingEndAngle = -90f;

        [Tooltip("振りかぶり時間（秒）")]
        public float windupDuration = 0.38f;
        [Tooltip("振り下ろし一閃時間（秒）")]
        public float swingDuration = 0.24f;
        [Tooltip("構え戻し時間（秒）")]
        public float recoverDuration = 0.38f;
        // 合計所要時間: 0.38 + 0.24 + 0.38 = 1.00秒（約1秒かけて振る）

        [Tooltip("通常攻撃のダメージ（普通）")]
        public int normalIceDamage = 16;

        [Header("スペシャル攻撃（氷球 / Ice Ball & 凍結拘束）設定")]
        [Tooltip("アイスボールのダメージ")]
        public int specialIceBallDamage = 5;
        [Tooltip("アイスボールの弾速")]
        public float iceBallSpeed = 12f;
        [Tooltip("アイスボールの飛翔持続時間（秒）")]
        public float iceBallLifetime = 2.0f;
        [Tooltip("スペシャル攻撃の消費マナ")]
        public float specialManaCost = 15f;
        [Tooltip("凍結効果の持続時間（秒）")]
        public float freezeDuration = 1.5f;
        [Tooltip("凍結効果の再発動クールダウン（秒）")]
        public float freezeCooldown = 5.0f;

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
        private float lastFreezeTime = -10f; // 最後に凍結成功した時刻

        private Coroutine normalAttackCoroutine;
        private Coroutine specialAttackCoroutine;
        private Coroutine parryWindowCoroutine;
        private Coroutine guardBreakCoroutine;

        // ビジュアル階層
        private Transform visualsTransform;
        private Transform iceBladePivot;
        private GameObject iceBladeObj;
        private SpriteRenderer iceBladeRenderer;
        public GameObject blockShield;
        private Hitbox swordHitbox;

        private readonly Vector3 defaultBladePos = new Vector3(0.35f, 0.40f, 0f);
        private readonly Vector3 handPivot = new Vector3(0.2f, 0.2f, 0f);

        // プロシージャル生成スプライト（アセット欠損防止）
        private static Sprite cachedIceSwordSprite;
        private static Sprite cachedCircleSprite;
        private static Sprite cachedBodySprite;
        private static Sprite cachedIceSlashSprite;
        private static Sprite cachedIceBallSprite;
        private static Sprite cachedIceBlockBorderSprite;

        protected override void Start()
        {
            base.Start();

            // 1. 体（他と同じグレーの箱）のビジュアル初期化
            SetupBodyVisuals();

            // 2. 氷の剣の初期化
            SetupIceBlade();

            // 3. ブロックシールド（他キャラ完全共通仕様）の初期化
            SetupBlockShield();

            // 4. ヒットボックスの取得・初期化
            swordHitbox = GetComponentInChildren<Hitbox>(true);
            if (swordHitbox != null)
            {
                swordHitbox.damage = normalIceDamage;
                swordHitbox.ownerPlayerID = playerID;
                swordHitbox.owner = this;
                swordHitbox.isNormalAttack = true;
                swordHitbox.postureDamageMultiplier = 1.5f;
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

            // 待機時の氷の剣の微細な浮遊・冷気ブレスアニメーション
            if (!isSwinging && !isSpecialAttacking && !isBlocking && !isGuardBroken && iceBladePivot != null)
            {
                float idleAngle = 25f + Mathf.Sin(Time.time * 3f) * 2.5f;
                iceBladePivot.localRotation = Quaternion.Euler(0, 0, idleAngle);
            }
        }

        public override void Move(float direction)
        {
            // 攻撃中、またはガードブレイク・死亡・スタン中は移動を停止
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

            if (iceBladePivot != null)
            {
                iceBladePivot.localRotation = Quaternion.Euler(0, 0, 25f);
            }

            if (iceBladeRenderer != null)
            {
                iceBladeRenderer.color = Color.white;
            }

            isSwinging = false;
            isSpecialAttacking = false;
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

            SpriteRenderer bodySr = visualsTransform.GetComponent<SpriteRenderer>();
            if (bodySr == null)
            {
                bodySr = visualsTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            if (bodySr.sprite == null)
            {
                bodySr.sprite = GetDefaultBodySprite();
            }

            bodySr.drawMode = SpriteDrawMode.Sliced;
            bodySr.size = new Vector2(1.0f, 2.0f);
            bodySr.color = new Color(0.80f, 0.82f, 0.85f, 1.0f); // 他キャラと同一のグレー色
            bodySr.sortingOrder = 0;
        }

        private void SetupIceBlade()
        {
            iceBladePivot = visualsTransform.Find("IceBladePivot");
            if (iceBladePivot == null)
            {
                GameObject pivotObj = new GameObject("IceBladePivot");
                pivotObj.transform.SetParent(visualsTransform, false);
                pivotObj.transform.localPosition = handPivot;
                iceBladePivot = pivotObj.transform;
            }

            Transform bladeTrans = iceBladePivot.Find("IceBlade");
            if (bladeTrans == null)
            {
                iceBladeObj = new GameObject("IceBlade");
                iceBladeObj.transform.SetParent(iceBladePivot, false);
                // 柄がピボットに位置し、切っ先が前方斜め上に伸びる配置
                iceBladeObj.transform.localPosition = defaultBladePos;
                bladeTrans = iceBladeObj.transform;
            }
            else
            {
                iceBladeObj = bladeTrans.gameObject;
            }

            iceBladeRenderer = iceBladeObj.GetComponent<SpriteRenderer>();
            if (iceBladeRenderer == null)
            {
                iceBladeRenderer = iceBladeObj.AddComponent<SpriteRenderer>();
            }

            iceBladeRenderer.sprite = GetIceSwordSprite();
            iceBladeRenderer.sortingOrder = 6;
            iceBladeRenderer.color = Color.white;

            // 初期位置と角度（自然に構える約25度前傾）
            iceBladePivot.localRotation = Quaternion.Euler(0, 0, 25f);
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
        // 通常攻撃: 【氷刃一閃（約1秒かけて振る重厚な氷の剣撃）】
        // =========================================================

        public override void AttackNormal()
        {
            if (isDead || isSwinging || isSpecialAttacking || isBlocking || isGuardBroken || isDashingOrEvading || IsHurtLocked) return;

            Debug.Log("[Ice] 氷の剣 通常攻撃（氷刃一閃）発動！");
            if (animator != null) animator.SetTrigger("AttackNormal");

            if (normalAttackCoroutine != null) StopCoroutine(normalAttackCoroutine);
            normalAttackCoroutine = StartCoroutine(IceSwordSwingRoutine());
        }

        private IEnumerator IceSwordSwingRoutine()
        {
            isSwinging = true;

            // 1. 予備動作（ウィンドアップフェーズ）: 背後・頭上へ高く振りかぶる (+85度) (約0.38秒)
            float elapsed = 0f;
            Quaternion startRot = iceBladePivot.localRotation;
            Quaternion windupRot = Quaternion.Euler(0, 0, swingStartAngle);

            while (elapsed < windupDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / windupDuration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f); // 滑らかに構え上げる

                iceBladePivot.localRotation = Quaternion.Lerp(startRot, windupRot, t);

                if (iceBladeRenderer != null)
                {
                    // 振りかぶりに合わせて氷の剣が冷気と輝きを帯びる（青白く発光）
                    iceBladeRenderer.color = Color.Lerp(Color.white, new Color(0.7f, 0.95f, 1f, 1f), t);
                }
                yield return null;
            }

            // 2. 振り下ろし一閃フェーズ: 前方地面に向かって大弧一閃 (-90度) (約0.24秒)
            elapsed = 0f;
            Quaternion strikeRot = Quaternion.Euler(0, 0, swingEndAngle);

            // 攻撃判定（Hitbox）の有効化
            if (swordHitbox != null)
            {
                swordHitbox.damage = normalIceDamage;
                swordHitbox.transform.localPosition = new Vector3(1.5f, 0.1f, 0f);
                swordHitbox.postureDamageMultiplier = 1.5f;
                swordHitbox.isNormalAttack = true;

                BoxCollider2D col = swordHitbox.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    col.size = new Vector2(2.2f, 1.4f); // 氷剣のリーチに合わせた判定
                }
                StartCoroutine(ActivateHitboxTemporarily(swordHitbox.gameObject, 0.20f));
            }

            // 氷の斬撃エフェクト（氷晶の衝撃波）
            SpawnIceSlashArc(transform.position + new Vector3(transform.localScale.x * 1.3f, 0.1f, 0f));

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.08f, 0.05f);
            }

            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / swingDuration;
                t = t * t; // 力強く加速して一気に振り抜く！

                iceBladePivot.localRotation = Quaternion.Lerp(windupRot, strikeRot, t);

                if (iceBladeRenderer != null)
                {
                    iceBladeRenderer.color = Color.Lerp(new Color(0.7f, 0.95f, 1f, 1f), Color.white, t);
                }
                yield return null;
            }
            iceBladePivot.localRotation = strikeRot;

            // 3. 回収（フォロースルー・構え戻し）フェーズ (約0.38秒)
            // 合計時間 = 0.38 + 0.24 + 0.38 = 1.00秒
            elapsed = 0f;
            Quaternion readyRot = Quaternion.Euler(0, 0, 25f);
            while (elapsed < recoverDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / recoverDuration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f); // ゆったりと余韻を残しながら戻す

                iceBladePivot.localRotation = Quaternion.Lerp(strikeRot, readyRot, t);
                yield return null;
            }

            iceBladePivot.localRotation = readyRot;
            if (iceBladeRenderer != null) iceBladeRenderer.color = Color.white;

            isSwinging = false;
        }

        // =========================================================
        // クールダウン情報取得（HUD表示用）
        // =========================================================

        public override float GetCooldownRemaining()
        {
            float elapsed = Time.time - lastFreezeTime;
            if (elapsed < freezeCooldown)
            {
                return freezeCooldown - elapsed;
            }
            return 0f;
        }

        public override float GetCooldownDuration()
        {
            return freezeCooldown;
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
            float elapsed = Time.time - lastFreezeTime;
            if (elapsed < freezeCooldown) return freezeCooldown - elapsed;
            return 0f;
        }

        // =========================================================
        // スペシャル攻撃: 【アイスボール発射（チャージなし / 5ダメージ / 1.5秒凍結拘束 / 5秒クールダウン）】
        // =========================================================

        public override void AttackSpecial()
        {
            if (isDead || isSwinging || isSpecialAttacking || isBlocking || isGuardBroken || isDashingOrEvading || IsHurtLocked || isStunned) return;

            if (IsSpecialSilenced())
            {
                Debug.Log("[Ice] 影の領域（沈黙フィールド）内にいるため、スペシャル攻撃を使用できません！");
                return;
            }

            if (currentMana < specialManaCost)
            {
                Debug.Log($"[Ice] マナが足りません！（必要マナ: {specialManaCost}, 現在マナ: {currentMana}）");
                return;
            }

            currentMana -= specialManaCost;
            Debug.Log($"[Ice] スペシャル攻撃（アイスボール発射）発動！ (マナ消費: {specialManaCost})");

            if (animator != null) animator.SetTrigger("AttackSpecial");

            if (specialAttackCoroutine != null) StopCoroutine(specialAttackCoroutine);
            specialAttackCoroutine = StartCoroutine(IceBallCastRoutine());
        }

        private IEnumerator IceBallCastRoutine()
        {
            isSpecialAttacking = true;

            // チャージなしで即座に前方に氷の剣を突き出し、切っ先からアイスボールを即時射出！
            Quaternion readyRot = iceBladePivot != null ? iceBladePivot.localRotation : Quaternion.Euler(0, 0, 25f);
            Quaternion thrustRot = Quaternion.Euler(0, 0, 0f); // 前方に鋭く突き出す構え

            float thrustDuration = 0.08f;
            float elapsed = 0f;
            while (elapsed < thrustDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / thrustDuration;
                if (iceBladePivot != null)
                {
                    iceBladePivot.localRotation = Quaternion.Lerp(readyRot, thrustRot, t);
                }
                yield return null;
            }

            // アイスボールの生成と発射
            float facingDir = transform.localScale.x > 0 ? 1f : -1f;
            Vector3 spawnPos = transform.position + new Vector3(facingDir * 1.2f, 0.2f, 0f);
            LaunchIceBall(spawnPos, facingDir);

            // 発射後の短いフォロースルー（0.14秒）
            float followDuration = 0.14f;
            elapsed = 0f;
            while (elapsed < followDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / followDuration;
                if (iceBladePivot != null)
                {
                    iceBladePivot.localRotation = Quaternion.Lerp(thrustRot, readyRot, t);
                }
                yield return null;
            }

            if (iceBladePivot != null) iceBladePivot.localRotation = readyRot;
            isSpecialAttacking = false;
        }

        private void LaunchIceBall(Vector3 spawnPos, float direction)
        {
            GameObject ball = new GameObject("IceBall_Projectile");
            ball.transform.position = spawnPos;
            ball.transform.localScale = new Vector3(1.1f, 1.1f, 1f);

            // スプライト
            SpriteRenderer sr = ball.AddComponent<SpriteRenderer>();
            sr.sprite = GetIceBallSprite();
            sr.color = Color.white;
            sr.sortingOrder = 8;

            // コライダー & ヒットボックス
            CircleCollider2D col = ball.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.45f;

            Hitbox hb = ball.AddComponent<Hitbox>();
            hb.damage = specialIceBallDamage; // 5ダメージ
            hb.ownerPlayerID = playerID;
            hb.owner = this;
            hb.isNormalAttack = false;
            hb.isProjectile = true;
            hb.postureDamageMultiplier = 1.0f;

            StartCoroutine(IceBallFlightRoutine(ball, hb, direction));
        }

        private IEnumerator IceBallFlightRoutine(GameObject ball, Hitbox hb, float direction)
        {
            if (ball == null) yield break;

            bool hitLanded = false;
            CharacterBase hitTarget = null;

            if (hb != null)
            {
                hb.OnHitLanded += (hurtbox, dmg) =>
                {
                    if (hitLanded) return;
                    hitLanded = true;
                    if (hurtbox != null && hurtbox.owner != null)
                    {
                        hitTarget = hurtbox.owner;
                        OnIceBallHitTarget(hitTarget);
                    }
                };
            }

            float elapsed = 0f;
            SpriteRenderer sr = ball.GetComponent<SpriteRenderer>();

            while (elapsed < iceBallLifetime && !hitLanded && ball != null)
            {
                elapsed += Time.deltaTime;

                // 直進移動
                ball.transform.position += new Vector3(direction * iceBallSpeed * Time.deltaTime, 0f, 0f);

                // 飛翔中の冷気の回転
                ball.transform.Rotate(0f, 0f, -direction * 480f * Time.deltaTime);

                // 終了間際のフェードアウト
                if (elapsed > iceBallLifetime - 0.2f && sr != null)
                {
                    float fade = (iceBallLifetime - elapsed) / 0.2f;
                    sr.color = new Color(1f, 1f, 1f, fade);
                }

                yield return null;
            }

            // 着弾インパクトまたは自然消滅エフェクト
            if (ball != null)
            {
                SpawnIceImpactBurst(ball.transform.position);
                Destroy(ball);
            }
        }

        private void OnIceBallHitTarget(CharacterBase target)
        {
            if (target == null || target.isDead) return;

            float timeSinceLastFreeze = Time.time - lastFreezeTime;
            bool canFreeze = timeSinceLastFreeze >= freezeCooldown;

            if (canFreeze)
            {
                lastFreezeTime = Time.time;
                Debug.Log($"[Ice] ★ 氷球命中（5ダメージ）！ 敵（P{target.playerID}）を1.5秒間凍結！ 水色の氷箱で拘束！ ★");
                StartCoroutine(FreezeTargetRoutine(target, freezeDuration));
            }
            else
            {
                float remainingCd = freezeCooldown - timeSinceLastFreeze;
                Debug.Log($"[Ice] 氷球命中（5ダメージ）！ 凍結効果はクールダウン中（残り {remainingCd:F1}秒）");
            }
        }

        private IEnumerator FreezeTargetRoutine(CharacterBase target, float duration)
        {
            if (target == null || target.isDead) yield break;

            // 1. 水色の箱（IceBlock_Visual）を生成し、凍ったキャラクターの周りに装着
            GameObject iceBlock = new GameObject("IceBlock_Visual");
            iceBlock.transform.SetParent(target.transform, false);
            iceBlock.transform.localPosition = Vector3.zero;
            iceBlock.transform.localScale = Vector3.one;

            // 水色の半透明な氷箱本体スプライト
            SpriteRenderer blockSr = iceBlock.AddComponent<SpriteRenderer>();
            blockSr.sprite = GetDefaultBodySprite();
            blockSr.drawMode = SpriteDrawMode.Sliced;
            blockSr.size = new Vector2(1.35f, 2.35f); // 1.0x2.0のキャラを包み込む水色の箱
            blockSr.color = new Color(0.35f, 0.82f, 1.0f, 0.55f); // 鮮やかな水色（半透明）
            blockSr.sortingOrder = 15; // キャラクターの前面に表示

            // 氷箱の外枠ハイライト（輝く氷の結晶エッジ）
            GameObject borderObj = new GameObject("IceBlock_Border");
            borderObj.transform.SetParent(iceBlock.transform, false);
            borderObj.transform.localPosition = Vector3.zero;
            SpriteRenderer borderSr = borderObj.AddComponent<SpriteRenderer>();
            borderSr.sprite = GetIceBlockBorderSprite();
            borderSr.drawMode = SpriteDrawMode.Sliced;
            borderSr.size = new Vector2(1.4f, 2.4f);
            borderSr.color = new Color(0.85f, 0.98f, 1.0f, 0.85f);
            borderSr.sortingOrder = 16;

            // 2. 凍結による移動・アクション完全停止
            Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
            Animator targetAnim = target.GetComponentInChildren<Animator>();
            float prevAnimSpeed = (targetAnim != null) ? targetAnim.speed : 1f;
            if (targetAnim != null) targetAnim.speed = 0f;

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.12f, 0.08f);
            }

            float timer = 0f;
            while (timer < duration && target != null && !target.isDead)
            {
                timer += Time.deltaTime;

                // 凍結中は常にスタン状態を維持（他からの被弾による解除を防止）
                target.isStunned = true;

                // 物理移動を完全停止
                if (targetRb != null)
                {
                    targetRb.linearVelocity = Vector2.zero;
                }

                // 氷箱の微細な冷気明滅
                if (blockSr != null)
                {
                    float shimmer = 0.55f + Mathf.Sin(timer * 18f) * 0.08f;
                    blockSr.color = new Color(0.35f, 0.82f, 1.0f, shimmer);
                }

                yield return null;
            }

            // 3. 凍結解除 & 氷箱の粉砕演出
            if (targetAnim != null) targetAnim.speed = prevAnimSpeed;
            if (target != null && !target.isDead)
            {
                target.isStunned = false;
                Debug.Log($"[Ice] 敵（P{target.playerID}）の凍結が解除されました！");
            }

            // 氷箱がパリンと砕け散るエフェクト
            if (iceBlock != null)
            {
                Vector3 breakPos = iceBlock.transform.position;
                Destroy(iceBlock);
                SpawnIceShatterBurst(breakPos);
            }
        }

        private void SpawnIceImpactBurst(Vector3 position)
        {
            GameObject burst = new GameObject("IceImpactBurst");
            burst.transform.position = position;
            burst.transform.localScale = Vector3.one * 0.9f;

            SpriteRenderer sr = burst.AddComponent<SpriteRenderer>();
            sr.sprite = GetIceBallSprite();
            sr.color = new Color(0.7f, 0.95f, 1f, 0.9f);
            sr.sortingOrder = 9;

            StartCoroutine(FadeAndScaleBurst(burst, 0.16f, 1.8f));
        }

        private void SpawnIceShatterBurst(Vector3 position)
        {
            // 氷箱が砕ける際、周囲に水色の氷片が飛び散る演出
            for (int i = 0; i < 6; i++)
            {
                GameObject shard = new GameObject("IceShard");
                shard.transform.position = position + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.8f, 0.8f), 0f);
                shard.transform.localScale = Vector3.one * Random.Range(0.25f, 0.45f);

                SpriteRenderer sr = shard.AddComponent<SpriteRenderer>();
                sr.sprite = GetDefaultBodySprite();
                sr.color = new Color(0.5f, 0.9f, 1f, 0.8f);
                sr.sortingOrder = 17;

                Vector2 velocity = new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(1.5f, 4.0f));
                StartCoroutine(IceShardRoutine(shard, velocity, 0.35f));
            }
        }

        private IEnumerator IceShardRoutine(GameObject shard, Vector2 velocity, float duration)
        {
            float elapsed = 0f;
            SpriteRenderer sr = shard.GetComponent<SpriteRenderer>();

            while (elapsed < duration && shard != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                velocity.y -= 9.8f * Time.deltaTime; // 重力落下
                shard.transform.position += (Vector3)(velocity * Time.deltaTime);
                shard.transform.Rotate(0f, 0f, 360f * Time.deltaTime);

                if (sr != null)
                {
                    sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, (1f - t) * 0.8f);
                }
                yield return null;
            }

            if (shard != null) Destroy(shard);
        }

        private IEnumerator FadeAndScaleBurst(GameObject obj, float duration, float targetScale)
        {
            float elapsed = 0f;
            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            Vector3 origScale = obj.transform.localScale;

            while (elapsed < duration && obj != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (sr != null)
                {
                    sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, (1f - t) * 0.9f);
                }
                obj.transform.localScale = Vector3.Lerp(origScale, origScale * targetScale, t);
                yield return null;
            }

            if (obj != null) Destroy(obj);
        }

        // =========================================================
        // ブロック＆パリー処理（全キャラクター完全共通仕様）
        // =========================================================

        public override void StartBlock()
        {
            if (isDead || isSwinging || isSpecialAttacking || isGuardBroken || isStunned || isDashingOrEvading) return;

            // DeepWoken Block Spam Prevention: パリークールダウン判定
            if (Time.time - lastParryTime < parryCooldown)
            {
                isParrying = false;
                isBlocking = false;
                isBlockingState = false;
                Debug.Log("[Ice] Block start ignored (Parry on Cooldown - Shield disabled).");
                return;
            }

            isBlocking = true;
            isBlockingState = true;
            isParrying = true;
            lastParryTime = Time.time;
            Debug.Log("[Ice] Block start (Parry Window Active).");

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
            Debug.Log("[Ice] Block stop.");

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
                Debug.Log("[Ice] Parry window closed.");

                // 通常ブロック色（落ち着いた青）に遷移（他キャラ同一）
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
                    Debug.Log("★ [Ice] PARRY SUCCESS! (DeepWoken Style) ★");
                    AudioManager.PlayParrySound();

                    // クールダウン即解除
                    lastParryTime = -10f;

                    // 体幹ゲージ回復
                    currentPosture = Mathf.Max(0f, currentPosture - damage);

                    // パリー報酬マナ回復 (6f)
                    AddMana(6f);

                    if (opponent != null)
                    {
                        // 攻撃側を0.6秒スタン
                        opponent.Stun(0.6f);

                        // ノックバック付与
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
                    Debug.Log("[Ice] Block success. Posture accumulating.");
                    AudioManager.PlayBlockSound();

                    float postureMultiplier = attackerHitbox != null ? attackerHitbox.postureDamageMultiplier : 1.5f;
                    currentPosture += damage * postureMultiplier;

                    if (currentPosture >= maxPosture)
                    {
                        // ガードブレイク！
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

            // 防御できなかった場合、通常被ダメージ処理へ
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
                    shieldSr.color = Color.white; // 一瞬真っ白に閃光
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

            Debug.Log("[Ice] ★ GUARD BROKEN! Stunned for 1.5s ★");
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
            Debug.Log("[Ice] Guard break recovered.");
        }

        // =========================================================
        // エフェクト演出
        // =========================================================

        private void SpawnIceSlashArc(Vector3 position)
        {
            GameObject arc = new GameObject("IceSlashArc");
            arc.transform.position = position;
            arc.transform.localScale = new Vector3(1.6f, 1.4f, 1f);

            SpriteRenderer sr = arc.AddComponent<SpriteRenderer>();
            sr.sprite = GetIceSlashSprite();
            sr.color = new Color(0.6f, 0.95f, 1f, 0.9f);
            sr.sortingOrder = 7;

            StartCoroutine(FadeAndScaleArc(arc, 0.22f));
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
                    sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, (1f - t) * 0.9f);
                }
                arc.transform.localScale = origScale * (1f + t * 0.4f);
                yield return null;
            }

            if (arc != null) Destroy(arc);
        }

        // =========================================================
        // プロシージャルスプライト生成
        // =========================================================

        public static Sprite GetIceSwordSprite()
        {
            if (cachedIceSwordSprite != null) return cachedIceSwordSprite;

            int w = 24, h = 72;
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

            // 柄頭（Pommel: 氷柱の青い宝玉）
            for (int y = 2; y <= 5; y++)
            {
                for (int x = 10; x <= 13; x++)
                {
                    tex.SetPixel(x, y, new Color(0.4f, 0.85f, 1f, 1f));
                }
            }

            // 柄（Grip: 深い藍色のレザー巻き）
            for (int y = 6; y <= 16; y++)
            {
                for (int x = 11; x <= 12; x++)
                {
                    bool rib = (y % 3 == 0);
                    Color gripCol = rib ? new Color(0.2f, 0.35f, 0.55f, 1f) : new Color(0.12f, 0.18f, 0.30f, 1f);
                    tex.SetPixel(x, y, gripCol);
                }
            }

            // 鍔（Crossguard: 鋭利な氷の結晶の翼）
            for (int y = 17; y <= 19; y++)
            {
                for (int x = 5; x <= 18; x++)
                {
                    bool isEdge = (x <= 6 || x >= 17 || y == 19);
                    Color guardCol = isEdge ? new Color(0.85f, 0.98f, 1f, 1f) : new Color(0.35f, 0.70f, 0.95f, 0.95f);
                    tex.SetPixel(x, y, guardCol);
                }
            }

            // 刀身（Ice Blade: 透き通るクリスタル氷刃）
            Color coreGlow = new Color(0.95f, 1f, 1f, 0.95f);
            Color innerIce = new Color(0.55f, 0.88f, 1f, 0.85f);
            Color outerEdge = new Color(0.80f, 0.96f, 1f, 0.95f);

            for (int y = 20; y < 68; y++)
            {
                float progress = (float)(y - 20) / (68 - 20);
                int halfWidth = Mathf.RoundToInt(Mathf.Lerp(4f, 1f, progress));

                int left = 12 - halfWidth;
                int right = 11 + halfWidth;

                for (int x = left; x <= right; x++)
                {
                    bool isBorder = (x == left || x == right);
                    bool isCenter = (x == 11 || x == 12);

                    if (isCenter)
                    {
                        tex.SetPixel(x, y, coreGlow);
                    }
                    else if (isBorder)
                    {
                        tex.SetPixel(x, y, outerEdge);
                    }
                    else
                    {
                        tex.SetPixel(x, y, innerIce);
                    }
                }
            }

            // 切っ先（Tip: シャープな1点）
            tex.SetPixel(11, 68, coreGlow);
            tex.SetPixel(12, 68, coreGlow);
            tex.SetPixel(11, 69, outerEdge);
            tex.SetPixel(12, 69, outerEdge);
            tex.SetPixel(11, 70, Color.white);

            tex.Apply();
            cachedIceSwordSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.15f), 32f);
            return cachedIceSwordSprite;
        }

        public static Sprite GetIceBallSprite()
        {
            if (cachedIceBallSprite != null) return cachedIceBallSprite;

            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        float normDist = dist / radius;
                        if (normDist < 0.35f)
                        {
                            tex.SetPixel(x, y, new Color(0.95f, 1f, 1f, 1f)); // 中心コア白発光
                        }
                        else if (normDist < 0.75f)
                        {
                            tex.SetPixel(x, y, new Color(0.4f, 0.88f, 1f, 0.95f)); // 透き通るシアン氷
                        }
                        else
                        {
                            float alpha = Mathf.Clamp01((1f - normDist) / 0.25f);
                            tex.SetPixel(x, y, new Color(0.6f, 0.95f, 1f, alpha));
                        }
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            // 雪の結晶模様（中央十字）
            for (int i = 8; i < 24; i++)
            {
                tex.SetPixel(i, 16, Color.white);
                tex.SetPixel(16, i, Color.white);
            }

            tex.Apply();
            cachedIceBallSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return cachedIceBallSprite;
        }

        public static Sprite GetIceBlockBorderSprite()
        {
            if (cachedIceBlockBorderSprite != null) return cachedIceBlockBorderSprite;

            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color borderCol = new Color(0.85f, 0.98f, 1f, 0.95f);
            Color clear = Color.clear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = (x <= 2 || x >= 29 || y <= 2 || y >= 29);
                    tex.SetPixel(x, y, isBorder ? borderCol : clear);
                }
            }

            tex.Apply();
            cachedIceBlockBorderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
            return cachedIceBlockBorderSprite;
        }

        private static Sprite GetIceSlashSprite()
        {
            if (cachedIceSlashSprite != null) return cachedIceSlashSprite;

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color cyanLight = new Color(0.5f, 0.92f, 1f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(10, 10));
                    if (dist >= 28f && dist <= 44f && x >= y - 8 && x <= y + 36)
                    {
                        float mid = 36f;
                        float diff = Mathf.Abs(dist - mid) / 8f;
                        float alpha = Mathf.Clamp01(1f - diff);
                        tex.SetPixel(x, y, new Color(cyanLight.r, cyanLight.g, cyanLight.b, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            cachedIceSlashSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return cachedIceSlashSprite;
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
