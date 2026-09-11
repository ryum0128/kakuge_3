using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // Base class for all characters in the game.
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterBase : MonoBehaviour
    {
        [Header("Character Configurations")]
        public CharacterStats stats;
        public int playerID = 1;

        [Header("Character Stats")]
        public int maxHP = 100;
        public int currentHP;
        public bool isGrounded;
        public bool isDead;
        public bool isStunned = false;

        [Header("リスポーン設定")]
        [Tooltip("最大ライフ数（残機）。0になったら本当に死亡。")]
        public int maxLives = 3;
        [HideInInspector]
        public int remainingLives;

        [Header("Dash & Evade Configurations")]
        public bool isInvincible = false;
        public bool isDashingOrEvading = false;
        
        // 攻撃動作中か判定するための仮想プロパティ（派生先でオーバーライドされます）
        public virtual bool IsAttacking => false;

        /// <summary>
        /// スタンスキルを使用できるかどうか（派生クラスで true にオーバーライドすると使用可能になる）
        /// デフォルトは false（使用不可）。ライトセーバーキャラのみ true にする。
        /// </summary>
        public virtual bool CanUseStunSkill => false;

        [HideInInspector]
        public float lastHurtTime = -10f;

        // --- スタンスキル用の内部変数 ---
        private int stunUseCount = 0;            // スタンスキルの使用回数
        private Coroutine stunCoroutine = null;  // スタン解除用のコルーチン

        [Header("スタンスキル設定")]
        [Tooltip("スタンスキルの最大使用回数")]
        public int maxStunUses = 2;              // スタンスキルの使用上限（2回）

        [Tooltip("チャージゲージが満タンになるまでの時間（秒）")]
        public float stunChargeTime = 10f;      // ゲージが0から100%になるまでの時間

        [Tooltip("現在のチャージゲージ量（0～1、0=空、1=満タン）")]
        public float stunChargeGauge = 0f;      // 現在のチャージ量（0.0～1.0）

        /// <summary>
        /// スタンゲージが満タンかどうかを確認できるプロパティ
        /// </summary>
        public bool IsStunReady => stunChargeGauge >= 1f;

        /// <summary>
        /// スタンスキルの残り使用回数
        /// </summary>
        public int StunUsesRemaining => maxStunUses - stunUseCount;

        /// <summary>
        /// スタンスキルが全回数使用済みかどうかを確認できるプロパティ
        /// </summary>
        public bool HasUsedStun => stunUseCount >= maxStunUses;

        // 被ダメージ後、一定時間（0.2秒）被弾反動で攻撃を出せないようにするロック判定
        public bool IsHurtLocked => Time.time - lastHurtTime < 0.2f;

        [Header("DeepWoken Posture & Mana Settings")]
        public float maxPosture = 100f;
        public float currentPosture = 0f;
        public float postureRegenSpeed = 20f;
        public float maxMana = 100f;
        public float currentMana = 50f; // Starts with 50 Mana
        public float manaRegenSpeed = 8f; // Mana recovery speed per second
        public bool isBlockingState = false;

        protected Rigidbody2D rb;
        protected Animator animator;
        protected Collider2D mainCollider;
        private float originalGravityScale = 3f;

        protected virtual void Start()
        {
            // キャラクターセレクト経由で生成されたキャラクター（_P1 / _P2）以外で、
            // ゲームスタート時にHierarchyに最初から置かれていたキャラクターは即座に消去
            if (!CharacterSelectManager.IsSpawningFromSelect && !name.EndsWith("_P1") && !name.EndsWith("_P2"))
            {
                Destroy(gameObject);
                return;
            }

            rb = GetComponent<Rigidbody2D>();
            mainCollider = GetComponent<Collider2D>();
            if (rb != null)
            {
                rb.mass = 1000f;
                originalGravityScale = rb.gravityScale;
            }
            
            // Determine playerID dynamically based on explicit name or position
            if (name.Contains("P2") || name.Contains("RightSide") || name.Contains("Player2") || name.Contains("(1)"))
            {
                playerID = 2;
            }
            else if (name.Contains("P1") || name.Contains("LeftSide") || name.Contains("Player1"))
            {
                playerID = 1;
            }
            else
            {
                var allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
                if (allChars.Length == 2)
                {
                    System.Array.Sort(allChars, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
                    playerID = (this == allChars[0]) ? 1 : 2;
                }
            }
            animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController == null)
            {
                animator.enabled = false;
                animator = null;
            }

            if (stats != null)
            {
                maxHP = stats.maxHP * 2;
            }
            else
            {
                maxHP = maxHP * 2;
            }
            currentHP = maxHP;
            currentMana = maxMana;
            remainingLives = maxLives;

            // Auto-instantiate HUDManager if it's missing in the scene
            if (FindAnyObjectByType<HUDManager>() == null)
            {
                GameObject hudGo = new GameObject("HUDManager");
                hudGo.AddComponent<HUDManager>();
            }

            // Sync all child Hitboxes to this character's playerID to guarantee no owner ID mismatch issues
            Hitbox[] hitboxes = GetComponentsInChildren<Hitbox>(true);
            foreach (Hitbox h in hitboxes)
            {
                h.owner = this;
                h.ownerPlayerID = playerID;
            }

            // 初期のスプライトカラーをキャッシュ
            CacheOriginalColorsIfNeeded();
        }

        protected virtual void Update()
        {
            if (isDead || (GameManager.Instance != null && !GameManager.Instance.IsPlaying)) return;

            // スタンチャージゲージを溜める（使用回数が残っていて、満タンでない場合）
            if (stunUseCount < maxStunUses && stunChargeGauge < 1f)
            {
                stunChargeGauge += Time.deltaTime / stunChargeTime;
                stunChargeGauge = Mathf.Clamp01(stunChargeGauge); // 0～1の範囲に収める
            }

            // スタン中は移動を停止する（入力を無視してキャラクターを止める）
            if (isStunned)
            {
                Rigidbody2D rbRef = GetComponent<Rigidbody2D>();
                if (rbRef != null)
                {
                    rbRef.linearVelocity = new Vector2(0, rbRef.linearVelocity.y);
                }
                SafeSetFloat("Speed", 0f);
            }

            // Ground check (コライダーの物理接地判定により、空中の頂点でダブルジャンプが発生しないようにする)
            isGrounded = CheckGrounded();

            SafeSetBool("IsGrounded", isGrounded);

            // Natural mana recovery
            if (currentMana < maxMana)
            {
                currentMana += manaRegenSpeed * Time.deltaTime;
                if (currentMana > maxMana) currentMana = maxMana;
            }
        }

        /// <summary>
        /// 物理コライダーを用いて足元が地面に接触しているかを正確に判定します。
        /// 空中の頂点などで速度が0になっても接地と誤判定されないようにし、ダブルジャンプを防止します。
        /// </summary>
        private bool CheckGrounded()
        {
            if (rb == null) return false;

            // ジャンプ等で上方向に移動中なら確実に空中
            if (rb.linearVelocity.y > 0.1f)
            {
                return false;
            }

            if (mainCollider == null)
            {
                mainCollider = GetComponent<Collider2D>();
            }

            if (mainCollider != null)
            {
                Bounds bounds = mainCollider.bounds;
                // コライダーの足元から下方向へBoxCastで地面をチェック
                Vector2 checkOrigin = new Vector2(bounds.center.x, bounds.min.y + 0.05f);
                Vector2 checkSize = new Vector2(bounds.size.x * 0.7f, 0.08f);

                RaycastHit2D[] hits = Physics2D.BoxCastAll(checkOrigin, checkSize, 0f, Vector2.down, 0.12f);
                foreach (var hit in hits)
                {
                    if (hit.collider == null || hit.collider.isTrigger) continue;

                    // 自分自身または子オブジェクトのコライダーは除外
                    if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform)) continue;

                    // 相手キャラクターのコライダーも地面とは判定しない
                    if (hit.collider.GetComponentInParent<CharacterBase>() != null) continue;

                    return true;
                }
            }
            else
            {
                // コライダー未設定時のフォールバック
                RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, Vector2.down, 1.2f);
                foreach (var hit in hits)
                {
                    if (hit.collider == null || hit.collider.isTrigger) continue;
                    if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform)) continue;
                    if (hit.collider.GetComponentInParent<CharacterBase>() != null) continue;

                    return true;
                }
            }

            return false;
        }

        // 攻撃ヒット時やイベント時にマナを増加させるメソッド
        public void AddMana(float amount)
        {
            if (isDead) return;
            currentMana += amount;
            if (currentMana > maxMana) currentMana = maxMana;
        }

        public virtual void Move(float direction)
        {
            if (isDead || isStunned) return;

            float speed = stats != null ? stats.moveSpeed : 5f;
            rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);

            // Flip sprite depending on direction
            if (direction != 0)
            {
                transform.localScale = new Vector3(Mathf.Sign(direction), 1, 1);
            }

            SafeSetFloat("Speed", Mathf.Abs(direction));
        }

        public virtual void Jump()
        {
            if (isDead || !isGrounded || isStunned) return;

            // ジャンプ実行（1回のみジャンプ可能にする）
            isGrounded = false;
            float force = stats != null ? stats.jumpForce : 12f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);

            SafeSetTrigger("Jump");
        }

        public virtual void AttackNormal()
        {
            if (isDead || isStunned) return;

            SafeSetTrigger("AttackNormal");
            Debug.Log("Normal attack triggered.");

            Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
            if (hitbox != null)
            {
                StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, 0.2f));
            }
        }

        protected IEnumerator ActivateHitboxTemporarily(GameObject hitboxObj, float duration)
        {
            hitboxObj.SetActive(true);
            yield return new WaitForSeconds(duration);
            hitboxObj.SetActive(false);
        }

        public virtual void AttackSpecial()
        {
            if (isDead || isStunned) return;
            SafeSetTrigger("AttackSpecial");
            Debug.Log("Special attack triggered.");
        }

        public virtual void AttackUltimate()
        {
            if (isDead || isStunned) return;
            SafeSetTrigger("AttackUltimate");
            Debug.Log("Ultimate attack triggered.");
        }

        private SpriteRenderer[] cachedRenderers;
        private Color[] originalSpriteColors;
        private Coroutine hitFlashCoroutine;
        private Coroutine respawnInvincibilityCoroutine;

        private void CacheOriginalColorsIfNeeded()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
                originalSpriteColors = new Color[cachedRenderers.Length];
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    if (cachedRenderers[i] != null)
                    {
                        originalSpriteColors[i] = cachedRenderers[i].color;
                    }
                }
            }
        }

        private void ResetToOriginalColors()
        {
            CacheOriginalColorsIfNeeded();
            if (cachedRenderers == null || originalSpriteColors == null) return;

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null && i < originalSpriteColors.Length)
                {
                    cachedRenderers[i].color = originalSpriteColors[i];
                }
            }
        }

        public virtual void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isDead || isInvincible) return;

            lastHurtTime = Time.time;

            // スタン中に攻撃を受けたら、スタンを解除する
            if (isStunned)
            {
                RemoveStun();
            }

            currentHP -= damage;
            if (currentHP < 0) currentHP = 0;

            SafeSetTrigger("Damage");

            // 被弾時にキャラが明るく赤色にフラッシュする反動演出（目に見える被弾フィードバック）
            if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(PlayHitFlashRoutine());

            // Register hit in HUD for damage popups, combos, and HP bars
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.RegisterHit(playerID, damage, transform.position);
            }

            if (currentHP == 0)
            {
                Die();
            }
        }

        private IEnumerator PlayHitFlashRoutine()
        {
            CacheOriginalColorsIfNeeded();
            if (cachedRenderers == null) yield break;

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null)
                {
                    cachedRenderers[i].color = new Color(1f, 0.2f, 0.2f, 1f); // 赤色に被弾フラッシュ
                }
            }

            yield return new WaitForSeconds(0.2f);

            ResetToOriginalColors();
            hitFlashCoroutine = null;
        }

        private void Die()
        {
            isDead = true;
            remainingLives--;

            // 実行中のエフェクトコルーチンを停止して色をリセット
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }
            if (respawnInvincibilityCoroutine != null)
            {
                StopCoroutine(respawnInvincibilityCoroutine);
                respawnInvincibilityCoroutine = null;
            }
            ResetToOriginalColors();

            SafeSetBool("IsDead", true);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCharacterDied(playerID);
            }
        }

        // =========================================================
        // リスポーン演出（ステージ中央へのダイナミック着地＆エフェクト）
        // =========================================================
        private static Sprite shockwaveSprite = null;
        private static Sprite beamSprite = null;

        private static Sprite GetOrCreateShockwaveSprite()
        {
            if (shockwaveSprite != null) return shockwaveSprite;

            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float outerRadius = size * 0.48f;
            float innerRadius = size * 0.30f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist >= innerRadius && dist <= outerRadius)
                    {
                        float mid = (innerRadius + outerRadius) * 0.5f;
                        float halfWidth = (outerRadius - innerRadius) * 0.5f;
                        float alpha = 1f - Mathf.Abs(dist - mid) / halfWidth;
                        alpha = Mathf.SmoothStep(0f, 1f, alpha);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            shockwaveSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return shockwaveSprite;
        }

        private static Sprite GetOrCreateBeamSprite()
        {
            if (beamSprite != null) return beamSprite;

            int w = 32;
            int h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < h; y++)
            {
                float verticalAlpha = 1f - (float)y / h; // 上に向かってフェード
                for (int x = 0; x < w; x++)
                {
                    float horizontalAlpha = 1f - Mathf.Abs((x - w * 0.5f) / (w * 0.5f));
                    float alpha = Mathf.Clamp01(horizontalAlpha * verticalAlpha);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            beamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f));
            return beamSprite;
        }

        private IEnumerator AnimateLandingEffects(Vector3 groundPos)
        {
            // 1. 光の柱（上空から降り注ぐビーム）
            GameObject beamObj = new GameObject("Respawn_Beam");
            beamObj.transform.position = groundPos;
            SpriteRenderer beamSr = beamObj.AddComponent<SpriteRenderer>();
            beamSr.sprite = GetOrCreateBeamSprite();
            beamSr.color = new Color(1f, 0.25f, 0.2f, 0.85f);
            beamSr.sortingOrder = 10;
            beamObj.transform.localScale = new Vector3(3.0f, 10f, 1f);

            // 2. 地面の衝撃波リング
            GameObject ringObj = new GameObject("Respawn_Shockwave");
            ringObj.transform.position = groundPos + new Vector3(0f, 0.1f, 0f);
            SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
            ringSr.sprite = GetOrCreateShockwaveSprite();
            ringSr.color = new Color(1f, 0.35f, 0.2f, 0.95f);
            ringSr.sortingOrder = 11;
            ringObj.transform.localScale = new Vector3(0.6f, 0.25f, 1f);

            float elapsed = 0f;
            float duration = 0.45f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (beamObj != null)
                {
                    float beamWidth = Mathf.Lerp(3.0f, 0.1f, t);
                    beamObj.transform.localScale = new Vector3(beamWidth, 10f, 1f);
                    Color bc = beamSr.color;
                    bc.a = Mathf.Lerp(0.85f, 0f, t);
                    beamSr.color = bc;
                }

                if (ringObj != null)
                {
                    float ringScaleX = Mathf.Lerp(0.6f, 7.0f, t);
                    float ringScaleY = Mathf.Lerp(0.25f, 2.0f, t);
                    ringObj.transform.localScale = new Vector3(ringScaleX, ringScaleY, 1f);
                    Color rc = ringSr.color;
                    rc.a = Mathf.Lerp(0.95f, 0f, t);
                    ringSr.color = rc;
                }

                yield return null;
            }

            if (beamObj != null) Destroy(beamObj);
            if (ringObj != null) Destroy(ringObj);
        }

        /// <summary>
        /// キャラクターをステージ真ん中にダイナミックに着地リスポーンさせます。
        /// </summary>
        public void Respawn()
        {
            // HP・状態のリセット
            currentHP = maxHP;
            isDead = false;
            isStunned = false;
            isInvincible = true; // リスポーン演出開始から無敵
            isDashingOrEvading = false;
            currentPosture = 0f;
            currentMana = maxMana;

            // 実行中のエフェクトコルーチンを停止
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }
            if (respawnInvincibilityCoroutine != null)
            {
                StopCoroutine(respawnInvincibilityCoroutine);
                respawnInvincibilityCoroutine = null;
            }

            // アニメーターのリセット
            SafeSetBool("IsDead", false);
            SafeResetTrigger("Damage");
            SafeResetTrigger("Stunned");
            SafeSetFloat("Speed", 0f);

            // 速度リセット
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            // 真ん中に上空からかっこよく着地するリスポーンシーケンスを開始
            respawnInvincibilityCoroutine = StartCoroutine(RespawnSequenceRoutine());

            Debug.Log($"プレイヤー{playerID} が真ん中にリスポーン！ 残りライフ: {remainingLives}");
        }

        private IEnumerator RespawnSequenceRoutine()
        {
            isInvincible = true;

            CacheOriginalColorsIfNeeded();

            Transform visuals = transform.Find("Visuals");
            if (visuals == null) visuals = transform;
            Vector3 baseScale = visuals.localScale;

            // 相手のいる方向を向く
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

            if (opponent != null)
            {
                float dir = opponent.transform.position.x - 0f; // 中央(0f)から相手の向き
                float face = dir >= 0 ? 1f : -1f;
                transform.localScale = new Vector3(face, 1f, 1f);
            }
            else
            {
                transform.localScale = new Vector3(playerID == 1 ? 1f : -1f, 1f, 1f);
            }

            // 地面の正確な高さをレイキャストで取得
            float groundY = -0.5f;
            RaycastHit2D groundHit = Physics2D.Raycast(new Vector2(0f, 10f), Vector2.down, 25f);
            if (groundHit.collider != null && !groundHit.collider.isTrigger)
            {
                float charHalfHeight = 1.0f;
                if (mainCollider != null) charHalfHeight = mainCollider.bounds.extents.y;
                groundY = groundHit.point.y + charHalfHeight;
            }

            float spawnHeight = groundY + 7.0f; // 真ん中の上空から降下
            Vector3 startPos = new Vector3(0f, spawnHeight, transform.position.z);
            Vector3 landPos = new Vector3(0f, groundY, transform.position.z);

            transform.position = startPos;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f; // 降下演出中は物理重力をオフ
            }

            // 最初から赤色オーラをまとう
            Color redAuraColor = new Color(1f, 0.2f, 0.2f, 1.0f);
            if (cachedRenderers != null)
            {
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    if (cachedRenderers[i] != null) cachedRenderers[i].color = redAuraColor;
                }
            }

            // 上空から中央へ高速急降下（0.22秒）
            float dropDuration = 0.22f;
            float dropElapsed = 0f;

            // 降下中は縦長にストレッチ
            visuals.localScale = new Vector3(baseScale.x * 0.75f, baseScale.y * 1.35f, baseScale.z);

            while (dropElapsed < dropDuration)
            {
                dropElapsed += Time.deltaTime;
                float t = dropElapsed / dropDuration;
                float curvedT = t * t; // 加速しながら降下
                transform.position = Vector3.Lerp(startPos, landPos, curvedT);
                yield return null;
            }

            transform.position = landPos;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = originalGravityScale; // 通常重力に復帰
            }

            // ★着地インパクト演出！
            // 1. 画面シェイク
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.22f, 0.14f);
            }

            // 2. 着地衝撃波（Shockwave）＆光の柱エフェクト
            float contactY = groundHit.collider != null ? groundHit.point.y : groundY - 1f;
            StartCoroutine(AnimateLandingEffects(new Vector3(0f, contactY, 0f)));

            // 3. 着地のスカッシュ（ヒーロー着地の沈み込み）＆反動アニメーション（0.12秒）
            float landElapsed = 0f;
            float landDuration = 0.12f;
            while (landElapsed < landDuration)
            {
                landElapsed += Time.deltaTime;
                float lt = landElapsed / landDuration;
                float sx = Mathf.Lerp(baseScale.x * 1.4f, baseScale.x, lt);
                float sy = Mathf.Lerp(baseScale.y * 0.6f, baseScale.y, lt);
                visuals.localScale = new Vector3(sx, sy, baseScale.z);
                yield return null;
            }
            visuals.localScale = baseScale;

            // ★着地後、2秒間の無敵時間（赤色点滅）を継続
            float remainingInvincible = 2.0f - dropDuration - landDuration;
            if (remainingInvincible < 1.0f) remainingInvincible = 1.6f;

            float blinkInterval = 0.12f;
            float invElapsed = 0f;
            bool blinkState = false;

            while (invElapsed < remainingInvincible)
            {
                blinkState = !blinkState;
                Color blinkColor = blinkState 
                    ? new Color(1f, 0.15f, 0.15f, 1.0f) 
                    : new Color(1f, 0.2f, 0.2f, 0.35f);

                if (cachedRenderers != null)
                {
                    for (int i = 0; i < cachedRenderers.Length; i++)
                    {
                        if (cachedRenderers[i] != null) cachedRenderers[i].color = blinkColor;
                    }
                }

                yield return new WaitForSeconds(blinkInterval);
                invElapsed += blinkInterval;
            }

            // 無敵時間終了：元の色・状態に完全に戻す
            ResetToOriginalColors();
            visuals.localScale = baseScale;

            isInvincible = false;
            respawnInvincibilityCoroutine = null;
        }

        public virtual void StartBlock() { }
        public virtual void StopBlock() { }

        public virtual void Stun(float duration)
        {
            if (isDead) return;
            StartCoroutine(StunRoutine(duration));
        }

        private IEnumerator StunRoutine(float duration)
        {
            isStunned = true;
            SafeSetTrigger("Damage");
            yield return new WaitForSeconds(duration);
            isStunned = false;
        }

        protected virtual void LateUpdate()
        {
            PreventCharacterOverlap();
            ClampToStageBoundaries();
        }

        private void ClampToStageBoundaries()
        {
            if (isDead) return;

            float minX = -12.0f;
            float maxX = 12.0f;

            if (transform.position.x < minX)
            {
                transform.position = new Vector3(minX, transform.position.y, transform.position.z);
                if (rb != null && rb.linearVelocity.x < 0)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }
            else if (transform.position.x > maxX)
            {
                transform.position = new Vector3(maxX, transform.position.y, transform.position.z);
                if (rb != null && rb.linearVelocity.x > 0)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }
        }

        private void PreventCharacterOverlap()
        {
            if (isDead || rb == null || isDashingOrEvading) return;

            // Find opponent
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

            if (opponent == null) return;

            // Get distance
            float dx = opponent.transform.position.x - transform.position.x;
            float absDx = Mathf.Abs(dx);
            float dy = opponent.transform.position.y - transform.position.y;
            float absDy = Mathf.Abs(dy);

            // Assume standard capsule collision width sum is ~0.9f.
            // Only block horizontal movement if we are close vertically (not jumping over each other).
            float minHorizontalDistance = 0.9f;
            float minVerticalDistance = 1.6f;

            if (absDx < minHorizontalDistance && absDy < minVerticalDistance)
            {
                Vector2 vel = rb.linearVelocity;

                // Moving right towards opponent on the right -> stop
                if (dx > 0 && vel.x > 0)
                {
                    vel.x = 0f;
                    rb.linearVelocity = vel;
                }
                // Moving left towards opponent on the left -> stop
                else if (dx < 0 && vel.x < 0)
                {
                    vel.x = 0f;
                    rb.linearVelocity = vel;
                }
            }
        }

        public void TriggerDashOrEvade(float inputDir)
        {
            if (isDead || isStunned || isDashingOrEvading || IsAttacking) return;
            StartCoroutine(DashOrEvadeRoutine(inputDir));
        }

        private IEnumerator DashOrEvadeRoutine(float inputDir)
        {
            isDashingOrEvading = true;
            isInvincible = true;

            // Opponent search
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

            Collider2D[] myCols = GetComponentsInChildren<Collider2D>();
            Collider2D[] oppCols = opponent != null ? opponent.GetComponentsInChildren<Collider2D>() : null;

            // Ignore collision with opponent
            if (myCols != null && oppCols != null)
            {
                foreach (var cA in myCols)
                {
                    foreach (var cB in oppCols)
                    {
                        Physics2D.IgnoreCollision(cA, cB, true);
                    }
                }
            }

            // Find visuals transform (fallback to this transform if not found)
            Transform visuals = transform.Find("Visuals");
            if (visuals == null) visuals = transform;

            SpriteRenderer sr = visuals.GetComponentInChildren<SpriteRenderer>();
            Color originalColor = sr != null ? sr.color : Color.white;
            Vector3 originalScale = visuals.localScale;

            float faceDir = transform.localScale.x; // 1 = right, -1 = left
            bool isDash = inputDir != 0f;
            float dashDir = isDash ? Mathf.Sign(inputDir) : -faceDir; // Dash in input direction, Evade backward
            float duration = isDash ? 0.18f : 0.15f; // ダッシュ距離を縮小 (duration: 0.22 -> 0.18, evade: 0.18 -> 0.15)
            float speed = isDash ? 13f : 8f;         // ダッシュ速度を抑える (speed: 18 -> 13, evade: 10 -> 8)

            // Apply visual cues
            if (sr != null)
            {
                sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.45f); // Transparent ghosting effect
            }

            if (isDash)
            {
                // Dash squash-stretch scaling
                visuals.localScale = new Vector3(originalScale.x * 1.3f, originalScale.y * 0.8f, originalScale.z);
            }
            else
            {
                // Evade tilt angle rotation
                visuals.localRotation = Quaternion.Euler(0, 0, -dashDir * 15f);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (isDead) break;
                elapsed += Time.deltaTime;
                rb.linearVelocity = new Vector2(dashDir * speed, rb.linearVelocity.y);
                yield return null;
            }

            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            // Restore collisions
            if (myCols != null && oppCols != null)
            {
                foreach (var cA in myCols)
                {
                    foreach (var cB in oppCols)
                    {
                        Physics2D.IgnoreCollision(cA, cB, false);
                    }
                }
            }

            // Restore visuals
            if (sr != null)
            {
                sr.color = originalColor;
            }
            visuals.localScale = originalScale;
            visuals.localRotation = Quaternion.identity;

            isInvincible = false;
            isDashingOrEvading = false;
        }

        // =========================================================
        // スタン（行動不能）スキル
        // =========================================================

        /// <summary>
        /// スタンスキルを発動します。
        /// 相手キャラクターを探して2秒間行動不能にします。（1試合に1回のみ）
        /// </summary>
        public virtual void AttackStun()
        {
            if (isDead || isStunned) return;

            // スタンスキルはライトセーバーキャラクター専用
            if (!CanUseStunSkill)
            {
                Debug.Log("スタンスキルはライトセーバーでしか使えません！");
                return;
            }

            // チャージゲージが満タンでない場合は使えない
            if (stunChargeGauge < 1f)
            {
                Debug.Log($"スタンゲージが足りません！（{stunChargeGauge * 100f:F0}%）");
                return;
            }

            // 使用回数の上限に達したら発動しない
            if (stunUseCount >= maxStunUses)
            {
                Debug.Log($"スタンスキルはもう使えません！（{maxStunUses}回使用済み）");
                return;
            }

            stunUseCount++;       // 使用回数を加算
            stunChargeGauge = 0f; // ゲージをリセット

            // スタンスキルのアニメーション（あれば再生）
            SafeSetTrigger("AttackStun");
            Debug.Log("スタンスキル発動！ 敵を2秒間行動不能にする！");

            // 相手キャラクターを探してスタンを付与する
            CharacterBase[] allCharacters = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (CharacterBase target in allCharacters)
            {
                // 自分以外のキャラクター（＝敵）にスタンを付与
                if (target.playerID != this.playerID && !target.isDead)
                {
                    target.ApplyStun(2.0f); // 2秒間スタン
                }
            }
        }

        /// <summary>
        /// このキャラクターをスタン（行動不能）状態にします。
        /// 実行中のアクション（コルーチン）をすべて強制中断します。
        /// </summary>
        public virtual void ResetActionStates()
        {
        }

        public void ApplyStun(float duration)
        {
            if (isDead || isStunned) return;

            // Reset subclasses specific states (isSwinging, isCharging, etc.)
            ResetActionStates();

            // 実行中のすべてのコルーチンE攻撁EニメーションなどEを強制停止する
            StopAllCoroutines();

            // アニメーターの状態をリセットする（攻撃モーション等を中断）
            SafeResetTrigger("AttackNormal");
            SafeResetTrigger("AttackSpecial");
            SafeResetTrigger("AttackUltimate");
            SafeSetFloat("Speed", 0f);
            SafeSetTrigger("Stunned");

            // 移動を止める
            Rigidbody2D rbRef = GetComponent<Rigidbody2D>();
            if (rbRef != null)
            {
                rbRef.linearVelocity = new Vector2(0, rbRef.linearVelocity.y);
            }

            isStunned = true;
            Debug.Log($"プレイヤー{playerID} がスタン状態になった！（{duration}秒間）");

            // 指定時間後に自動でスタン解除するコルーチンを開始
            stunCoroutine = StartCoroutine(StunTimer(duration));
        }

        /// <summary>
        /// スタン状態を解除します。
        /// </summary>
        public void RemoveStun()
        {
            if (!isStunned) return;

            // スタン解除タイマーが動いていたら止める
            if (stunCoroutine != null)
            {
                StopCoroutine(stunCoroutine);
                stunCoroutine = null;
            }

            isStunned = false;
            Debug.Log($"プレイヤー{playerID} のスタンが解除された！");

            // スタン解除のアニメーション処理
            SafeResetTrigger("Stunned");
        }

        /// <summary>
        /// スタンの持続時間を管理するコルーチン（時間経過で自動解除）
        /// </summary>
        private IEnumerator StunTimer(float duration)
        {
            yield return new WaitForSeconds(duration);
            RemoveStun(); // 時間が来たらスタン解除
        }

        // =========================================================
        // Animator パラメーター安全制御ヘルパー
        // =========================================================
        protected bool HasAnimatorParameter(string paramName)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == paramName) return true;
            }
            return false;
        }

        protected void SafeSetBool(string paramName, bool value)
        {
            if (HasAnimatorParameter(paramName))
            {
                animator.SetBool(paramName, value);
            }
        }

        protected void SafeSetFloat(string paramName, float value)
        {
            if (HasAnimatorParameter(paramName))
            {
                animator.SetFloat(paramName, value);
            }
        }

        protected void SafeSetTrigger(string paramName)
        {
            if (HasAnimatorParameter(paramName))
            {
                animator.SetTrigger(paramName);
            }
        }

        protected void SafeResetTrigger(string paramName)
        {
            if (HasAnimatorParameter(paramName))
            {
                animator.ResetTrigger(paramName);
            }
        }
    }
}

