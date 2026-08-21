using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【HammerCharacter（ハンマーキャラクター）】
    // 重いハンマーを振り回す、一撃が強力な重量級キャラクター！
    // CharacterBase を継承して、ハンマー固有の攻撃性能を追加しています。
    //
    // 特徴：
    //   ・移動やジャンプは遅く重いが、一撃のダメージが高い
    //   ・通常攻撃は近距離の横振り（HammerHitbox）
    //   ・特殊攻撃は前方の地面への叩きつけ（HammerSpecialHitbox）
    //   ・同時押し必殺技は前方に踏み込みつつ放つ超強力な「ギガスマッシュ」（HammerUltimateHitbox）
    // =================================================================================
    public class HammerCharacter : CharacterBase
    {
        [Header("===== ハンマー専用設定 =====")]

        [Tooltip("通常攻撃（横振り）のダメージ")]
        public int normalAttackDamage = 18;

        [Tooltip("特殊攻撃（叩きつけ）のダメージ")]
        public int specialAttackDamage = 28;

        [Tooltip("必殺技（ギガスマッシュ）のダメージ")]
        public int ultimateAttackDamage = 55;

        [Tooltip("必殺技（ギガスマッシュ）の踏み込み速度")]
        public float ultimateForwardDashForce = 6f;

        [Header("===== クールタイム設定 =====")]
        [Tooltip("通常攻撃のクールタイム（秒）")]
        public float normalAttackCooldown = 0.5f;

        [Tooltip("特殊攻撃（叩きつけ）のクールタイム（秒）")]
        public float specialAttackCooldown = 1.0f;

        [Tooltip("必殺技（ギガスマッシュ）のクールタイム（秒）")]
        public float ultimateAttackCooldown = 3.0f;

        // 各攻撃の残りクールタイム時間（秒）
        public float normalAttackCooldownTimer { get; private set; } = 0f;
        public float specialAttackCooldownTimer { get; private set; } = 0f;
        public float ultimateAttackCooldownTimer { get; private set; } = 0f;

        [Header("===== 必殺技ゲージ設定 =====")]
        [Tooltip("チャージゲージの最大値")]
        public float maxChargeGauge = 100f;

        [Tooltip("現在のチャージゲージ値")]
        public float chargeGauge = 0f;

        [Tooltip("1秒あたりの自動チャージ量")]
        public float passiveChargeRate = 3f;

        [Tooltip("通常攻撃が命中したときのチャージ増加量")]
        public float normalHitChargeGain = 15f;

        [Tooltip("特殊攻撃が命中したときのチャージ増加量")]
        public float specialHitChargeGain = 25f;

        private Texture2D bgTex;
        private Texture2D fillTex;
        private GUIStyle textStyle;

        [Header("===== ジャンプ設定 =====")]
        [Tooltip("最大ジャンプ回数（2にすると2段ジャンプが可能）")]
        public int maxJumps = 2;
        private int remainingJumps = 2;

        private Rigidbody2D myRb;
        private Animator myAnimator;
        private Transform visualsTransform;
        private bool isProceduralAnimating = false;
        private Coroutine activeAnimCoroutine;

        void Awake()
        {
            myRb = GetComponent<Rigidbody2D>();
            myAnimator = GetComponentInChildren<Animator>();
            visualsTransform = transform.Find("Visuals");
        }

        void Start()
        {
            base.Start();

            // 子オブジェクトからHitbox（通常・特殊攻撃）を取得して、命中時のコールバックを登録
            Hitbox[] hitboxes = GetComponentsInChildren<Hitbox>(true);
            foreach (var hb in hitboxes)
            {
                if (hb.gameObject.name == "HammerHitbox")
                {
                    hb.OnHitLanded += (hurtbox, dmg) =>
                    {
                        AddCharge(normalHitChargeGain);
                    };
                }
                else if (hb.gameObject.name == "HammerSpecialHitbox" || hb.gameObject.name == "HammerSpecialBackHitbox")
                {
                    hb.OnHitLanded += (hurtbox, dmg) =>
                    {
                        AddCharge(specialHitChargeGain);
                    };
                }
            }
        }

        void Update()
        {
            if (isDead || GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            // 接地判定
            isGrounded = Mathf.Abs(myRb.linearVelocity.y) < 0.1f;

            if (myAnimator != null)
            {
                myAnimator.SetBool("IsGrounded", isGrounded);
            }

            // --- 必殺技ゲージの自動蓄積 ---
            AddCharge(passiveChargeRate * Time.deltaTime);

            // --- クールタイムタイマーの更新 ---
            if (normalAttackCooldownTimer > 0f)
                normalAttackCooldownTimer = Mathf.Max(0f, normalAttackCooldownTimer - Time.deltaTime);
            if (specialAttackCooldownTimer > 0f)
                specialAttackCooldownTimer = Mathf.Max(0f, specialAttackCooldownTimer - Time.deltaTime);
            if (ultimateAttackCooldownTimer > 0f)
                ultimateAttackCooldownTimer = Mathf.Max(0f, ultimateAttackCooldownTimer - Time.deltaTime);

            HandleBaseAnimations();
        }

        // 地面と衝突している間だけジャンプ回数をリセットします
        private void OnCollisionEnter2D(Collision2D collision)
        {
            ResetJumpsOnGroundContact(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            ResetJumpsOnGroundContact(collision);
        }

        private void ResetJumpsOnGroundContact(Collision2D collision)
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                // 法線ベクトルが上向き（0.5より大きい＝足元に地面がある）なら接地とみなしてジャンプ回数をリセット
                if (contact.normal.y > 0.5f)
                {
                    remainingJumps = maxJumps;
                    break;
                }
            }
        }

        // =========================================================
        // ジャンプ処理（2段ジャンプ対応）
        // =========================================================
        public new void Jump()
        {
            if (isDead) return;

            // 空中や地上でのジャンプ残回数をチェック
            if (remainingJumps > 0)
            {
                remainingJumps--;

                // statsがセットされていなければ、仮のジャンプ力「9.5」を使います。
                float force = stats != null ? stats.jumpForce : 9.5f;

                // 物理エンジンでジャンプの速度を設定（Y速度を上書き）
                myRb.linearVelocity = new Vector2(myRb.linearVelocity.x, force);

                // ジャンプのアニメーションを再生
                if (myAnimator != null)
                {
                    myAnimator.SetTrigger("Jump");
                }
                
                Debug.Log($"ハンマー：ジャンプ！ 残りジャンプ可能回数: {remainingJumps}");
            }
        }

        // =========================================================
        // 通常攻撃: ハンマーの横振り
        // =========================================================
        public new void AttackNormal()
        {
            if (isDead) return;
            if (normalAttackCooldownTimer > 0f) return; // クールタイム中なら発動しない

            normalAttackCooldownTimer = normalAttackCooldown; // タイマー設定

            if (myAnimator != null) myAnimator.SetTrigger("AttackNormal");
            Debug.Log("ハンマー：通常攻撃（横振り）！");

            PlayAnimation(AnimateAttackNormal());

            // 子オブジェクトから特定の攻撃判定（HammerHitbox）を探して、一時的に有効化します
            Hitbox hitbox = GetHitboxByName("HammerHitbox");
            if (hitbox != null)
            {
                hitbox.damage = normalAttackDamage;
                hitbox.ownerPlayerID = playerID;
                StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, 0.3f));
            }
        }

        // =========================================================
        // 特殊攻撃: ハンマーの叩きつけ
        // =========================================================
        public new void AttackSpecial()
        {
            if (isDead) return;
            if (specialAttackCooldownTimer > 0f) return; // クールタイム中なら発動しない

            specialAttackCooldownTimer = specialAttackCooldown; // タイマー設定

            if (myAnimator != null) myAnimator.SetTrigger("AttackSpecial");
            Debug.Log("ハンマー：特殊攻撃（前後連続叩きつけ）！");

            PlayAnimation(AnimateAttackSpecial());

            StartCoroutine(ExecuteDoubleSlam());
        }

        private System.Collections.IEnumerator ExecuteDoubleSlam()
        {
            // 1段目: 前方叩きつけ
            Hitbox forwardHitbox = GetHitboxByName("HammerSpecialHitbox");
            if (forwardHitbox != null)
            {
                forwardHitbox.damage = specialAttackDamage;
                forwardHitbox.ownerPlayerID = playerID;
                forwardHitbox.gameObject.SetActive(true);
            }

            // 0.25秒間、前方の判定を出す
            yield return new WaitForSeconds(0.25f);

            if (forwardHitbox != null)
            {
                forwardHitbox.gameObject.SetActive(false);
            }

            // 2段目: 後方叩きつけ
            Hitbox backwardHitbox = GetHitboxByName("HammerSpecialBackHitbox");
            if (backwardHitbox != null)
            {
                backwardHitbox.damage = specialAttackDamage;
                backwardHitbox.ownerPlayerID = playerID;
                backwardHitbox.gameObject.SetActive(true);
            }

            // 0.25秒間、後方の判定を出す
            yield return new WaitForSeconds(0.25f);

            if (backwardHitbox != null)
            {
                backwardHitbox.gameObject.SetActive(false);
            }
        }

        // =========================================================
        // 必殺技: ギガスマッシュ！（同時押しで発動する大技）
        // =========================================================
        public new void AttackUltimate()
        {
            if (isDead) return;
            if (ultimateAttackCooldownTimer > 0f) return; // クールタイム中なら発動しない

            // 必殺技発動時にゲージを消費してゼロに戻す
            chargeGauge = 0f;

            ultimateAttackCooldownTimer = ultimateAttackCooldown; // タイマー設定

            if (myAnimator != null) myAnimator.SetTrigger("AttackUltimate");
            Debug.Log("ハンマー：必殺技（ギガスマッシュ）！！！");

            PlayAnimation(AnimateAttackUltimate());

            // 前方に少し踏み込みながら叩きつけます
            float forwardDirection = Mathf.Sign(transform.localScale.x);
            myRb.AddForce(new Vector2(forwardDirection * ultimateForwardDashForce, 2f), ForceMode2D.Impulse);

            // 子オブジェクトから特定の攻撃判定（HammerUltimateHitbox）を探して有効化
            Hitbox hitbox = GetHitboxByName("HammerUltimateHitbox");
            if (hitbox != null)
            {
                hitbox.damage = ultimateAttackDamage;
                hitbox.ownerPlayerID = playerID;
                StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, 0.6f));
            }
        }

        // 名前から特定のHitboxコンポーネントを検索するヘルパーメソッド
        private Hitbox GetHitboxByName(string name)
        {
            Hitbox[] hitboxes = GetComponentsInChildren<Hitbox>(true);
            foreach (var hb in hitboxes)
            {
                if (hb.gameObject.name == name)
                {
                    return hb;
                }
            }
            // 見つからない場合は一番最初のHitboxをフォールバックとして返します
            return GetComponentInChildren<Hitbox>(true);
        }

        private System.Collections.IEnumerator ActivateHitboxTemporarily(GameObject hitboxObj, float duration)
        {
            hitboxObj.SetActive(true);
            yield return new WaitForSeconds(duration);
            hitboxObj.SetActive(false);
        }

        // =========================================================
        // 必殺技ゲージ関連メソッド & GUI描画
        // =========================================================
        public void AddCharge(float amount)
        {
            if (isDead) return;
            chargeGauge = Mathf.Clamp(chargeGauge + amount, 0f, maxChargeGauge);
        }

        private void InitTextures()
        {
            if (bgTex == null)
            {
                bgTex = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.6f)); // 半透明の黒背景
            }
            if (fillTex == null)
            {
                fillTex = MakeTex(2, 2, new Color(0.85f, 0.15f, 0.15f)); // ハンマーは赤色のゲージ
            }
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
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
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

        // 必殺技ゲージを画面上に綺麗に表示します
        void OnGUI()
        {
            if (isDead) return;
            InitTextures();

            float width = 200f;
            float height = 20f;
            float x = (playerID == 1) ? 20f : Screen.width - width - 20f;
            float y = 90f; // 体力ゲージの下

            // 背景描画
            GUI.DrawTexture(new Rect(x, y, width, height), bgTex);

            // ゲージの割合幅を計算
            float fillRatio = chargeGauge / maxChargeGauge;
            float fillWidth = (width - 4f) * fillRatio;
            
            Color barColor = new Color(0.85f, 0.15f, 0.15f); // 赤色
            string text = $"ギガスマッシュ: {Mathf.Floor(chargeGauge)}%";
            
            // 満タン時のピカピカ点滅エフェクト
            if (chargeGauge >= maxChargeGauge)
            {
                float flash = Mathf.PingPong(Time.time * 4f, 1f);
                barColor = Color.Lerp(new Color(0.85f, 0.15f, 0.15f), new Color(1f, 0.85f, 0f), flash); // 赤とゴールドの点滅
                text = "ギガスマッシュ READY! (K+L)";
            }
            
            // ゲージ描画
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(x + 2f, y + 2f, fillWidth, height - 4f), fillTex);
            GUI.color = Color.white; // リセット

            // テキスト表示
            GUI.Label(new Rect(x, y, width, height), text, textStyle);
        }

        // =========================================================
        // 手動・手続き型アニメーションシステム (Procedural Animations)
        // =========================================================
        private void PlayAnimation(System.Collections.IEnumerator anim)
        {
            if (activeAnimCoroutine != null)
            {
                StopCoroutine(activeAnimCoroutine);
            }
            isProceduralAnimating = true;
            activeAnimCoroutine = StartCoroutine(anim);
        }

        private void HandleBaseAnimations()
        {
            if (isProceduralAnimating || isDead || visualsTransform == null) return;

            if (!isGrounded)
            {
                // 空中：少し後ろに傾く
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, -15f);
            }
            else if (Mathf.Abs(myRb.linearVelocity.x) > 0.1f)
            {
                // 移動中：歩きに合わせて前後揺れ
                float angle = Mathf.Sin(Time.time * 14f) * 8f - 5f;
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            else
            {
                // アイドル：ゆっくり呼吸するような揺れ
                float angle = Mathf.Sin(Time.time * 3f) * 3f;
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        public new void TakeDamage(int damage)
        {
            if (isDead) return;
            base.TakeDamage(damage);

            if (currentHP <= 0)
            {
                PlayAnimation(AnimateDeath());
            }
            else
            {
                PlayAnimation(AnimateDamage());
            }
        }

        private System.Collections.IEnumerator AnimateAttackNormal()
        {
            if (visualsTransform == null) yield break;
            float duration = 0.3f;
            float elapsed = 0f;

            // 振りかぶり (0.08秒)
            float windupDuration = 0.08f;
            while (elapsed < windupDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / windupDuration;
                float angle = Mathf.Lerp(0f, -35f, t);
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }

            // 振り下ろし (0.08秒)
            elapsed = 0f;
            float swingDuration = 0.08f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / swingDuration;
                float angle = Mathf.Lerp(-35f, 90f, t);
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }

            // 残心と戻り (残り時間)
            elapsed = 0f;
            float returnDuration = duration - windupDuration - swingDuration;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / returnDuration;
                float angle = Mathf.Lerp(90f, 0f, t);
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }

            visualsTransform.localRotation = Quaternion.identity;
            isProceduralAnimating = false;
        }

        private System.Collections.IEnumerator AnimateAttackSpecial()
        {
            if (visualsTransform == null) yield break;
            float elapsed = 0f;

            // --- 1段目: 前方叩きつけ ---
            // 振り上げ (0.08秒)
            while (elapsed < 0.08f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.08f;
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 75f, t));
                yield return null;
            }

            // 叩きつけ (0.05秒)
            elapsed = 0f;
            while (elapsed < 0.05f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.05f;
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(75f, -30f, t));
                yield return null;
            }

            // インパクトの揺れと溜め (残り0.12秒)
            elapsed = 0f;
            while (elapsed < 0.12f)
            {
                elapsed += Time.deltaTime;
                float shake = Mathf.Sin(elapsed * 60f) * 2f;
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, -30f + shake);
                yield return null;
            }

            // --- 2段目: 後方叩きつけ ---
            // 振り上げ (0.08秒)
            elapsed = 0f;
            while (elapsed < 0.08f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.08f;
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-30f, 80f, t));
                yield return null;
            }

            // 後方へ叩きつけ (0.05秒)
            elapsed = 0f;
            while (elapsed < 0.05f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.05f;
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(80f, -145f, t));
                yield return null;
            }

            // 後方インパクトの揺れと溜め (0.07秒)
            elapsed = 0f;
            while (elapsed < 0.07f)
            {
                elapsed += Time.deltaTime;
                float shake = Mathf.Sin(elapsed * 60f) * 2f;
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, -145f + shake);
                yield return null;
            }

            // 戻り (0.05秒)
            elapsed = 0f;
            while (elapsed < 0.05f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.05f;
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-145f, 0f, t));
                yield return null;
            }

            visualsTransform.localRotation = Quaternion.identity;
            isProceduralAnimating = false;
        }

        private System.Collections.IEnumerator AnimateAttackUltimate()
        {
            if (visualsTransform == null) yield break;
            float duration = 0.6f;
            float elapsed = 0f;

            Vector3 originalScale = visualsTransform.localScale;
            Vector3 targetScale = originalScale * 1.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (t < 0.2f)
                {
                    visualsTransform.localScale = Vector3.Lerp(originalScale, targetScale, t / 0.2f);
                }
                else if (t > 0.8f)
                {
                    visualsTransform.localScale = Vector3.Lerp(targetScale, originalScale, (t - 0.8f) / 0.2f);
                }
                else
                {
                    visualsTransform.localScale = targetScale;
                }

                float angle = -t * 720f; // 高速スピン
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

                yield return null;
            }

            visualsTransform.localScale = originalScale;
            visualsTransform.localRotation = Quaternion.identity;
            isProceduralAnimating = false;
        }

        private System.Collections.IEnumerator AnimateDamage()
        {
            if (visualsTransform == null) yield break;
            float duration = 0.2f;
            float elapsed = 0f;
            Vector3 originalScale = visualsTransform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                float angle = Mathf.Lerp(0f, -25f, Mathf.PingPong(t * 2f, 1f));
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

                float scaleFactor = Mathf.Lerp(1.0f, 0.85f, Mathf.PingPong(t * 2f, 1f));
                visualsTransform.localScale = originalScale * scaleFactor;

                yield return null;
            }

            visualsTransform.localScale = originalScale;
            visualsTransform.localRotation = Quaternion.identity;
            isProceduralAnimating = false;
        }

        private System.Collections.IEnumerator AnimateDeath()
        {
            if (visualsTransform == null) yield break;
            float duration = 1.0f;
            float elapsed = 0f;
            Vector3 startPos = visualsTransform.localPosition;
            Vector3 targetPos = startPos + new Vector3(-0.5f, -1.0f, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                visualsTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                visualsTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -90f, t));

                yield return null;
            }
        }
    }
}
