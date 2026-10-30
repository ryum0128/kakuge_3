using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // =================================================================================
    // 【GannrannsuCharacter（ガンランスキャラクター）】
    // CharacterBase（お手本）を継承し、ガンランス特有の能力を実装しています。
    // 特徴：
    //   ・低空ホバリング（軽やかな浮遊挙動）
    //   ・通常攻撃：槍の突き（LanceHitbox、前方にスラスト）
    //   ・特殊攻撃：砲撃発射（GannrannsuShell）
    //   ・必殺技：竜撃砲（DragonBlastProjectile、大ダメージ＋後方反動）
    //   ・ブロック／パリィ、ダッシュ／回避、スタンスキルも完全対応！
    // =================================================================================
    public class GannrannsuCharacter : CharacterBase
    {
        [Header("===== ガンランス専用設定 =====")]

        [Tooltip("砲弾のプレハブ（飛ぶ弾）")]
        public GameObject shellPrefab;

        [Tooltip("砲弾が出る位置（槍の先端）")]
        public Transform firePoint;

        [Tooltip("竜撃砲用の攻撃オブジェクト")]
        public GameObject dragonBlastHitbox;

        [Tooltip("砲弾の飛行速度")]
        public float shellSpeed = 12f;

        [Tooltip("砲弾のダメージ")]
        public int shellDamage = 15;

        [Tooltip("竜撃砲のダメージ")]
        public int dragonBlastDamage = 40;

        [Tooltip("竜撃砲発射時の後方への反動力")]
        public float dragonBlastRecoil = 8f;

        [Tooltip("浮力")]
        public float hoverForce = 15f;

        [Tooltip("浮遊する高さの上限")]
        public float maxHoverHeight = 2.5f;

        [Header("===== 竜撃砲チャージ設定 =====")]
        public float chargeRequiredTime = 1.5f;
        public bool isCharging { get; private set; }

        [Header("===== 竜撃砲ゲージ設定 =====")]
        public float maxChargeGauge = 100f;
        public float chargeGauge = 0f;
        public float passiveChargeRate = 5f;
        public float normalHitChargeGain = 10f;
        public float specialHitChargeGain = 20f;

        [Header("===== 竜撃砲クールタイム設定 =====")]
        public float ultimateAttackCooldown = 3.0f;
        public float ultimateAttackCooldownTimer { get; private set; } = 0f;

        [Header("===== レーザーエフェクト設定 =====")]
        public float laserDuration = 0.5f;
        public float laserWidth = 0.25f;
        public float laserRange = 20f;

        // --- GUI用テクスチャとスタイル ---
        private Texture2D bgTex;
        private Texture2D fillTex;
        private GUIStyle textStyle;

        private Rigidbody2D myRb;
        private Animator myAnimator;
        private Transform visualsTransform;
        private Vector3 originalVisualsPos;
        private Coroutine activeVisualsAnimCoroutine;

        private bool isHovering = true;
        private bool HasAnimator => myAnimator != null && myAnimator.runtimeAnimatorController != null;
        private bool isAttacking = false;

        // 攻撃中判定のオーバーライド（CharacterBase や AI と正しく連携）
        public override bool IsAttacking => isAttacking;

        void Awake()
        {
            myRb = GetComponent<Rigidbody2D>();
            // 親クラスの参照を確実にセット（Start前のNullReference防止）
            rb = myRb;
            mainCollider = GetComponent<Collider2D>();
            myAnimator = GetComponentInChildren<Animator>();
            visualsTransform = transform.Find("Visuals");
            if (visualsTransform != null)
            {
                originalVisualsPos = visualsTransform.localPosition;
            }

            if (myRb != null)
            {
                myRb.gravityScale = 1.0f;
            }
        }

        protected override void Start()
        {
            base.Start();

            // 親クラス Start で rb が再取得された後も重力スケールを確実に維持
            if (rb != null)
            {
                rb.gravityScale = 1.0f;
            }

            // 通常攻撃用Hitboxのヒット時コールバック登録
            Hitbox[] hitboxes = GetComponentsInChildren<Hitbox>(true);
            foreach (var hb in hitboxes)
            {
                if (hb.gameObject.name == "LanceHitbox")
                {
                    hb.OnHitLanded += (hurtbox, dmg) =>
                    {
                        AddCharge(normalHitChargeGain);
                    };
                }
            }
        }

        public override void ResetActionStates()
        {
            base.ResetActionStates();
            isAttacking = false;
            if (isCharging)
            {
                CancelCharge();
            }
            if (activeVisualsAnimCoroutine != null)
            {
                StopCoroutine(activeVisualsAnimCoroutine);
                activeVisualsAnimCoroutine = null;
            }
            if (visualsTransform != null)
            {
                visualsTransform.localPosition = originalVisualsPos;
            }
        }

        protected override void Update()
        {
            if (isDead || (GameManager.Instance != null && !GameManager.Instance.IsPlaying)) return;

            // CharacterBase.Update（スタンゲージ加算、基本処理など）を実行
            base.Update();

            // --- 浮遊処理 ---
            if (isHovering && transform.position.y < maxHoverHeight && myRb != null)
            {
                myRb.AddForce(Vector2.up * hoverForce * Time.deltaTime, ForceMode2D.Force);
            }

            // 接地判定：地面とコライダーが接触しているか、または地面近くで安定している場合はジャンプ可能に
            bool physicalGrounded = isGrounded; // base.Update() 内で CheckGrounded() された結果
            bool nearFloor = transform.position.y <= (maxHoverHeight + 0.3f) && (myRb != null && Mathf.Abs(myRb.linearVelocity.y) < 2.5f);
            isGrounded = physicalGrounded || nearFloor;

            if (HasAnimator)
            {
                myAnimator.SetBool("IsHovering", isHovering);
                myAnimator.SetBool("IsGrounded", isGrounded);
            }

            // --- チャージゲージの自然増加 ---
            AddCharge(passiveChargeRate * Time.deltaTime);

            // --- クールタイムタイマーの更新 ---
            if (ultimateAttackCooldownTimer > 0f)
            {
                ultimateAttackCooldownTimer = Mathf.Max(0f, ultimateAttackCooldownTimer - Time.deltaTime);
            }
        }

        // 移動処理
        public override void Move(float direction)
        {
            if (isDead || isStunned) return;

            if (isCharging)
            {
                if (myRb != null) myRb.linearVelocity = new Vector2(0f, myRb.linearVelocity.y);
                if (HasAnimator) myAnimator.SetFloat("Speed", 0f);
                return;
            }

            base.Move(direction);
        }

        // ジャンプ処理
        public override void Jump()
        {
            if (isDead || isCharging || isStunned) return;

            // ガンランスは浮遊するため接地フラグが安定しにくいので、確実にジャンプできるようにする
            if (!isGrounded && transform.position.y > maxHoverHeight + 0.5f) return;

            isGrounded = false;
            float force = stats != null ? stats.jumpForce : 10f;
            if (myRb != null)
            {
                myRb.linearVelocity = new Vector2(myRb.linearVelocity.x, force);
            }
            else if (rb != null)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
            }

            if (HasAnimator) myAnimator.SetTrigger("Jump");
        }

        // チャージ制御
        public void StartCharge()
        {
            if (isDead) return;
            isCharging = true;
            if (HasAnimator) myAnimator.SetTrigger("StartCharge");
        }

        public void CancelCharge()
        {
            if (!isCharging || isDead) return;
            isCharging = false;
            if (HasAnimator) myAnimator.SetTrigger("CancelCharge");
        }

        public void CompleteCharge()
        {
            if (!isCharging || isDead) return;
            isCharging = false;
            AttackUltimate();
        }

        // チャージゲージ増加
        public void AddCharge(float amount)
        {
            if (isDead) return;
            chargeGauge = Mathf.Clamp(chargeGauge + amount, 0f, maxChargeGauge);
        }

        private IEnumerator SetAttackingState(float duration)
        {
            isAttacking = true;
            yield return new WaitForSeconds(duration);
            isAttacking = false;
        }

        // =========================================================
        // 通常攻撃: 槍の突き（近接スラスト攻撃）
        // =========================================================
        public override void AttackNormal()
        {
            if (isDead || isAttacking || isStunned || IsHurtLocked) return;

            if (HasAnimator) myAnimator.SetTrigger("AttackNormal");

            // 攻撃硬直 0.22秒
            StartCoroutine(SetAttackingState(0.22f));

            // プロシージャルな突きアニメーション（槍を前に突き出して戻す）
            if (visualsTransform != null)
            {
                if (activeVisualsAnimCoroutine != null) StopCoroutine(activeVisualsAnimCoroutine);
                activeVisualsAnimCoroutine = StartCoroutine(AnimateThrust(0.22f));
            }

            // LanceHitbox を探して一時的に有効化
            Hitbox[] hitboxes = GetComponentsInChildren<Hitbox>(true);
            foreach (var hb in hitboxes)
            {
                if (hb.gameObject.name == "LanceHitbox")
                {
                    StartCoroutine(ActivateHitboxTemporarily(hb.gameObject, 0.20f));
                    break;
                }
            }
        }

        private IEnumerator AnimateThrust(float duration)
        {
            float half = duration * 0.4f;
            float elapsed = 0f;
            Vector3 targetOffset = new Vector3(0.45f, 0f, 0f);

            // 前方へ素早くスラスト
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                visualsTransform.localPosition = originalVisualsPos + targetOffset * Mathf.Sin(t * Mathf.PI * 0.5f);
                yield return null;
            }

            // 元の位置へ戻る
            elapsed = 0f;
            float returnDuration = duration - half;
            Vector3 startPos = visualsTransform.localPosition;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                visualsTransform.localPosition = Vector3.Lerp(startPos, originalVisualsPos, t);
                yield return null;
            }
            visualsTransform.localPosition = originalVisualsPos;
            activeVisualsAnimCoroutine = null;
        }

        // =========================================================
        // 特殊攻撃: 砲撃発射
        // =========================================================
        public override void AttackSpecial()
        {
            if (isDead || isAttacking || isStunned || IsHurtLocked) return;

            if (HasAnimator) myAnimator.SetTrigger("AttackSpecial");

            // 攻撃硬直 0.3秒
            StartCoroutine(SetAttackingState(0.30f));

            // 反動アニメーション（少し後退して戻る）
            if (visualsTransform != null)
            {
                if (activeVisualsAnimCoroutine != null) StopCoroutine(activeVisualsAnimCoroutine);
                activeVisualsAnimCoroutine = StartCoroutine(AnimateRecoil(0.25f, 0.2f));
            }

            // 砲弾プレハブの生成
            if (shellPrefab != null && firePoint != null)
            {
                GameObject shell = Instantiate(shellPrefab, firePoint.position, Quaternion.identity);
                GannrannsuShell shellScript = shell.GetComponent<GannrannsuShell>();
                if (shellScript != null)
                {
                    float direction = Mathf.Sign(transform.localScale.x);
                    shellScript.Initialize(direction, shellSpeed, shellDamage, playerID, () =>
                    {
                        AddCharge(specialHitChargeGain);
                    });
                }
            }
            else
            {
                // フォールバック：firePointやプレハブがない場合
                Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + new Vector3(Mathf.Sign(transform.localScale.x) * 1.2f, 0f, 0f);
                GameObject shellObj = new GameObject("GannrannsuShell");
                shellObj.transform.position = spawnPos;
                var s = shellObj.AddComponent<GannrannsuShell>();
                s.Initialize(Mathf.Sign(transform.localScale.x), shellSpeed, shellDamage, playerID, () =>
                {
                    AddCharge(specialHitChargeGain);
                });
            }
        }

        private IEnumerator AnimateRecoil(float duration, float recoilDistance)
        {
            float elapsed = 0f;
            float kickDuration = duration * 0.25f;
            Vector3 recoilOffset = new Vector3(-recoilDistance, 0.05f, 0f);

            while (elapsed < kickDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / kickDuration);
                visualsTransform.localPosition = originalVisualsPos + recoilOffset * t;
                yield return null;
            }

            elapsed = 0f;
            float recoverDuration = duration - kickDuration;
            Vector3 startPos = visualsTransform.localPosition;
            while (elapsed < recoverDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / recoverDuration);
                visualsTransform.localPosition = Vector3.Lerp(startPos, originalVisualsPos, t);
                yield return null;
            }
            visualsTransform.localPosition = originalVisualsPos;
            activeVisualsAnimCoroutine = null;
        }

        // =========================================================
        // 必殺技: 竜撃砲
        // =========================================================
        public override void AttackUltimate()
        {
            if (isDead || isAttacking || isStunned || IsHurtLocked) return;

            chargeGauge = 0f;
            ultimateAttackCooldownTimer = ultimateAttackCooldown;

            if (HasAnimator) myAnimator.SetTrigger("AttackUltimate");

            StartCoroutine(SetAttackingState(0.55f));

            if (visualsTransform != null)
            {
                if (activeVisualsAnimCoroutine != null) StopCoroutine(activeVisualsAnimCoroutine);
                activeVisualsAnimCoroutine = StartCoroutine(AnimateRecoil(0.45f, 0.5f));
            }

            // 竜撃砲の弾を生成
            GameObject projObj = new GameObject("DragonBlastProjectile");
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + new Vector3(Mathf.Sign(transform.localScale.x) * 1.5f, 0f, 0f);
            projObj.transform.position = spawnPos;

            DragonBlastProjectile proj = projObj.AddComponent<DragonBlastProjectile>();
            float direction = Mathf.Sign(transform.localScale.x);
            proj.Initialize(direction, 15f, dragonBlastDamage, playerID);

            // 本体に後方への強力な反動を加える
            float recoilDirection = -direction;
            if (myRb != null)
            {
                myRb.AddForce(new Vector2(recoilDirection * dragonBlastRecoil, 2f), ForceMode2D.Impulse);
            }
        }

        public override void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isDead) return;

            isAttacking = false;
            if (activeVisualsAnimCoroutine != null)
            {
                StopCoroutine(activeVisualsAnimCoroutine);
                activeVisualsAnimCoroutine = null;
            }
            if (visualsTransform != null)
            {
                visualsTransform.localPosition = originalVisualsPos;
            }

            base.TakeDamage(damage, attackerHitbox);
        }


        protected new IEnumerator ActivateHitboxTemporarily(GameObject hitboxObj, float duration)
        {
            if (hitboxObj != null)
            {
                hitboxObj.SetActive(true);
                yield return new WaitForSeconds(duration);
                hitboxObj.SetActive(false);
            }
        }

        // --- GUI表示 ---
        private void InitTextures()
        {
            if (bgTex == null) bgTex = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.6f));
            if (fillTex == null) fillTex = MakeTex(2, 2, Color.orange);
            if (textStyle == null)
            {
                textStyle = new GUIStyle();
                textStyle.fontSize = 12;
                textStyle.fontStyle = FontStyle.Bold;
                textStyle.normal.textColor = Color.white;
                textStyle.alignment = TextAnchor.MiddleCenter;
            }
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        void OnDestroy()
        {
            if (bgTex != null) Destroy(bgTex);
            if (fillTex != null) Destroy(fillTex);
        }

        void OnGUI()
        {
            if (isDead) return;
            InitTextures();

            float width = 200f;
            float height = 20f;
            float x = (playerID == 1) ? 20f : Screen.width - width - 20f;
            float y = 90f;

            GUI.DrawTexture(new Rect(x, y, width, height), bgTex);

            float fillRatio = chargeGauge / maxChargeGauge;
            float fillWidth = (width - 4f) * fillRatio;

            Color barColor = Color.orange;
            string text = $"CHARGE: {Mathf.Floor(chargeGauge)}%";

            if (chargeGauge >= maxChargeGauge)
            {
                float flash = Mathf.PingPong(Time.time * 4f, 1f);
                barColor = Color.Lerp(new Color(1f, 0.3f, 0f), new Color(1f, 0.9f, 0f), flash);

                string keyLabel = (playerID == 1) ? "J+K" : "1+2 / U+I";
                text = $"READY! ({keyLabel})";
            }

            GUI.color = barColor;
            GUI.DrawTexture(new Rect(x + 2f, y + 2f, fillWidth, height - 4f), fillTex);
            GUI.color = Color.white;

            GUI.Label(new Rect(x, y, width, height), text, textStyle);
        }
    }
}
