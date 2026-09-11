using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    /// <summary>
    /// 炎を操る格闘キャラクター「炎 (Flame)」
    /// パリー、ガード、ダッシュ、回避などの基本操作・挙動は他キャラクターと完全共通仕様です。
    /// （技は今後追加予定）
    /// </summary>
    public class FlameCharacter : CharacterBase
    {
        [Header("炎キャラクター固有設定")]
        public Transform visualsTransform;
        public GameObject blockShield;
        public GameObject redBallWeapon;
        private readonly Vector3 normalBallPos = new Vector3(0.55f, 0.25f, 0f); // 常にキャラの体・手元にぴったり密着

        [Header("炎の剣（Fire Blade）設定")]
        public Transform fireBladePivot;
        public GameObject fireBladeObj;
        public SpriteRenderer fireBladeRenderer;
        public Hitbox normalAttackHitbox;
        public Sprite fireBladeSprite;
        private bool isAttacking = false;
        private Coroutine normalAttackCoroutine;

        [Header("スペシャル攻撃（Fire Ball）3段階チャージ設定")]
        [Tooltip("Lv1 (1秒溜め): ダメージ 10, マナ 10, 小さめ")]
        public int specialDamageLv1 = 10;
        public float specialManaCostLv1 = 10f;
        public float specialRadiusLv1 = 0.65f;

        [Tooltip("Lv2 (2秒溜め): ダメージ 15, マナ 15, 中くらい")]
        public int specialDamageLv2 = 15;
        public float specialManaCostLv2 = 15f;
        public float specialRadiusLv2 = 0.95f;

        [Tooltip("Lv3 (3秒溜め・最大威力): ダメージ 25, マナ 25, 特大（今までと同じ）")]
        public int specialDamageLv3 = 25;
        public float specialManaCostLv3 = 25f;
        public float specialRadiusLv3 = 1.30f;

        [Tooltip("Fire Ballのガード時Posture削り倍率 (2.8倍: Lv1=28, Lv2=42, Lv3=70)")]
        public float specialPostureMultiplier = 2.8f;
        [Tooltip("吹き込み・飛翔時間（秒）")]
        public float specialFlightDuration = 1.0f;
        [Tooltip("弾速")]
        public float specialSpeed = 9.5f;

        private bool isSpecialAttacking = false;
        private Coroutine specialAttackCoroutine = null;
        private GameObject currentChargingBall = null;

        public override bool IsAttacking => isAttacking || isSpecialAttacking;

        [Header("ブロック・パリー設定")]
        [Tooltip("パリー受付時間（秒）")]
        public float parryWindow = 0.25f;
        [Tooltip("パリー再使用クールダウン（秒）")]
        public float parryCooldown = 0.5f;

        private bool isBlocking = false;
        private bool isParrying = false;
        private bool isGuardBroken = false;
        private float lastParryTime = -10f;
        private Coroutine parryWindowCoroutine;
        private Coroutine guardBreakCoroutine;

        private SpriteRenderer spriteRenderer;

        protected virtual void Awake()
        {
            SetupRedBallWeapon();
        }

        protected override void Start()
        {
            base.Start();

            if (visualsTransform == null)
            {
                visualsTransform = transform.Find("Visuals");
                if (visualsTransform == null) visualsTransform = transform;
            }

            spriteRenderer = visualsTransform.GetComponentInChildren<SpriteRenderer>();

            // 武器（赤いボール）の初期化（常にキャラの手元に常駐）
            SetupRedBallWeapon();

            // 炎の剣（Fire Blade）の初期化
            SetupFireBlade();

            // ブロックシールド（炎のバリア）の初期化
            SetupBlockShield();
        }

        protected override void Update()
        {
            base.Update();

            if (isDead)
            {
                if (isAttacking || isSpecialAttacking) CancelAttacks();
                return;
            }

            // 赤いボール武器: キャラクターの体・手元に常に寄り添って追従
            if (redBallWeapon != null && !isAttacking && !isSpecialAttacking)
            {
                float bobbingY = normalBallPos.y + Mathf.Sin(Time.time * 4f) * 0.04f;
                redBallWeapon.transform.localPosition = new Vector3(normalBallPos.x, bobbingY, normalBallPos.z);
            }
        }

        public override void Move(float direction)
        {
            // スペシャル攻撃の溜め・吹き込み中は移動不可
            if (isSpecialAttacking)
            {
                if (rb != null)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
                SafeSetFloat("Speed", 0f);
                return;
            }

            base.Move(direction);
        }

        private void SetupRedBallWeapon()
        {
            if (visualsTransform == null)
            {
                visualsTransform = transform.Find("Visuals");
                if (visualsTransform == null) visualsTransform = transform;
            }

            if (redBallWeapon == null)
            {
                Transform existingBall = visualsTransform.Find("RedBallWeapon");
                if (existingBall == null) existingBall = transform.Find("RedBallWeapon");

                if (existingBall != null)
                {
                    redBallWeapon = existingBall.gameObject;
                }
                else
                {
                    redBallWeapon = new GameObject("RedBallWeapon");
                    redBallWeapon.transform.SetParent(visualsTransform);
                    redBallWeapon.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
                }
            }

            if (redBallWeapon.transform.parent != visualsTransform)
            {
                redBallWeapon.transform.SetParent(visualsTransform);
            }
            redBallWeapon.transform.localPosition = normalBallPos;

            SpriteRenderer ballSr = redBallWeapon.GetComponent<SpriteRenderer>();
            if (ballSr == null) ballSr = redBallWeapon.AddComponent<SpriteRenderer>();
            if (ballSr.sprite == null)
            {
                ballSr.sprite = GetOrCreateCircleSprite();
            }
            ballSr.color = new Color(1.0f, 0.2f, 0.1f, 1.0f);
            ballSr.sortingOrder = 5;

            redBallWeapon.SetActive(true);
        }

        private static Sprite circleSpriteCache = null;
        public static Sprite GetOrCreateCircleSprite()
        {
            if (circleSpriteCache != null) return circleSpriteCache;

#if UNITY_EDITOR
            circleSpriteCache = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (circleSpriteCache != null) return circleSpriteCache;
#endif
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
            circleSpriteCache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return circleSpriteCache;
        }

        private void SetupBlockShield()
        {
            if (blockShield == null)
            {
                Transform existingShield = transform.Find("BlockShield");
                if (existingShield == null) existingShield = transform.Find("Flame_BlockShield");

                if (existingShield != null)
                {
                    blockShield = existingShield.gameObject;
                }
                else
                {
                    blockShield = new GameObject("BlockShield");
                    blockShield.transform.SetParent(transform);
                    blockShield.transform.localPosition = new Vector3(0.7f, 0f, 0f);
                    blockShield.transform.localScale = Vector3.one;

                    SpriteRenderer shieldSr = blockShield.AddComponent<SpriteRenderer>();
                    shieldSr.sprite = GetOrCreateCircleSprite();
                    shieldSr.drawMode = SpriteDrawMode.Sliced;
                    shieldSr.size = new Vector2(0.8f, 2.0f);
                    shieldSr.color = new Color(0.3f, 0.8f, 1f, 0.6f); // 他キャラと同じ鮮やかな青色
                    shieldSr.sortingOrder = 10;
                }
            }

            if (blockShield != null)
            {
                blockShield.SetActive(false);
            }
        }

        private void SetupFireBlade()
        {
            if (fireBladePivot == null)
            {
                Transform existingPivot = transform.Find("FireBladePivot");
                if (existingPivot != null)
                {
                    fireBladePivot = existingPivot;
                }
                else
                {
                    GameObject pivotObj = new GameObject("FireBladePivot");
                    pivotObj.transform.SetParent(transform);
                    pivotObj.transform.localPosition = new Vector3(0.2f, 0.2f, 0f);
                    pivotObj.transform.localRotation = Quaternion.identity;
                    fireBladePivot = pivotObj.transform;
                }
            }

            if (fireBladeObj == null)
            {
                Transform existingBlade = fireBladePivot.Find("FireBlade");
                if (existingBlade != null)
                {
                    fireBladeObj = existingBlade.gameObject;
                }
                else
                {
                    fireBladeObj = new GameObject("FireBlade");
                    fireBladeObj.transform.SetParent(fireBladePivot);
                    fireBladeObj.transform.localPosition = Vector3.zero;
                    fireBladeObj.transform.localRotation = Quaternion.identity;
                    fireBladeObj.transform.localScale = Vector3.one;
                }
            }

            fireBladeRenderer = fireBladeObj.GetComponent<SpriteRenderer>();
            if (fireBladeRenderer == null)
            {
                fireBladeRenderer = fireBladeObj.AddComponent<SpriteRenderer>();
            }

            if (fireBladeSprite == null)
            {
                fireBladeSprite = GetOrCreateFireBladeSprite();
            }

            if (fireBladeSprite != null)
            {
                fireBladeRenderer.sprite = fireBladeSprite;
            }

            fireBladeRenderer.sortingOrder = 8;

            // 剣の刃（blade）のグラフィックと1ミリの狂いもなく完全一致する当たり判定（Hitbox）
            Transform existingBladeHitbox = fireBladeObj.transform.Find("BladeHitbox");
            GameObject hitboxObj = existingBladeHitbox != null ? existingBladeHitbox.gameObject : null;

            if (hitboxObj == null)
            {
                // 旧仕様のHitboxがあれば削除
                Transform oldHitbox = fireBladePivot.Find("Hitbox");
                if (oldHitbox != null)
                {
                    Destroy(oldHitbox.gameObject);
                }

                hitboxObj = new GameObject("BladeHitbox");
                hitboxObj.transform.SetParent(fireBladeObj.transform);
            }

            // 炎の剣の刃の傾き（45度）、柄から切っ先（2.2m）の真ん中にコライダーを配置
            hitboxObj.transform.localPosition = new Vector3(0.78f, 0.78f, 0f);
            hitboxObj.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            hitboxObj.transform.localScale = Vector3.one;

            BoxCollider2D boxCol = hitboxObj.GetComponent<BoxCollider2D>();
            if (boxCol == null) boxCol = hitboxObj.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            // 刃の全長 2.2f、炎の刃の幅 0.75f で刃の見た目と完全に一致！
            boxCol.size = new Vector2(2.2f, 0.75f);
            boxCol.offset = Vector2.zero;

            normalAttackHitbox = hitboxObj.GetComponent<Hitbox>();
            if (normalAttackHitbox == null) normalAttackHitbox = hitboxObj.AddComponent<Hitbox>();
            normalAttackHitbox.damage = 18;
            normalAttackHitbox.ownerPlayerID = playerID;
            normalAttackHitbox.postureDamageMultiplier = 1.5f;
            normalAttackHitbox.isNormalAttack = true;

            hitboxObj.SetActive(false);
            fireBladeObj.SetActive(false);
        }

        private static Sprite fireBladeSpriteRuntimeCache = null;
        public static Sprite GetOrCreateFireBladeSprite()
        {
            if (fireBladeSpriteRuntimeCache != null) return fireBladeSpriteRuntimeCache;

#if UNITY_EDITOR
            string assetPath = "Assets/Flame/FireBladeSprite.png";
            fireBladeSpriteRuntimeCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (fireBladeSpriteRuntimeCache != null) return fireBladeSpriteRuntimeCache;
#endif
            string fullPath = System.IO.Path.Combine(Application.dataPath, "Flame/FireBladeSprite.png");
            if (System.IO.File.Exists(fullPath))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    fireBladeSpriteRuntimeCache = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.18f, 0.18f), 380f);
                    return fireBladeSpriteRuntimeCache;
                }
            }
            return null;
        }

        // =========================================================
        // パリー・ブロック処理（他キャラと共通仕様）
        // =========================================================

        public override void StartBlock()
        {
            if (isDead || isGuardBroken || isStunned || isDashingOrEvading) return;

            // パリークールダウン判定（連打防止）
            if (Time.time - lastParryTime < parryCooldown)
            {
                isParrying = false;
                isBlocking = false;
                isBlockingState = false;
                return;
            }

            isBlocking = true;
            isBlockingState = true;
            isParrying = true;
            lastParryTime = Time.time;

            if (parryWindowCoroutine != null) StopCoroutine(parryWindowCoroutine);
            parryWindowCoroutine = StartCoroutine(CloseParryWindowRoutine());

            // 他キャラと全く同じシールド表示
            if (blockShield != null)
            {
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr == null) shieldSr = blockShield.AddComponent<SpriteRenderer>();
                if (shieldSr.sprite == null) shieldSr.sprite = GetOrCreateCircleSprite();

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
        // 被ダメージ＆パリー判定（他キャラ共通）
        // =========================================================

        public override void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isDead || isInvincible) return;

            // 相手キャラを探して背後攻撃（めくり）かどうかを判定
            CharacterBase opponent = null;
            CharacterBase[] allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (var c in allChars)
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
                float faceDir = transform.localScale.x; // 1: 右向き, -1: 左向き
                float relX = opponent.transform.position.x - transform.position.x;
                if ((faceDir > 0 && relX < 0) || (faceDir < 0 && relX > 0))
                {
                    isBackAttack = true; // 背後からの攻撃
                }
            }

            // 正面からの防御
            if (isBlocking && !isBackAttack)
            {
                if (isParrying)
                {
                    // ★パリー成功！
                    Debug.Log("★炎のパリー成功！ノーダメージ！");
                    lastParryTime = -10f; // パリークールダウン即時リセット

                    // 姿勢値回復ボーナス
                    currentPosture -= damage;
                    if (currentPosture < 0f) currentPosture = 0f;

                    // マナ回復ボーナス (+6)
                    AddMana(6f);

                    StartCoroutine(ParrySuccessFeedbackRoutine());
                    return;
                }
                else
                {
                    // 通常ブロック成功
                    Debug.Log("炎のガード成功！ダメージ防御！");
                    float postureMultiplier = attackerHitbox != null ? attackerHitbox.postureDamageMultiplier : 1.5f;
                    currentPosture += damage * postureMultiplier;

                    if (currentPosture >= maxPosture)
                    {
                        StartCoroutine(GuardBreakRoutine());
                    }
                    else
                    {
                        StartCoroutine(BlockHitFeedbackRoutine());
                    }
                    return;
                }
            }

            // 通常被弾
            base.TakeDamage(damage, attackerHitbox);
            if (isDead) CancelAttacks();
        }

        private IEnumerator ParrySuccessFeedbackRoutine()
        {
            // ヒットストップ
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerHitStop(6);
                HUDManager.Instance.TriggerScreenShake(0.12f, 0.08f);
            }

            // シールドが白くフラッシュ（他キャラ同一）
            if (blockShield != null)
            {
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr != null)
                {
                    Color orig = isParrying ? new Color(0.3f, 0.8f, 1f, 0.6f) : new Color(0.3f, 0.5f, 0.7f, 0.45f);
                    shieldSr.color = new Color(1f, 1f, 1f, 0.9f);
                    yield return new WaitForSeconds(0.15f);
                    if (isBlocking) shieldSr.color = orig;
                }
            }
        }

        private IEnumerator BlockHitFeedbackRoutine()
        {
            if (blockShield != null)
            {
                Vector3 origScale = blockShield.transform.localScale;
                blockShield.transform.localScale = origScale * 1.25f;
                yield return new WaitForSeconds(0.08f);
                blockShield.transform.localScale = origScale;
            }
        }

        private IEnumerator GuardBreakRoutine()
        {
            isGuardBroken = true;
            CancelAttacks();
            StopBlock();

            currentPosture = 0f;
            SafeSetTrigger("Stunned");

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.3f, 0.15f);
            }

            yield return new WaitForSeconds(1.8f);

            isGuardBroken = false;
        }

        // =========================================================
        // 攻撃技
        // =========================================================

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

            if (normalAttackHitbox != null)
            {
                normalAttackHitbox.gameObject.SetActive(false);
            }

            if (fireBladeObj != null)
            {
                fireBladeObj.SetActive(false);
            }

            if (fireBladePivot != null)
            {
                fireBladePivot.localRotation = Quaternion.identity;
            }

            if (fireBladeRenderer != null)
            {
                fireBladeRenderer.color = Color.white;
            }

            if (redBallWeapon != null)
            {
                redBallWeapon.transform.localPosition = normalBallPos;
                redBallWeapon.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            }

            if (specialAttackCoroutine != null)
            {
                StopCoroutine(specialAttackCoroutine);
                specialAttackCoroutine = null;
            }

            if (currentChargingBall != null)
            {
                Destroy(currentChargingBall);
                currentChargingBall = null;
            }

            isAttacking = false;
            isSpecialAttacking = false;
        }

        /// <summary>
        /// 通常攻撃: 炎の剣（Fire Blade）による一閃スラッシュ
        /// 発動から攻撃発生まではTAIKENの通常攻撃と完全に同じスピード（0.18秒）
        /// </summary>
        public override void AttackNormal()
        {
            if (isDead || isAttacking || isSpecialAttacking || isBlocking || isGuardBroken || isDashingOrEvading || IsHurtLocked) return;

            Debug.Log("炎キャラクター: Fire Blade 通常スラッシュ発動！");

            SafeSetTrigger("AttackNormal");

            if (normalAttackCoroutine != null) StopCoroutine(normalAttackCoroutine);
            normalAttackCoroutine = StartCoroutine(FireBladeSlashRoutine());
        }

        private IEnumerator FireBladeSlashRoutine()
        {
            isAttacking = true;

            if (fireBladeObj != null)
            {
                fireBladeObj.SetActive(true);
            }

            if (fireBladeRenderer != null)
            {
                // 初期状態はうっすら光を帯びて出現
                fireBladeRenderer.color = new Color(1f, 0.6f, 0.2f, 0.4f);
            }

            Vector3 origBallScale = redBallWeapon != null ? redBallWeapon.transform.localScale : new Vector3(1.4f, 1.4f, 1f);
            Vector3 handPos = new Vector3(0.35f, 0.25f, 0f);

            // 1. 予備動作（ウィンドアップフェーズ）: TAIKENと全く同じ攻撃スピード (0.18秒)
            // 発動から攻撃するまでの溜め・振りかぶり時間。赤いボールも手元に引き寄せられて共鳴
            float windupDuration = 0.18f;
            float elapsed = 0f;

            while (elapsed < windupDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / windupDuration;
                float ease = Mathf.Sin(t * Mathf.PI * 0.5f);

                // 炎の剣を背後・頭上へ高く振りかぶる (+65度)
                float angle = Mathf.Lerp(0f, 65f, ease);
                if (fireBladePivot != null)
                {
                    fireBladePivot.localRotation = Quaternion.Euler(0, 0, angle);
                }

                if (fireBladeRenderer != null)
                {
                    // 振りかぶりに合わせて炎の剣が燃え上がる
                    fireBladeRenderer.color = Color.Lerp(new Color(1f, 0.6f, 0.2f, 0.4f), Color.white, ease);
                }

                if (redBallWeapon != null)
                {
                    // 赤いボールも手元に引き寄せられ、炎の剣のコアとして脈動！
                    redBallWeapon.transform.localPosition = Vector3.Lerp(normalBallPos, handPos, ease);
                    redBallWeapon.transform.localScale = origBallScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.18f);
                }

                yield return null;
            }

            // 2. 斬撃（スラッシュフェーズ）: 地面に向かって炎の刃で一気に一閃！ (0.16秒)
            // 刃のグラフィックと完全同期したHitboxを有効化！
            if (normalAttackHitbox != null)
            {
                normalAttackHitbox.ownerPlayerID = playerID;
                StartCoroutine(ActivateHitboxTemporarily(normalAttackHitbox.gameObject, 0.16f));
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.08f, 0.05f);
            }

            float swingDuration = 0.16f;
            elapsed = 0f;

            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / swingDuration;
                float ease = t * t; // 力強く加速して振り抜く

                // +65度から一気に地面前方の-105度まで大弧スラッシュ！
                float angle = Mathf.Lerp(65f, -105f, ease);
                if (fireBladePivot != null)
                {
                    fireBladePivot.localRotation = Quaternion.Euler(0, 0, angle);
                }

                if (fireBladeRenderer != null)
                {
                    // スラッシュ瞬間は白熱炎、振り抜きにかけて深紅の炎に
                    fireBladeRenderer.color = Color.Lerp(new Color(1.3f, 1.2f, 0.8f, 1f), new Color(1f, 0.35f, 0.1f, 1f), t);
                }

                if (redBallWeapon != null)
                {
                    // 攻撃中も手元にしっかり吸い付いて一体化
                    redBallWeapon.transform.localPosition = handPos;
                }

                yield return null;
            }

            // 3. 回収・消滅（フォロースルーフェーズ）: 斬り抜いた後、炎がフッと消えて赤いボールが元の位置へ戻る (0.20秒)
            float recoverDuration = 0.20f;
            elapsed = 0f;

            while (elapsed < recoverDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / recoverDuration;

                if (fireBladeRenderer != null)
                {
                    // 炎の剣が自然にフェードアウト
                    fireBladeRenderer.color = new Color(1f, 0.35f, 0.1f, 1f - t);
                }

                if (redBallWeapon != null)
                {
                    redBallWeapon.transform.localPosition = Vector3.Lerp(handPos, normalBallPos, t);
                    redBallWeapon.transform.localScale = Vector3.Lerp(redBallWeapon.transform.localScale, origBallScale, t);
                }

                yield return null;
            }

            // 終了処理
            if (fireBladeObj != null)
            {
                fireBladeObj.SetActive(false);
            }

            if (fireBladePivot != null)
            {
                fireBladePivot.localRotation = Quaternion.identity;
            }

            if (fireBladeRenderer != null)
            {
                fireBladeRenderer.color = Color.white;
            }

            if (redBallWeapon != null)
            {
                redBallWeapon.transform.localPosition = normalBallPos;
                redBallWeapon.transform.localScale = origBallScale;
            }

            isAttacking = false;
        }


        // =========================================================
        // スペシャル攻撃: Fire Ball（長押しで最大3秒溜められる3段階チャージ技）
        // 1秒: ダメージ10, マナ10, 小さめ
        // 2秒: ダメージ15, マナ15, 中くらい
        // 3秒: ダメージ25, マナ25, 特大（今までと同じ最大威力）
        // =========================================================

        public override void AttackSpecial()
        {
            if (isDead || isAttacking || isSpecialAttacking || isBlocking || isGuardBroken || isDashingOrEvading || IsHurtLocked) return;

            if (currentMana < specialManaCostLv1)
            {
                Debug.Log($"炎キャラクター: マナが足りません！ (最低必要マナ: {specialManaCostLv1})");
                return;
            }

            Debug.Log("炎キャラクター: Fire Ball チャージ開始！（長押しで最大3秒まで溜め可能）");
            SafeSetTrigger("AttackSpecial");

            if (specialAttackCoroutine != null) StopCoroutine(specialAttackCoroutine);
            specialAttackCoroutine = StartCoroutine(FireBallSpecialRoutine());
        }

        private bool IsChargeKeyHeld()
        {
            PlayerInputController pic = GetComponent<PlayerInputController>();
            if (pic != null)
            {
                return pic.IsSpecialAttackHeld();
            }

            if (UnityEngine.InputSystem.Keyboard.current == null) return false;
            if (playerID == 1)
            {
                return UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.L].isPressed
                    || UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.K].isPressed;
            }
            else
            {
                return UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.O].isPressed
                    || UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.Numpad3].isPressed
                    || UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.I].isPressed;
            }
        }

        private IEnumerator FireBallSpecialRoutine()
        {
            isSpecialAttacking = true;

            // チャージ中の火球プレビューを生成（基準サイズLv3で生成し、スケールで動的拡大）
            currentChargingBall = CreateBigFireBall(3);
            currentChargingBall.transform.SetParent(transform);
            Vector3 handFrontPos = new Vector3(1.15f, 0.2f, 0f);
            currentChargingBall.transform.localPosition = handFrontPos;
            currentChargingBall.transform.localScale = Vector3.zero;

            // 溜め中は当たり判定コライダーを無効化
            Collider2D col = currentChargingBall.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            Vector3 origBallScale = redBallWeapon != null ? redBallWeapon.transform.localScale : new Vector3(1.4f, 1.4f, 1f);
            Vector3 handPos = new Vector3(0.35f, 0.25f, 0f);

            float chargeTimer = 0f;
            int currentLevel = 0;
            int prevLevel = 0;

            // 最大3.0秒まで溜められる
            while (chargeTimer < 3.0f)
            {
                chargeTimer += Time.deltaTime;

                // 所持マナに応じたチャージ上限
                int maxAllowedLevel = 1;
                if (currentMana >= specialManaCostLv3) maxAllowedLevel = 3;
                else if (currentMana >= specialManaCostLv2) maxAllowedLevel = 2;

                if (chargeTimer >= 3.0f && maxAllowedLevel >= 3) currentLevel = 3;
                else if (chargeTimer >= 2.0f && maxAllowedLevel >= 2) currentLevel = 2;
                else if (chargeTimer >= 1.0f && maxAllowedLevel >= 1) currentLevel = 1;
                else currentLevel = 0;

                // レベルアップ時の閃光＆画面シェイク演出！
                if (currentLevel > prevLevel)
                {
                    prevLevel = currentLevel;
                    if (HUDManager.Instance != null)
                    {
                        HUDManager.Instance.TriggerScreenShake(0.04f + currentLevel * 0.03f, 0.03f + currentLevel * 0.02f);
                    }
                    StartCoroutine(FlashChargingBallRoutine(currentChargingBall));
                    Debug.Log($"★Fire Ball チャージ Lv{currentLevel} 達成！");
                }

                // スケール計算: 溜まり度合いに応じてなめらかに拡大
                float targetScaleFactor;
                if (chargeTimer < 1.0f)
                {
                    targetScaleFactor = Mathf.Lerp(0.15f, specialRadiusLv1 / specialRadiusLv3, chargeTimer / 1.0f);
                }
                else if (chargeTimer < 2.0f)
                {
                    targetScaleFactor = Mathf.Lerp(specialRadiusLv1 / specialRadiusLv3, specialRadiusLv2 / specialRadiusLv3, (chargeTimer - 1.0f) / 1.0f);
                }
                else
                {
                    targetScaleFactor = Mathf.Lerp(specialRadiusLv2 / specialRadiusLv3, 1.0f, (chargeTimer - 2.0f) / 1.0f);
                }

                // チャージ中の脈動
                float pulse = 1f + Mathf.Sin(chargeTimer * (18f + currentLevel * 6f)) * (0.05f + currentLevel * 0.025f);
                if (currentChargingBall != null)
                {
                    currentChargingBall.transform.localScale = Vector3.one * (targetScaleFactor * pulse);
                }

                // 手元の赤いボール武器も火球へとエネルギーを注ぎ込むように共鳴
                if (redBallWeapon != null)
                {
                    float handEase = Mathf.Clamp01(chargeTimer / 1.0f);
                    redBallWeapon.transform.localPosition = Vector3.Lerp(normalBallPos, handPos, handEase);
                    redBallWeapon.transform.localScale = origBallScale * (1f + Mathf.Sin(chargeTimer * 20f) * (0.16f + currentLevel * 0.08f));
                }

                // キー入力の継続判定（キーを離したらその時点のレベルで発射）
                bool keyHeld = IsChargeKeyHeld();
                if (!keyHeld)
                {
                    if (currentLevel >= 1)
                    {
                        // 1秒以上溜めた状態で離した → 即発射！
                        break;
                    }
                    else
                    {
                        // 1秒未満で離した場合はチャージキャンセル
                        Debug.Log("Fire Ball: 1秒未満でキーを離したためチャージをキャンセルしました");
                        CancelAttacks();
                        yield break;
                    }
                }

                yield return null;
            }

            // 発射レベルの決定（最低でもLv1、最大Lv3）
            int launchLevel = Mathf.Clamp(currentLevel, 1, 3);
            int damage = (launchLevel == 3) ? specialDamageLv3 : (launchLevel == 2) ? specialDamageLv2 : specialDamageLv1;
            float manaCost = (launchLevel == 3) ? specialManaCostLv3 : (launchLevel == 2) ? specialManaCostLv2 : specialManaCostLv1;

            // マナ消費
            currentMana -= manaCost;
            if (currentMana < 0f) currentMana = 0f;

            Debug.Log($"炎キャラクター: Fire Ball Lv{launchLevel} 吹き込み発射！ (ダメージ: {damage}, 消費マナ: {manaCost})");

            // 溜め用オブジェクトを消去し、発射用の正規火球を生成して射出！
            if (currentChargingBall != null)
            {
                Destroy(currentChargingBall);
                currentChargingBall = null;
            }

            GameObject launchedBall = CreateBigFireBall(launchLevel);
            launchedBall.transform.position = transform.position + new Vector3(transform.localScale.x * handFrontPos.x, handFrontPos.y, 0f);
            launchedBall.transform.localScale = Vector3.one;

            // 吹き込みの衝撃画面シェイク
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.08f + launchLevel * 0.05f, 0.05f + launchLevel * 0.03f);
            }

            // キャラの向いている方向へ吹き込み射出（約1.0秒間直進）
            float facingDir = transform.localScale.x > 0 ? 1f : -1f;
            StartCoroutine(BlowFireBallFlightRoutine(launchedBall, facingDir, damage, launchLevel));

            // 吹き込み後のキャラクターのフォロースルー（約0.22秒で通常行動に復帰）
            float recoverDuration = 0.22f;
            float recElapsed = 0f;
            while (recElapsed < recoverDuration)
            {
                recElapsed += Time.deltaTime;
                float t = recElapsed / recoverDuration;

                if (redBallWeapon != null)
                {
                    redBallWeapon.transform.localPosition = Vector3.Lerp(handPos, normalBallPos, t);
                    redBallWeapon.transform.localScale = Vector3.Lerp(redBallWeapon.transform.localScale, origBallScale, t);
                }

                yield return null;
            }

            if (redBallWeapon != null)
            {
                redBallWeapon.transform.localPosition = normalBallPos;
                redBallWeapon.transform.localScale = origBallScale;
            }

            isSpecialAttacking = false;
            specialAttackCoroutine = null;
        }

        private IEnumerator FlashChargingBallRoutine(GameObject ball)
        {
            if (ball == null) yield break;
            SpriteRenderer[] srs = ball.GetComponentsInChildren<SpriteRenderer>();
            Color[] origColors = new Color[srs.Length];
            for (int i = 0; i < srs.Length; i++)
            {
                if (srs[i] != null)
                {
                    origColors[i] = srs[i].color;
                    srs[i].color = Color.white;
                }
            }

            yield return new WaitForSeconds(0.07f);

            for (int i = 0; i < srs.Length; i++)
            {
                if (srs[i] != null)
                {
                    srs[i].color = origColors[i];
                }
            }
        }

        private IEnumerator BlowFireBallFlightRoutine(GameObject ball, float direction, int damage, int level)
        {
            if (ball == null) yield break;

            Hitbox hb = ball.GetComponent<Hitbox>();
            bool hitLanded = false;
            if (hb != null)
            {
                hb.damage = damage;
                hb.OnHitLanded += (hurtbox, dmg) =>
                {
                    hitLanded = true;
                };
            }

            SpriteRenderer[] srs = ball.GetComponentsInChildren<SpriteRenderer>();
            Color[] initialColors = new Color[srs.Length];
            for (int i = 0; i < srs.Length; i++)
            {
                initialColors[i] = srs[i].color;
            }

            float elapsed = 0f;

            // 約1.0秒間勢いよく吹き込んで前進
            while (elapsed < specialFlightDuration && !hitLanded)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / specialFlightDuration;

                // 直進移動
                ball.transform.position += new Vector3(direction * specialSpeed * Time.deltaTime, 0f, 0f);

                // 飛翔中の激しい炎の揺らぎ・脈動
                float pulse = 1f + Mathf.Sin(Time.time * 26f) * 0.05f;
                ball.transform.localScale = new Vector3(pulse, pulse, 1f);

                // 最後の0.2秒間（0.8秒〜1.0秒）で徐々に燃え尽きるフェードアウト
                if (t > 0.8f)
                {
                    float fade = 1f - ((t - 0.8f) / 0.2f);
                    for (int i = 0; i < srs.Length; i++)
                    {
                        if (srs[i] != null)
                        {
                            srs[i].color = new Color(initialColors[i].r, initialColors[i].g, initialColors[i].b, initialColors[i].a * fade);
                        }
                    }
                }

                yield return null;
            }

            // ヒットした場合：着弾時に大爆発的にパッと弾けて消滅
            if (hitLanded && ball != null)
            {
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.TriggerScreenShake(0.08f + level * 0.04f, 0.05f + level * 0.03f);
                    HUDManager.Instance.TriggerHitStop(4 + level * 2);
                }

                // 着弾エフェクト: スケールを拡大しながら瞬間的にフェードアウト
                float burstDuration = 0.08f;
                float burstElapsed = 0f;
                Vector3 currentScale = ball.transform.localScale;
                while (burstElapsed < burstDuration && ball != null)
                {
                    burstElapsed += Time.deltaTime;
                    float bt = burstElapsed / burstDuration;
                    ball.transform.localScale = currentScale * (1f + bt * 0.4f);
                    for (int i = 0; i < srs.Length; i++)
                    {
                        if (srs[i] != null)
                        {
                            srs[i].color = new Color(initialColors[i].r, initialColors[i].g, initialColors[i].b, initialColors[i].a * (1f - bt));
                        }
                    }
                    yield return null;
                }
            }

            if (ball != null)
            {
                Destroy(ball);
            }
        }

        private GameObject CreateBigFireBall(int level)
        {
            GameObject ballRoot = new GameObject($"FireBall_Projectile_Lv{level}");

            Sprite circleSp = GetOrCreateCircleSprite();
            float baseRadius = (circleSp != null && circleSp.bounds.extents.x > 0.001f) ? circleSp.bounds.extents.x : 0.32f;

            // レベルごとの目標半径（Lv1: 0.65f, Lv2: 0.95f, Lv3: 1.30f）
            float targetRadius = (level == 3) ? specialRadiusLv3 : (level == 2) ? specialRadiusLv2 : specialRadiusLv1;
            int damage = (level == 3) ? specialDamageLv3 : (level == 2) ? specialDamageLv2 : specialDamageLv1;

            float outerScale = targetRadius / baseRadius;

            // 外層（深紅の大炎）: 見た目の半径が targetRadius とミリ単位で完全一致！
            GameObject outerObj = new GameObject("OuterFlame");
            outerObj.transform.SetParent(ballRoot.transform, false);
            SpriteRenderer outerSr = outerObj.AddComponent<SpriteRenderer>();
            outerSr.sprite = circleSp;
            outerSr.color = new Color(1.0f, 0.18f, 0.05f, 0.95f);
            outerSr.sortingOrder = 15;
            outerObj.transform.localScale = new Vector3(outerScale, outerScale, 1f);

            // 中層（鮮やかな橙色の炎）: 半径比率約75%
            float innerScale = (targetRadius * 0.75f) / baseRadius;
            GameObject innerObj = new GameObject("InnerFlame");
            innerObj.transform.SetParent(ballRoot.transform, false);
            SpriteRenderer innerSr = innerObj.AddComponent<SpriteRenderer>();
            innerSr.sprite = circleSp;
            innerSr.color = new Color(1.0f, 0.58f, 0.08f, 0.95f);
            innerSr.sortingOrder = 16;
            innerObj.transform.localScale = new Vector3(innerScale, innerScale, 1f);

            // コア層（超高温の白熱コア）: 半径比率約45%
            float coreScale = (targetRadius * 0.45f) / baseRadius;
            GameObject coreObj = new GameObject("CoreFlame");
            coreObj.transform.SetParent(ballRoot.transform, false);
            SpriteRenderer coreSr = coreObj.AddComponent<SpriteRenderer>();
            coreSr.sprite = circleSp;
            coreSr.color = new Color(1.0f, 0.96f, 0.6f, 1.0f);
            coreSr.sortingOrder = 17;
            coreObj.transform.localScale = new Vector3(coreScale, coreScale, 1f);

            // 当たり判定用コライダー: ボールの外見サイズ（targetRadius）と完全に一致！
            CircleCollider2D col = ballRoot.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = targetRadius;

            // Hitboxコンポーネント
            Hitbox hitbox = ballRoot.AddComponent<Hitbox>();
            hitbox.damage = damage;
            hitbox.owner = this;
            hitbox.ownerPlayerID = playerID;
            hitbox.isProjectile = true;
            hitbox.isNormalAttack = false;
            hitbox.postureDamageMultiplier = specialPostureMultiplier; // 2.8f

            return ballRoot;
        }

        public override void AttackUltimate()
        {
            // 必殺技は今後実装
        }
    }
}
