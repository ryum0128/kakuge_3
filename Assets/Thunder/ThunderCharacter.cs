using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // =================================================================================
    // 【ThunderCharacter（雷のハンマーを振るうキャラクター）】
    // 体は他キャラクターと同じグレーの箱型。
    // 通常攻撃は大剣（Taiken）のように重厚な雷のハンマーを振りかぶり、
    // 地面へ一気に叩きつけて衝撃波を発生させます。
    // スペシャル攻撃は約3秒間天にハンマーを掲げて雷を溜め、
    // 相手の頭上から大迫力の落雷（ダメージ25）を叩き落とします！
    // ブロック・パリー仕様は他キャラクター（Taiken / Flame）と完全に共通です。
    // =================================================================================
    public class ThunderCharacter : CharacterBase
    {
        [Header("雷のハンマー（Thunder Hammer）設定")]
        [Tooltip("ハンマーの振りかぶり角度（肩上へ高く掲げる）")]
        public float swingStartAngle = 80f;
        [Tooltip("ハンマーの振り下ろし終了角度（地面叩きつけ）")]
        public float swingEndAngle = -95f;
        [Tooltip("振り下ろし時間（秒）")]
        public float swingDuration = 0.16f;
        [Tooltip("構え戻し時間（秒）")]
        public float recoverDuration = 0.22f;

        [Tooltip("通常ハンマー攻撃のダメージ")]
        public int normalHammerDamage = 16;

        [Header("スペシャル攻撃（落雷 / Lightning Strike）設定")]
        [Tooltip("落雷スペシャル攻撃のダメージ")]
        public int specialThunderDamage = 25;
        [Tooltip("落雷チャージ時間（秒）")]
        public float specialChargeDuration = 3.0f;
        [Tooltip("スペシャル攻撃の消費マナ")]
        public float specialManaCost = 25f;

        [Header("ブロック・パリー設定（他キャラと共通仕様）")]
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
        private Coroutine normalAttackCoroutine;
        private Coroutine specialAttackCoroutine;
        private Coroutine parryWindowCoroutine;
        private Coroutine guardBreakCoroutine;

        // ビジュアル階層
        private Transform visualsTransform;
        private Transform hammerPivot;
        private GameObject hammerObj;
        private SpriteRenderer hammerRenderer;
        public GameObject blockShield;
        private Hitbox punchHitbox;
        private GameObject activeReticule;

        private Vector3 defaultHammerPos = new Vector3(0.35f, 0.15f, 0f);
        private Vector3 handPivot = new Vector3(0.2f, 0.15f, 0f);

        // プロシージャル生成スプライト（アセット欠損防止）
        private static Sprite cachedHammerSprite;
        private static Sprite cachedCircleSprite;
        private static Sprite cachedShockwaveSprite;
        private static Sprite cachedBodySprite;
        private static Sprite cachedBoltSprite;
        private static Sprite cachedReticuleSprite;
        private static AudioClip cachedThunderClip;

        protected override void Start()
        {
            base.Start();

            // 1. 体（他と同じグレーの箱）のビジュアル初期化
            SetupBodyVisuals();

            // 2. 雷のハンマーの初期化
            SetupThunderHammer();

            // 3. ブロックシールド（他キャラと完全共通仕様）の初期化
            SetupBlockShield();

            // 4. ヒットボックスの取得・初期化
            punchHitbox = GetComponentInChildren<Hitbox>(true);
            if (punchHitbox != null)
            {
                punchHitbox.damage = normalHammerDamage;
                punchHitbox.ownerPlayerID = playerID;
                punchHitbox.owner = this;
                punchHitbox.gameObject.SetActive(false);
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
                if (activeReticule != null)
                {
                    Destroy(activeReticule);
                    activeReticule = null;
                }
                return;
            }
        }

        public override void Move(float direction)
        {
            // 特殊技チャージ中、またはガードブレイク・死亡・スタン中は移動を停止
            if (isSpecialAttacking || isGuardBroken || isDead || isStunned)
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

        // =========================================================
        // ビジュアルの自動セットアップ
        // =========================================================

        private void SetupBodyVisuals()
        {
            visualsTransform = transform.Find("Visuals");
            if (visualsTransform == null)
            {
                GameObject visObj = new GameObject("Visuals");
                visObj.transform.SetParent(transform, false);
                visObj.transform.localPosition = Vector3.zero;
                visualsTransform = visObj.transform;
            }

            // キャラクター自体は他と同じグレーの箱（1x2 スライス）
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
            bodySr.color = new Color(0.80f, 0.82f, 0.85f, 1.0f); // スタイリッシュなグレー色
            bodySr.sortingOrder = 0;
        }

        private void SetupThunderHammer()
        {
            hammerPivot = visualsTransform.Find("HammerPivot");
            if (hammerPivot == null)
            {
                GameObject pivotObj = new GameObject("HammerPivot");
                pivotObj.transform.SetParent(visualsTransform, false);
                pivotObj.transform.localPosition = handPivot;
                hammerPivot = pivotObj.transform;
            }

            Transform hammerTrans = hammerPivot.Find("ThunderHammer");
            if (hammerTrans == null)
            {
                hammerObj = new GameObject("ThunderHammer");
                hammerObj.transform.SetParent(hammerPivot, false);
                // 柄がピボットに位置し、槌頭が前方上に突き出る配置
                hammerObj.transform.localPosition = new Vector3(0.35f, 0.45f, 0f);
                hammerTrans = hammerObj.transform;
            }
            else
            {
                hammerObj = hammerTrans.gameObject;
            }

            hammerRenderer = hammerObj.GetComponent<SpriteRenderer>();
            if (hammerRenderer == null)
            {
                hammerRenderer = hammerObj.AddComponent<SpriteRenderer>();
            }

            hammerRenderer.sprite = GetThunderHammerSprite();
            hammerRenderer.sortingOrder = 5;
            hammerRenderer.color = Color.white;

            // 初期位置と角度（自然に構える約25度前傾）
            hammerPivot.localRotation = Quaternion.Euler(0, 0, 25f);
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
        // 通常攻撃: 【雷槌スマッシュ（大剣風の重厚な振り下ろし）】
        // =========================================================

        public override void AttackNormal()
        {
            if (isDead || isSwinging || isSpecialAttacking || isBlocking || isGuardBroken || isDashingOrEvading || IsHurtLocked) return;

            Debug.Log("[Thunder] 雷槌スマッシュ通常攻撃 発動！");
            if (animator != null) animator.SetTrigger("AttackNormal");

            if (normalAttackCoroutine != null) StopCoroutine(normalAttackCoroutine);
            normalAttackCoroutine = StartCoroutine(ThunderSwingRoutine());
        }

        private IEnumerator ThunderSwingRoutine()
        {
            isSwinging = true;

            // 1. 予備動作（ウィンドアップ）: 肩上に高く掲げ、雷をチャージ (+80度) (0.22秒)
            float windupDuration = 0.22f;
            float elapsed = 0f;
            Quaternion startRot = hammerPivot.localRotation;
            Quaternion windupRot = Quaternion.Euler(0, 0, swingStartAngle);

            while (elapsed < windupDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / windupDuration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f);

                hammerPivot.localRotation = Quaternion.Lerp(startRot, windupRot, t);

                if (hammerRenderer != null)
                {
                    hammerRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.95f, 0.4f, 1f), t);
                }
                yield return null;
            }

            // 2. 振り下ろしフェーズ: 地面に向かって超大弧一気スマッシュ (-95度) (0.16秒)
            elapsed = 0f;
            Quaternion strikeRot = Quaternion.Euler(0, 0, swingEndAngle);
            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / swingDuration;
                t = t * t; // 加速して地面へ叩きつける！

                hammerPivot.localRotation = Quaternion.Lerp(windupRot, strikeRot, t);

                if (hammerRenderer != null)
                {
                    hammerRenderer.color = Color.Lerp(new Color(1f, 0.95f, 0.4f, 1f), new Color(0.8f, 1f, 1f, 1f), t);
                }
                yield return null;
            }
            hammerPivot.localRotation = strikeRot;

            // 3. 【着地インパクト】雷光ショックウェーブ＆攻撃判定（Hitbox）の有効化！
            float activeHitboxDuration = 0.18f;
            if (punchHitbox != null)
            {
                punchHitbox.damage = normalHammerDamage;
                punchHitbox.transform.localPosition = new Vector3(1.6f, -0.4f, 0f); // 地面叩きつけ地点
                punchHitbox.postureDamageMultiplier = 1.5f;
                punchHitbox.isNormalAttack = true;

                BoxCollider2D col = punchHitbox.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    col.size = new Vector2(2.4f, 1.2f); // 横方向にも雷が走る広範囲判定
                }
                StartCoroutine(ActivateHitboxTemporarily(punchHitbox.gameObject, activeHitboxDuration));
            }

            // 地面に雷の衝撃波エフェクトを発生
            SpawnGroundShockwave(transform.position + new Vector3(transform.localScale.x * 1.5f, -0.6f, 0f), 1.0f);

            yield return new WaitForSeconds(activeHitboxDuration);

            // 4. 回収（フォロースルー・構え戻し） (0.22秒)
            elapsed = 0f;
            Quaternion readyRot = Quaternion.Euler(0, 0, 25f);
            while (elapsed < recoverDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / recoverDuration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f);

                hammerPivot.localRotation = Quaternion.Lerp(strikeRot, readyRot, t);

                if (hammerRenderer != null)
                {
                    hammerRenderer.color = Color.Lerp(new Color(0.8f, 1f, 1f, 1f), Color.white, t);
                }
                yield return null;
            }

            hammerPivot.localRotation = readyRot;
            if (hammerRenderer != null) hammerRenderer.color = Color.white;

            isSwinging = false;
        }

        // =========================================================
        // スペシャル攻撃: 【天雷招来（3秒チャージ落雷 / Lightning Strike）】
        // 相手の足元に電撃の照準（レティクル）をロックし、3秒後に天から大落雷を直撃させる！
        // ダメージ: 25, 消費マナ: 25
        // =========================================================

        private bool IsChargeKeyHeld()
        {
            // PlayerInputController 経由
            PlayerInputController pic = GetComponent<PlayerInputController>();
            if (pic != null && pic.IsSpecialAttackHeld()) return true;

            // GamepadInputController 経由
            GamepadInputController gic = GetComponent<GamepadInputController>();
            if (gic != null && gic.IsSpecialAttackHeld()) return true;

            // ゲームパッド直接判定
            if (UnityEngine.InputSystem.Gamepad.all.Count > 0)
            {
                int padIdx = (playerID == 2) ? 1 : 0;
                if (padIdx < UnityEngine.InputSystem.Gamepad.all.Count)
                {
                    var pad = UnityEngine.InputSystem.Gamepad.all[padIdx];
                    if (pad.buttonNorth.isPressed) return true; // Yボタン
                }
            }

            // キーボード直接判定フォールバック
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (playerID == 1)
                {
                    if (kb[UnityEngine.InputSystem.Key.K].isPressed || kb[UnityEngine.InputSystem.Key.L].isPressed) return true;
                }
                else
                {
                    if (kb[UnityEngine.InputSystem.Key.I].isPressed || kb[UnityEngine.InputSystem.Key.O].isPressed || kb[UnityEngine.InputSystem.Key.Numpad3].isPressed || kb[UnityEngine.InputSystem.Key.Numpad2].isPressed) return true;
                }
            }

            return false;
        }

        public override bool IsInParryWindow() => isParrying;
        public override bool IsInGuardBreak() => isGuardBroken;
        public override float GetParryCooldownRemaining()
        {
            float elapsed = Time.time - lastParryTime;
            if (elapsed < parryCooldown) return parryCooldown - elapsed;
            return 0f;
        }

        public override void AttackSpecial()
        {
            if (isDead || isSwinging || isSpecialAttacking || isBlocking || isGuardBroken || isStunned || isDashingOrEvading || IsHurtLocked) return;

            if (IsSpecialSilenced())
            {
                Debug.Log("[Thunder] 影の領域（沈黙フィールド）内にいるため、スペシャル攻撃を使用できません！");
                return;
            }

            if (currentMana < specialManaCost)
            {
                Debug.Log($"[Thunder] マナが足りません！ (必要マナ: {specialManaCost}, 現在マナ: {currentMana})");
                return;
            }

            // マナ消費はチャージ完了時（発射時）に行うため、離してキャンセルした場合はマナが減りません
            if (animator != null) animator.SetTrigger("AttackSpecial");

            if (specialAttackCoroutine != null) StopCoroutine(specialAttackCoroutine);
            specialAttackCoroutine = StartCoroutine(ThunderStrikeSpecialRoutine());
        }

        private IEnumerator ThunderStrikeSpecialRoutine()
        {
            isSpecialAttacking = true;
            Debug.Log("[Thunder] スペシャル攻撃: 長押しチャージ開始（最大3秒、離すとキャンセル）！");

            CharacterBase opponent = FindOpponent();
            Vector3 targetPos = (opponent != null) ? opponent.transform.position : transform.position + new Vector3(transform.localScale.x * 4.5f, 0f, 0f);

            // 相手の位置に電撃照準（レティクル）を生成
            if (activeReticule != null) Destroy(activeReticule);
            activeReticule = new GameObject("ThunderTargetReticule");
            activeReticule.transform.position = targetPos;
            SpriteRenderer reticuleSr = activeReticule.AddComponent<SpriteRenderer>();
            reticuleSr.sprite = GetTargetReticuleSprite();
            reticuleSr.color = new Color(0.2f, 0.85f, 1f, 0.5f);
            reticuleSr.sortingOrder = 8;
            activeReticule.transform.localScale = new Vector3(2.0f, 0.8f, 1f);

            // 1. チャージフェーズ（約3秒間長押し、離したらキャンセル）
            Quaternion readyRot = hammerPivot.localRotation;
            Quaternion skyRot = Quaternion.Euler(0, 0, 115f); // 天空へ高く掲げる

            float chargeTimer = 0f;
            float lockTime = 2.5f; // 2.5秒までは相手を追従。残り0.5秒で位置を確定して回避のチャンスを与える

            while (chargeTimer < specialChargeDuration)
            {
                chargeTimer += Time.deltaTime;
                float progress = chargeTimer / specialChargeDuration;

                // 長押し判定（SandbagAIは自動完遂、プレイヤーはキーを離したらキャンセル）
                bool keyHeld = (GetComponent<SandbagAI>() != null) || IsChargeKeyHeld();

                // 入力開始直後（0.08秒）以降にキーが離されたら即座にキャンセル！
                if (!keyHeld && chargeTimer > 0.08f)
                {
                    Debug.Log("[Thunder] ボタンが離されたため、スペシャル攻撃のチャージをキャンセルしました。");
                    CancelAttacks();
                    yield break;
                }

                // ハンマーを天に掲げる
                hammerPivot.localRotation = Quaternion.Lerp(readyRot, skyRot, Mathf.Clamp01(chargeTimer / 0.35f));

                // チャージの進み具合に応じてハンマーが黄金〜超帯電シアンに激しく明滅
                if (hammerRenderer != null)
                {
                    float pulse = Mathf.Sin(chargeTimer * (10f + progress * 20f));
                    Color chargeColor = Color.Lerp(new Color(1f, 0.9f, 0.2f, 1f), new Color(0.4f, 1f, 1f, 1f), progress);
                    hammerRenderer.color = Color.Lerp(chargeColor, Color.white, Mathf.Abs(pulse));
                }

                // 照準の更新（2.5秒までは相手を追尾）
                if (chargeTimer < lockTime && opponent != null && !opponent.isDead)
                {
                    targetPos = opponent.transform.position;
                    if (activeReticule != null)
                    {
                        activeReticule.transform.position = targetPos;
                    }
                }

                // 照準の脈動＆回転
                if (activeReticule != null && reticuleSr != null)
                {
                    float retPulse = 1f + Mathf.Sin(chargeTimer * (12f + progress * 16f)) * 0.15f;
                    activeReticule.transform.localScale = new Vector3(2.0f * retPulse, 0.8f * retPulse, 1f);
                    reticuleSr.color = (chargeTimer >= lockTime) ? new Color(1f, 0.25f, 0.25f, 0.95f) : new Color(0.2f, 0.85f, 1f, 0.5f + progress * 0.45f);
                }

                // チャージ音・画面振動演出
                if (chargeTimer > 1.0f && chargeTimer - Time.deltaTime <= 1.0f)
                {
                    if (HUDManager.Instance != null) HUDManager.Instance.TriggerScreenShake(0.04f, 0.03f);
                }
                if (chargeTimer > 2.0f && chargeTimer - Time.deltaTime <= 2.0f)
                {
                    if (HUDManager.Instance != null) HUDManager.Instance.TriggerScreenShake(0.07f, 0.04f);
                }
                if (chargeTimer > 2.7f && chargeTimer - Time.deltaTime <= 2.7f)
                {
                    if (HUDManager.Instance != null) HUDManager.Instance.TriggerScreenShake(0.12f, 0.06f);
                }

                yield return null;
            }

            // 3秒間のチャージ完了！ここでマナを消費
            currentMana -= specialManaCost;
            if (currentMana < 0f) currentMana = 0f;

            // 2. 発射フェーズ: ハンマーを一気に振り下ろし、相手の地点へ天から極大落雷！
            if (activeReticule != null)
            {
                targetPos = activeReticule.transform.position;
                Destroy(activeReticule);
                activeReticule = null;
            }

            // ハンマー振り下ろし
            Quaternion slamRot = Quaternion.Euler(0, 0, -35f);
            hammerPivot.localRotation = slamRot;
            if (hammerRenderer != null) hammerRenderer.color = Color.white;

            // 画面大シェイク＆落雷轟音SE
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.35f, 0.25f);
            }
            PlayThunderSound();

            // 天空から相手の位置へ極大落雷（確実にダメージ25）を投下！
            SpawnLightningBolt(targetPos);

            yield return new WaitForSeconds(0.24f);

            // 3. 構え戻し（フォロースルー）
            float recoverTime = 0.28f;
            float rElapsed = 0f;
            while (rElapsed < recoverTime)
            {
                rElapsed += Time.deltaTime;
                float t = rElapsed / recoverTime;
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                hammerPivot.localRotation = Quaternion.Lerp(slamRot, readyRot, t);
                yield return null;
            }

            hammerPivot.localRotation = readyRot;
            isSpecialAttacking = false;
            Debug.Log("[Thunder] スペシャル攻撃: 天雷招来 完了！");
        }

        /// <summary>
        /// 指定位置（相手の地点）に天空から落雷ピラーを召喚（確実に25ダメージ）
        /// </summary>
        private void SpawnLightningBolt(Vector3 strikePos)
        {
            GameObject bolt = new GameObject("ThunderBoltStrike");
            // 初期化中にOnEnableが走ってデフォルトダメージ(10)で即時判定されるのを防ぐため、非アクティブで作成！
            bolt.SetActive(false);

            // 天空上空（+6f）から地面（strikePos.y）まで伸びる長大なピラー
            bolt.transform.position = new Vector3(strikePos.x, strikePos.y + 5.5f, 0f);
            bolt.transform.localScale = new Vector3(1.6f, 13f, 1f);

            SpriteRenderer sr = bolt.AddComponent<SpriteRenderer>();
            sr.sprite = GetLightningBoltSprite();
            sr.color = Color.white;
            sr.sortingOrder = 12;

            // ヒットボックス（確実にダメージ25、体幹削り2.5倍）
            BoxCollider2D col = bolt.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.2f, 1.0f);

            Hitbox hb = bolt.AddComponent<Hitbox>();
            hb.damage = specialThunderDamage; // 確実に25ダメージを設定
            hb.useDistanceScaling = false;    // 距離減衰なし（先端でも至近でも確実に25ダメージ）
            hb.useTimeScaling = false;        // 時間減衰なし（常に25ダメージ）
            hb.postureDamageMultiplier = 2.5f;
            hb.isNormalAttack = false;
            hb.ownerPlayerID = playerID;
            hb.owner = this;

            // 全ての設定を終えてからアクティブ化し、確実に25ダメージで判定させる！
            bolt.SetActive(true);

            // 地面インパクト雷撃破
            SpawnGroundShockwave(new Vector3(strikePos.x, strikePos.y - 0.5f, 0f), 1.8f);

            StartCoroutine(FadeAndDestroyBolt(bolt, 0.24f));
        }

        private IEnumerator FadeAndDestroyBolt(GameObject bolt, float duration)
        {
            float elapsed = 0f;
            SpriteRenderer sr = bolt.GetComponent<SpriteRenderer>();

            while (elapsed < duration && bolt != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (sr != null)
                {
                    // 明滅しながらフェードアウト
                    float flash = (Mathf.Sin(elapsed * 60f) > 0f) ? 1.0f : 0.7f;
                    sr.color = new Color(1f, 1f, 1f, (1f - t) * flash);
                }
                yield return null;
            }

            if (bolt != null) Destroy(bolt);
        }

        private CharacterBase FindOpponent()
        {
            CharacterBase[] allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (CharacterBase c in allChars)
            {
                if (c != this && !c.isDead)
                {
                    return c;
                }
            }
            return null;
        }

        private void SpawnGroundShockwave(Vector3 position, float scale)
        {
            GameObject shock = new GameObject("GroundShockwave");
            shock.transform.position = position;
            shock.transform.localScale = new Vector3(scale * 1.5f, scale * 0.8f, 1f);

            SpriteRenderer sr = shock.AddComponent<SpriteRenderer>();
            sr.sprite = GetShockwaveSprite();
            sr.color = new Color(1f, 0.95f, 0.3f, 0.9f);
            sr.sortingOrder = 7;

            StartCoroutine(FadeAndDestroy(shock, 0.28f));
        }

        private IEnumerator FadeAndDestroy(GameObject obj, float duration)
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
                    sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);
                }
                obj.transform.localScale = origScale * (1f + t * 0.5f);
                yield return null;
            }

            if (obj != null) Destroy(obj);
        }

        // =========================================================
        // ブロック＆パリー処理（全キャラクター共通仕様）
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
                Debug.Log("Block start ignored (Parry on Cooldown - Shield disabled).");
                return;
            }

            isBlocking = true;
            isBlockingState = true;
            isParrying = true;
            lastParryTime = Time.time;
            Debug.Log("Block start (Parry Window Active).");

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
            Debug.Log("Block stop.");

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
                Debug.Log("Parry window closed.");

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
        // 被ダメージ＆パリー判定（全キャラクター共通仕様）
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
                    Debug.Log("★ PARRY SUCCESS! (DeepWoken Style) ★");
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
                    Debug.Log("Block success. Posture accumulating.");
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
                        StartCoroutine(BlockHitRoutine());
                    }
                }
                return; // ダメージ無効化
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

            CancelAttacks();
            base.TakeDamage(damage, attackerHitbox);

            if (isDead && blockShield != null)
            {
                blockShield.SetActive(false);
            }
        }

        private IEnumerator ParrySuccessRoutine()
        {
            SpriteRenderer bodySr = visualsTransform != null ? visualsTransform.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
            Color originalColor = bodySr != null ? bodySr.color : new Color(0.80f, 0.82f, 0.85f, 1.0f);
            if (bodySr != null)
            {
                bodySr.color = Color.white; // 体を白くフラッシュ
            }

            if (blockShield != null)
            {
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr != null)
                {
                    shieldSr.color = new Color(1f, 1f, 1f, 0.9f); // ピュアホワイト閃光
                }
            }

            yield return new WaitForSeconds(0.1f);

            if (bodySr != null)
            {
                bodySr.color = originalColor;
            }

            if (blockShield != null && isBlocking)
            {
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr != null)
                {
                    shieldSr.color = isParrying ? new Color(0.3f, 0.8f, 1f, 0.6f) : new Color(0.3f, 0.5f, 0.7f, 0.45f);
                }
            }
        }

        private IEnumerator BlockHitRoutine()
        {
            if (blockShield != null)
            {
                SpriteRenderer shieldSr = blockShield.GetComponent<SpriteRenderer>();
                if (shieldSr != null)
                {
                    shieldSr.color = new Color(0.6f, 0.8f, 1f, 0.8f);
                    yield return new WaitForSeconds(0.06f);
                    if (isBlocking)
                    {
                        shieldSr.color = isParrying ? new Color(0.3f, 0.8f, 1f, 0.6f) : new Color(0.3f, 0.5f, 0.7f, 0.45f);
                    }
                }
            }
        }

        private IEnumerator GuardBreakRoutine()
        {
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerHitStop(2);
            }

            isGuardBroken = true;
            isBlocking = false;
            isBlockingState = false;
            isParrying = false;

            Debug.Log("!! GUARD BREAK !!");

            // シールドを赤くフラッシュさせて割る
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

            // キャラクターをオレンジ赤色にしてスタン
            SpriteRenderer bodySr = visualsTransform != null ? visualsTransform.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
            Color originalColor = bodySr != null ? bodySr.color : new Color(0.80f, 0.82f, 0.85f, 1.0f);
            if (bodySr != null)
            {
                bodySr.color = new Color(0.9f, 0.3f, 0.1f, 1f);
            }

            yield return new WaitForSeconds(guardBreakDuration);

            // 回復
            if (bodySr != null)
            {
                bodySr.color = originalColor;
            }
            currentPosture = 0f;
            isGuardBroken = false;
            Debug.Log("Guard Break Stun finished.");
        }

        public void CancelAttacks()
        {
            if (normalAttackCoroutine != null) StopCoroutine(normalAttackCoroutine);
            if (specialAttackCoroutine != null) StopCoroutine(specialAttackCoroutine);
            isSwinging = false;
            isSpecialAttacking = false;
            if (hammerPivot != null) hammerPivot.localRotation = Quaternion.Euler(0, 0, 25f);
            if (hammerRenderer != null) hammerRenderer.color = Color.white;
            if (punchHitbox != null) punchHitbox.gameObject.SetActive(false);
            if (activeReticule != null)
            {
                Destroy(activeReticule);
                activeReticule = null;
            }
        }

        public override void ResetActionStates()
        {
            base.ResetActionStates();
            CancelAttacks();
            StopBlock();
            currentPosture = 0f;
            isGuardBroken = false;
            isBlocking = false;
            isParrying = false;
            isBlockingState = false;
            if (blockShield != null) blockShield.SetActive(false);
        }

        // =========================================================
        // プロシージャル生成（テクスチャ・スプライト・オーディオ）
        // =========================================================

        private static Sprite GetDefaultBodySprite()
        {
            if (cachedBodySprite != null) return cachedBodySprite;
            int w = 32, h = 64;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color fill = new Color(0.9f, 0.9f, 0.92f, 1f);
            Color border = new Color(0.4f, 0.42f, 0.46f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = (x < 2 || x >= w - 2 || y < 2 || y >= h - 2);
                    tex.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            tex.Apply();
            cachedBodySprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
            return cachedBodySprite;
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
            cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
            return cachedCircleSprite;
        }

        public static Sprite GetThunderHammerSprite()
        {
            if (cachedHammerSprite != null) return cachedHammerSprite;

            int w = 64, h = 64;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color empty = new Color(0, 0, 0, 0);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, empty);
                }
            }

            // 柄（Handle）
            for (int y = 4; y < 42; y++)
            {
                for (int x = 14; x <= 18; x++)
                {
                    bool isGrip = (y % 6 == 0 || y % 6 == 1);
                    Color col = isGrip ? new Color(0.78f, 0.58f, 0.2f, 1f) : new Color(0.35f, 0.38f, 0.45f, 1f);
                    tex.SetPixel(x, y, col);
                }
            }

            // ハンマー頭部（Mighty Hammer Head）
            Color steelDark = new Color(0.18f, 0.20f, 0.26f, 1f);
            Color goldTrim = new Color(0.95f, 0.78f, 0.22f, 1f);
            Color lightningCyan = new Color(0.3f, 0.95f, 1f, 1f);

            for (int y = 36; y <= 58; y++)
            {
                for (int x = 6; x <= 56; x++)
                {
                    bool isBorder = (x <= 8 || x >= 54 || y <= 37 || y >= 57);
                    tex.SetPixel(x, y, isBorder ? goldTrim : steelDark);
                }
            }

            // 中央の稲妻マーク（Lightning Bolt Rune）
            int[,] bolt = {
                {48, 30}, {49, 31}, {50, 31}, {51, 32},
                {45, 33}, {46, 33}, {47, 34},
                {42, 35}, {43, 35}, {44, 36},
                {45, 32}, {46, 31},
                {40, 30}, {41, 31}, {42, 31}
            };
            for (int i = 0; i < bolt.GetLength(0); i++)
            {
                int by = bolt[i, 0];
                int bx = bolt[i, 1];
                if (bx >= 0 && bx < w && by >= 0 && by < h)
                {
                    tex.SetPixel(bx, by, lightningCyan);
                    tex.SetPixel(bx + 1, by, Color.white);
                }
            }

            tex.Apply();
            cachedHammerSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.25f, 0.15f), 32f);
            return cachedHammerSprite;
        }

        private static Sprite GetLightningBoltSprite()
        {
            if (cachedBoltSprite != null) return cachedBoltSprite;

            int w = 32, h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color cyanOuter = new Color(0.2f, 0.85f, 1f, 0.6f);
            Color cyanInner = new Color(0.7f, 0.95f, 1f, 0.95f);

            float center = (w - 1) / 2f;

            for (int y = 0; y < h; y++)
            {
                // ジグザグの稲妻コアライン
                float offset = Mathf.Sin(y * 0.25f) * 3.5f + Mathf.Cos(y * 0.65f) * 2.0f;
                float coreX = center + offset;

                for (int x = 0; x < w; x++)
                {
                    float dist = Mathf.Abs(x - coreX);
                    if (dist < 1.5f)
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                    else if (dist < 4.0f)
                    {
                        tex.SetPixel(x, y, cyanInner);
                    }
                    else if (dist < 8.0f)
                    {
                        float alpha = (1.0f - (dist - 4.0f) / 4.0f) * 0.65f;
                        tex.SetPixel(x, y, new Color(0.2f, 0.85f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            cachedBoltSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            return cachedBoltSprite;
        }

        private static Sprite GetTargetReticuleSprite()
        {
            if (cachedReticuleSprite != null) return cachedReticuleSprite;

            int w = 64, h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float cx = (w - 1) / 2f;
            float cy = (h - 1) / 2f;
            float rx = w / 2f - 2f;
            float ry = h / 2f - 2f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / rx;
                    float dy = (y - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    // リング描画（d = 0.75〜1.0）
                    if (d >= 0.72f && d <= 1.0f)
                    {
                        float edgeDist = Mathf.Abs(d - 0.86f);
                        float a = Mathf.Clamp01(1f - edgeDist * 6f);
                        tex.SetPixel(x, y, new Color(0.3f, 0.9f, 1f, a));
                    }
                    // 十字スリット
                    else if ((Mathf.Abs(x - cx) < 1.5f && d < 1.1f) || (Mathf.Abs(y - cy) < 1.5f && d < 1.1f))
                    {
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.85f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            cachedReticuleSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            return cachedReticuleSprite;
        }

        private static Sprite GetShockwaveSprite()
        {
            if (cachedShockwaveSprite != null) return cachedShockwaveSprite;
            int w = 64, h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float normX = (float)x / w;
                    float normY = (float)y / h;
                    float spike = Mathf.Abs(Mathf.Sin(normX * Mathf.PI * 4f));
                    if (normY < spike * 0.9f)
                    {
                        tex.SetPixel(x, y, new Color(1f, 0.95f, 0.3f, 0.85f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            cachedShockwaveSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.1f), 32f);
            return cachedShockwaveSprite;
        }

        private static AudioClip GetThunderAudioClip()
        {
            if (cachedThunderClip != null) return cachedThunderClip;
            int sampleRate = 44100;
            float duration = 0.85f;
            int count = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[count];
            System.Random rand = new System.Random(888);

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                // 初動の激しい放電クラックル (0 - 0.08s)
                float crackle = ((float)rand.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 35f) * 0.7f;
                // 重低音の轟音ランブル (50-90Hz)
                float freq = 80f * Mathf.Exp(-t * 2.5f) + 45f;
                float rumble = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 3.8f) * 0.8f;
                // サブノイズ
                float subNoise = ((float)rand.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 4.5f) * 0.35f;

                samples[i] = Mathf.Clamp(crackle + rumble + subNoise, -1f, 1f);
            }

            cachedThunderClip = AudioClip.Create("ThunderStrikeSE", count, 1, sampleRate, false);
            cachedThunderClip.SetData(samples, 0);
            return cachedThunderClip;
        }

        private void PlayThunderSound()
        {
            AudioClip clip = GetThunderAudioClip();
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : transform.position, 1.0f);
            }
        }
    }
}
