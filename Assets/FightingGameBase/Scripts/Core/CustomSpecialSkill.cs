using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FightingGameBase
{
    public enum SpecialAttackStyle
    {
        Taiken = 0,
        Dagger = 1,
        Flame = 2,
        Thunder = 3,
        Ice = 4,
        Shadow = 5,
        Ninja = 6
    }

    /// <summary>
    /// 自分流キャラクターカスタマイズ用：
    /// 任意のキャラクターに対して、7種類のスペシャル攻撃（必殺技）のいずれかを動的に換装・実行するモジュール
    /// </summary>
    public class CustomSpecialSkill : MonoBehaviour
    {
        public SpecialAttackStyle style = SpecialAttackStyle.Flame;
        public bool IsActive { get; set; } = false;

        private CharacterBase owner;
        private Coroutine activeSpecialCoroutine;

        // --- 忍者クナイ用状態 ---
        private NinjaYellowKunai activeKunai;
        private float lastKunaiActionTime = -10f;
        private const float KunaiActionInterval = 0.12f;

        // --- 影の領域（沈黙）用状態 ---
        private float lastShadowSpecialTime = -100f;
        private const float ShadowCooldown = 25f;

        // キャッシュ用スプライト
        private static Sprite cachedFireBallSprite;
        private static Sprite cachedIceBallSprite;
        private static Sprite cachedLightningSprite;
        private static Sprite cachedGreatswordSprite;
        private static Sprite cachedCircleSprite;
        private static Sprite cachedIceBlockSprite;

        public void Initialize(CharacterBase character, SpecialAttackStyle specialStyle, bool activate)
        {
            owner = character;
            style = specialStyle;
            IsActive = activate;
        }

        public string GetSkillDisplayName()
        {
            switch (style)
            {
                case SpecialAttackStyle.Taiken: return "溜め重斬り";
                case SpecialAttackStyle.Dagger: return "突進一閃";
                case SpecialAttackStyle.Flame: return "火球";
                case SpecialAttackStyle.Thunder: return "天の落雷";
                case SpecialAttackStyle.Ice: return "氷結弾";
                case SpecialAttackStyle.Shadow: return "影の領域";
                case SpecialAttackStyle.Ninja: return "飛雷神クナイ";
                default: return "必殺技";
            }
        }

        public float GetCooldownDuration()
        {
            if (style == SpecialAttackStyle.Shadow) return ShadowCooldown;
            return 0f;
        }

        public float GetCooldownRemaining()
        {
            if (style == SpecialAttackStyle.Shadow)
            {
                float elapsed = Time.time - lastShadowSpecialTime;
                return Mathf.Max(0f, ShadowCooldown - elapsed);
            }
            return 0f;
        }

        public string GetCooldownStatusText()
        {
            if (owner != null && owner.IsSpecialSilenced())
            {
                return "<color=#cc44ff>封印中</color>";
            }

            if (style == SpecialAttackStyle.Shadow)
            {
                float remain = GetCooldownRemaining();
                if (remain > 0.05f) return string.Format("<color=#ffaa00>{0:F1}s</color>", remain);
                return "<color=#00ff88>READY</color>";
            }

            float neededMana = GetRequiredMana();
            if (neededMana > 0f && owner != null && owner.currentMana < neededMana)
            {
                return string.Format("<color=#ff4444>M:{0:F0}</color>", neededMana);
            }

            return "<color=#00ff88>READY</color>";
        }

        public bool CanInputSpecialDuringAttack
        {
            get
            {
                return (style == SpecialAttackStyle.Ninja && activeKunai != null && activeKunai.CurrentState == NinjaYellowKunai.KunaiState.Placed);
            }
        }

        public float GetRequiredMana()
        {
            switch (style)
            {
                case SpecialAttackStyle.Taiken: return 30f;
                case SpecialAttackStyle.Dagger: return 20f;
                case SpecialAttackStyle.Flame: return 20f;
                case SpecialAttackStyle.Thunder: return 25f;
                case SpecialAttackStyle.Ice: return 15f;
                default: return 0f;
            }
        }

        public void ExecuteSpecial()
        {
            if (owner == null) owner = GetComponent<CharacterBase>();
            if (owner == null || owner.isDead || owner.isStunned || owner.IsInGuardBreak() || owner.isBlockingState) return;

            // 沈黙フィールド判定
            if (owner.IsSpecialSilenced())
            {
                Debug.Log(string.Format("[CustomSpecial] {0} は影の領域（沈黙フィールド）内のためスペシャル攻撃を発動できません！", owner.name));
                return;
            }

            switch (style)
            {
                case SpecialAttackStyle.Taiken:
                    ExecuteTaikenSpecial();
                    break;
                case SpecialAttackStyle.Dagger:
                    ExecuteDaggerSpecial();
                    break;
                case SpecialAttackStyle.Flame:
                    ExecuteFlameSpecial();
                    break;
                case SpecialAttackStyle.Thunder:
                    ExecuteThunderSpecial();
                    break;
                case SpecialAttackStyle.Ice:
                    ExecuteIceSpecial();
                    break;
                case SpecialAttackStyle.Shadow:
                    ExecuteShadowSpecial();
                    break;
                case SpecialAttackStyle.Ninja:
                    ExecuteNinjaSpecial();
                    break;
            }
        }

        // =========================================================================
        // 1. TAIKEN: 溜め重斬り
        // =========================================================================
        private void ExecuteTaikenSpecial()
        {
            if (owner.currentMana < 30f)
            {
                Debug.Log(string.Format("[CustomSpecial] マナが不足しています (必要: 30, 現在: {0:F0})", owner.currentMana));
                return;
            }
            owner.currentMana -= 30f;

            if (activeSpecialCoroutine != null) StopCoroutine(activeSpecialCoroutine);
            activeSpecialCoroutine = StartCoroutine(TaikenSpecialRoutine());
        }

        private IEnumerator TaikenSpecialRoutine()
        {
            float facing = owner.transform.localScale.x >= 0 ? 1f : -1f;

            GameObject swordObj = new GameObject("SpectralGreatsword");
            swordObj.transform.position = owner.transform.position + new Vector3(facing * 0.4f, 0.4f, 0f);
            SpriteRenderer sr = swordObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateGreatswordSprite();
            sr.color = new Color(1.0f, 0.88f, 0.35f, 0.95f);
            sr.sortingOrder = 14;
            swordObj.transform.localScale = Vector3.one * 1.6f;

            float windupTime = 0.25f;
            float elapsed = 0f;
            Quaternion startRot = Quaternion.Euler(0f, 0f, facing > 0 ? 80f : -80f);
            Quaternion endRot = Quaternion.Euler(0f, 0f, facing > 0 ? -40f : 40f);
            swordObj.transform.rotation = startRot;

            while (elapsed < windupTime)
            {
                elapsed += Time.deltaTime;
                swordObj.transform.position = owner.transform.position + new Vector3(facing * 0.4f, 0.4f, 0f);
                yield return null;
            }

            GameObject hitboxObj = new GameObject("GreatswordHitbox");
            hitboxObj.transform.position = owner.transform.position + new Vector3(facing * 1.6f, 0.2f, 0f);
            BoxCollider2D col = hitboxObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2.6f, 2.2f);

            Hitbox hb = hitboxObj.AddComponent<Hitbox>();
            hb.damage = 35;
            hb.postureDamageMultiplier = 2.0f;
            hb.ownerPlayerID = owner.playerID;
            hb.owner = owner;
            hb.isNormalAttack = false;

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.28f, 0.16f);
            }

            elapsed = 0f;
            float swingTime = 0.12f;
            while (elapsed < swingTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / swingTime);
                swordObj.transform.rotation = Quaternion.Lerp(startRot, endRot, t);
                swordObj.transform.position = owner.transform.position + new Vector3(facing * (0.4f + t * 0.6f), 0.4f - t * 0.4f, 0f);
                yield return null;
            }

            Destroy(hitboxObj);

            elapsed = 0f;
            while (elapsed < 0.15f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.15f);
                if (sr != null) sr.color = new Color(1.0f, 0.88f, 0.35f, 0.95f * (1f - t));
                yield return null;
            }

            if (swordObj != null) Destroy(swordObj);
        }

        // =========================================================================
        // 2. Dagger: 突進一閃
        // =========================================================================
        private void ExecuteDaggerSpecial()
        {
            if (owner.currentMana < 20f)
            {
                Debug.Log(string.Format("[CustomSpecial] マナが不足しています (必要: 20, 現在: {0:F0})", owner.currentMana));
                return;
            }
            owner.currentMana -= 20f;

            if (activeSpecialCoroutine != null) StopCoroutine(activeSpecialCoroutine);
            activeSpecialCoroutine = StartCoroutine(DaggerStepSlashRoutine());
        }

        private IEnumerator DaggerStepSlashRoutine()
        {
            float facing = owner.transform.localScale.x >= 0 ? 1f : -1f;
            Rigidbody2D rb = owner.GetComponent<Rigidbody2D>();

            float dashTime = 0.15f;
            float elapsed = 0f;

            GameObject hitboxObj = new GameObject("DaggerSpecialHitbox");
            hitboxObj.transform.position = owner.transform.position + new Vector3(facing * 1.0f, 0.1f, 0f);
            hitboxObj.transform.SetParent(owner.transform);
            BoxCollider2D col = hitboxObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2.0f, 1.4f);

            Hitbox hb = hitboxObj.AddComponent<Hitbox>();
            hb.damage = 25;
            hb.postureDamageMultiplier = 1.3f;
            hb.ownerPlayerID = owner.playerID;
            hb.owner = owner;
            hb.isNormalAttack = false;

            while (elapsed < dashTime)
            {
                elapsed += Time.deltaTime;
                if (rb != null)
                {
                    rb.linearVelocity = new Vector2(facing * 20f, rb.linearVelocity.y);
                }
                SpawnGhostTrail(owner.transform.position, owner.transform.localScale);
                yield return null;
            }

            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            Destroy(hitboxObj);
        }

        private void SpawnGhostTrail(Vector3 pos, Vector3 scale)
        {
            GameObject ghost = new GameObject("GhostTrail");
            ghost.transform.position = pos;
            ghost.transform.localScale = scale;
            SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateCircleSprite();
            sr.color = new Color(0.3f, 0.85f, 1.0f, 0.45f);
            sr.sortingOrder = 6;
            StartCoroutine(FadeAndDestroy(ghost, 0.18f));
        }

        // =========================================================================
        // 3. Flame: 火球
        // =========================================================================
        private void ExecuteFlameSpecial()
        {
            if (owner.currentMana < 20f)
            {
                Debug.Log(string.Format("[CustomSpecial] マナが不足しています (必要: 20, 現在: {0:F0})", owner.currentMana));
                return;
            }
            owner.currentMana -= 20f;

            float facing = owner.transform.localScale.x >= 0 ? 1f : -1f;
            Vector3 spawnPos = owner.transform.position + new Vector3(facing * 1.0f, 0.2f, 0f);

            GameObject fb = new GameObject("FireBall_Projectile");
            fb.transform.position = spawnPos;
            fb.transform.localScale = Vector3.one * 1.3f;

            SpriteRenderer sr = fb.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateFireBallSprite();
            sr.color = Color.white;
            sr.sortingOrder = 9;

            CircleCollider2D circle = fb.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.5f;

            Hitbox hb = fb.AddComponent<Hitbox>();
            hb.damage = 22;
            hb.ownerPlayerID = owner.playerID;
            hb.owner = owner;
            hb.isNormalAttack = false;
            hb.isProjectile = true;
            hb.postureDamageMultiplier = 1.2f;

            StartCoroutine(FireBallFlightRoutine(fb, hb, facing));
        }

        private IEnumerator FireBallFlightRoutine(GameObject ball, Hitbox hb, float facing)
        {
            float speed = 13.0f;
            float maxLifetime = 2.0f;
            float elapsed = 0f;
            bool hitLanded = false;

            if (hb != null)
            {
                hb.OnHitLanded += (hurtbox, dmg) =>
                {
                    hitLanded = true;
                };
            }

            while (elapsed < maxLifetime && !hitLanded && ball != null)
            {
                elapsed += Time.deltaTime;
                ball.transform.position += new Vector3(facing * speed * Time.deltaTime, 0f, 0f);
                ball.transform.Rotate(0f, 0f, -facing * 720f * Time.deltaTime);
                yield return null;
            }

            if (ball != null)
            {
                SpawnExplosionEffect(ball.transform.position);
                Destroy(ball);
            }
        }

        private void SpawnExplosionEffect(Vector3 pos)
        {
            GameObject exp = new GameObject("ExplosionEffect");
            exp.transform.position = pos;
            SpriteRenderer sr = exp.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateCircleSprite();
            sr.color = new Color(1.0f, 0.5f, 0.1f, 0.85f);
            sr.sortingOrder = 15;
            exp.transform.localScale = Vector3.one * 0.8f;

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.18f, 0.08f);
            }

            StartCoroutine(ScaleAndFade(exp, 1.8f, 0.22f));
        }

        // =========================================================================
        // 4. Thunder: 天の落雷
        // =========================================================================
        private void ExecuteThunderSpecial()
        {
            if (owner.currentMana < 25f)
            {
                Debug.Log(string.Format("[CustomSpecial] マナが不足しています (必要: 25, 現在: {0:F0})", owner.currentMana));
                return;
            }
            owner.currentMana -= 25f;

            if (activeSpecialCoroutine != null) StopCoroutine(activeSpecialCoroutine);
            activeSpecialCoroutine = StartCoroutine(ThunderStrikeRoutine());
        }

        private IEnumerator ThunderStrikeRoutine()
        {
            CharacterBase target = FindOpponent();
            Vector3 targetPos = target != null ? target.transform.position : owner.transform.position + new Vector3(owner.transform.localScale.x * 4.5f, 0f, 0f);

            GameObject reticle = new GameObject("ThunderReticle");
            reticle.transform.position = targetPos;
            SpriteRenderer retSr = reticle.AddComponent<SpriteRenderer>();
            retSr.sprite = GetOrCreateCircleSprite();
            retSr.color = new Color(0.2f, 0.85f, 1.0f, 0.65f);
            retSr.sortingOrder = 8;
            reticle.transform.localScale = new Vector3(1.8f, 0.6f, 1f);

            float chargeTime = 0.45f;
            float elapsed = 0f;
            while (elapsed < chargeTime)
            {
                elapsed += Time.deltaTime;
                if (target != null && !target.isDead)
                {
                    targetPos = target.transform.position;
                    if (reticle != null) reticle.transform.position = targetPos;
                }
                yield return null;
            }

            if (reticle != null) Destroy(reticle);

            GameObject bolt = new GameObject("ThunderBoltStrike");
            bolt.SetActive(false);
            bolt.transform.position = new Vector3(targetPos.x, targetPos.y + 5.5f, 0f);
            bolt.transform.localScale = new Vector3(1.6f, 13f, 1f);

            SpriteRenderer sr = bolt.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateLightningSprite();
            sr.color = Color.white;
            sr.sortingOrder = 13;

            BoxCollider2D col = bolt.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.2f, 1.0f);

            Hitbox hb = bolt.AddComponent<Hitbox>();
            hb.damage = 25;
            hb.postureDamageMultiplier = 2.5f;
            hb.isNormalAttack = false;
            hb.ownerPlayerID = owner.playerID;
            hb.owner = owner;

            bolt.SetActive(true);

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.35f, 0.22f);
            }

            StartCoroutine(FadeAndDestroy(bolt, 0.25f));
        }

        // =========================================================================
        // 5. Ice: 氷結弾 (相手を確実に1.5秒凍結！)
        // =========================================================================
        private void ExecuteIceSpecial()
        {
            if (owner.currentMana < 15f)
            {
                Debug.Log(string.Format("[CustomSpecial] マナが不足しています (必要: 15, 現在: {0:F0})", owner.currentMana));
                return;
            }
            owner.currentMana -= 15f;

            float facing = owner.transform.localScale.x >= 0 ? 1f : -1f;
            Vector3 spawnPos = owner.transform.position + new Vector3(facing * 1.1f, 0.2f, 0f);

            GameObject ball = new GameObject("IceBall_Projectile");
            ball.transform.position = spawnPos;
            ball.transform.localScale = Vector3.one * 1.25f;

            SpriteRenderer sr = ball.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateIceBallSprite();
            sr.color = Color.white;
            sr.sortingOrder = 9;

            CircleCollider2D circle = ball.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.45f;

            Hitbox hb = ball.AddComponent<Hitbox>();
            hb.damage = 8;
            hb.ownerPlayerID = owner.playerID;
            hb.owner = owner;
            hb.isNormalAttack = false;
            hb.isProjectile = true;
            hb.postureDamageMultiplier = 1.0f;

            StartCoroutine(IceBallFlightRoutine(ball, hb, facing));
        }

        private IEnumerator IceBallFlightRoutine(GameObject ball, Hitbox hb, float facing)
        {
            float speed = 14.0f;
            float maxLifetime = 2.0f;
            float elapsed = 0f;
            bool hitLanded = false;

            if (hb != null)
            {
                hb.OnHitLanded += (hurtbox, dmg) =>
                {
                    if (hitLanded) return;
                    hitLanded = true;
                    if (hurtbox != null && hurtbox.owner != null)
                    {
                        CharacterBase target = hurtbox.owner;

                        // 向き判定（クナイ同様、正面ガードされているかチェック）
                        float faceDir = target.transform.localScale.x;
                        float relX = (ball != null ? ball.transform.position.x : transform.position.x) - target.transform.position.x;
                        bool isFacing = (faceDir > 0 && relX > 0) || (faceDir < 0 && relX < 0);
                        bool isGuarding = target.isBlockingState;

                        if (isGuarding && isFacing)
                        {
                            AudioManager.PlayBlockSound();
                            Debug.Log(string.Format("[CustomSpecial] 敵 (P{0}) が氷結弾をガード！凍結を防ぎました。", target.playerID));
                        }
                        else
                        {
                            // ガードされていなければ確実に1.5秒凍結！
                            Debug.Log(string.Format("[CustomSpecial] ★ 敵 (P{0}) に氷結弾命中！1.5秒間氷箱に完全凍結！ ★", target.playerID));
                            StartCoroutine(FreezeTargetRoutine(target, 1.5f));
                        }
                    }
                };
            }

            while (elapsed < maxLifetime && !hitLanded && ball != null)
            {
                elapsed += Time.deltaTime;
                ball.transform.position += new Vector3(facing * speed * Time.deltaTime, 0f, 0f);
                ball.transform.Rotate(0f, 0f, -facing * 500f * Time.deltaTime);
                yield return null;
            }

            if (ball != null)
            {
                SpawnIceShatter(ball.transform.position);
                Destroy(ball);
            }
        }

        /// <summary>
        /// 敵キャラクターを水色の氷ブロック（IceBlock_Visual）で1.5秒間完全凍結・拘束！
        /// </summary>
        private IEnumerator FreezeTargetRoutine(CharacterBase target, float duration)
        {
            if (target == null || target.isDead) yield break;

            // 1. 水色の半透明な氷箱を生成し、キャラクターを包み込む
            GameObject iceBlock = new GameObject("IceBlock_Visual");
            iceBlock.transform.SetParent(target.transform, false);
            iceBlock.transform.localPosition = new Vector3(0f, 0f, 0f);
            iceBlock.transform.localScale = Vector3.one;

            SpriteRenderer blockSr = iceBlock.AddComponent<SpriteRenderer>();
            blockSr.sprite = GetOrCreateIceBlockSprite();
            blockSr.color = new Color(0.35f, 0.85f, 1.0f, 0.70f);
            blockSr.sortingOrder = 18;
            iceBlock.transform.localScale = new Vector3(1.35f, 2.35f, 1f);

            // 氷箱の外枠輝きハイライト
            GameObject borderObj = new GameObject("IceBlock_Border");
            borderObj.transform.SetParent(iceBlock.transform, false);
            borderObj.transform.localPosition = Vector3.zero;
            SpriteRenderer borderSr = borderObj.AddComponent<SpriteRenderer>();
            borderSr.sprite = GetOrCreateIceBlockSprite();
            borderSr.color = new Color(0.9f, 0.98f, 1.0f, 0.9f);
            borderSr.sortingOrder = 19;
            borderObj.transform.localScale = new Vector3(1.08f, 1.06f, 1f);

            // 2. 移動・アクション完全停止
            Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
            Animator targetAnim = target.GetComponentInChildren<Animator>();
            float prevAnimSpeed = (targetAnim != null) ? targetAnim.speed : 1f;
            if (targetAnim != null) targetAnim.speed = 0f;

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.14f, 0.08f);
            }

            float timer = 0f;
            while (timer < duration && target != null && !target.isDead)
            {
                timer += Time.deltaTime;
                target.isStunned = true;
                if (targetRb != null) targetRb.linearVelocity = Vector2.zero;

                // 氷箱の冷気パルス明滅
                if (blockSr != null)
                {
                    float shimmer = 0.65f + Mathf.Sin(timer * 22f) * 0.12f;
                    blockSr.color = new Color(0.35f, 0.85f, 1.0f, shimmer);
                }
                yield return null;
            }

            // 3. 凍結解除 & 粉砕エフェクト
            if (targetAnim != null) targetAnim.speed = prevAnimSpeed;
            if (target != null && !target.isDead)
            {
                target.isStunned = false;
                Debug.Log(string.Format("[CustomSpecial] 敵 (P{0}) の凍結拘束が解除されました！", target.playerID));
            }

            if (iceBlock != null)
            {
                Vector3 breakPos = iceBlock.transform.position;
                Destroy(iceBlock);
                SpawnIceShatter(breakPos);
            }
        }

        private void SpawnIceShatter(Vector3 pos)
        {
            for (int i = 0; i < 6; i++)
            {
                GameObject shard = new GameObject("IceShard");
                shard.transform.position = pos + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.6f, 0.6f), 0f);
                shard.transform.localScale = Vector3.one * Random.Range(0.2f, 0.4f);
                SpriteRenderer sr = shard.AddComponent<SpriteRenderer>();
                sr.sprite = GetOrCreateCircleSprite();
                sr.color = new Color(0.55f, 0.9f, 1f, 0.9f);
                sr.sortingOrder = 19;
                StartCoroutine(ScaleAndFade(shard, 1.5f, 0.22f));
            }
        }

        // =========================================================================
        // 6. Shadow: 影の領域
        // =========================================================================
        private void ExecuteShadowSpecial()
        {
            float elapsed = Time.time - lastShadowSpecialTime;
            if (elapsed < ShadowCooldown)
            {
                Debug.Log(string.Format("[CustomSpecial] 影の領域はクールダウン中です（残り {0:F1} 秒）", ShadowCooldown - elapsed));
                return;
            }

            lastShadowSpecialTime = Time.time;
            ShadowDomainField.Spawn(owner.transform.position, ShadowCooldown);

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.22f, 0.12f);
            }

            Debug.Log("[CustomSpecial] ★ 影の領域（沈黙フィールド）を展開しました！");
        }

        // =========================================================================
        // 7. Ninja: 飛雷神クナイ
        // =========================================================================
        private void ExecuteNinjaSpecial()
        {
            if (Time.time - lastKunaiActionTime < KunaiActionInterval) return;

            if (activeKunai != null && activeKunai.CurrentState == NinjaYellowKunai.KunaiState.Placed)
            {
                lastKunaiActionTime = Time.time;
                Vector3 dest = activeKunai.transform.position + Vector3.up * 0.25f;
                owner.transform.position = dest;
                AudioManager.PlayTeleportSound();

                CharacterBase opp = FindOpponent();
                if (opp != null)
                {
                    float dx = opp.transform.position.x - owner.transform.position.x;
                    if (Mathf.Abs(dx) > 0.1f)
                    {
                        float sign = dx > 0 ? 1f : -1f;
                        Vector3 s = owner.transform.localScale;
                        owner.transform.localScale = new Vector3(Mathf.Abs(s.x) * sign, s.y, s.z);
                    }
                }

                activeKunai.Dispel(false);
                activeKunai = null;
                Debug.Log("[CustomSpecial] クナイへ瞬間移動完了！");
                return;
            }

            if (activeKunai != null && activeKunai.CurrentState == NinjaYellowKunai.KunaiState.Flying)
            {
                lastKunaiActionTime = Time.time;
                activeKunai.DropAndPlant();
                return;
            }

            if (activeKunai == null || activeKunai.CurrentState == NinjaYellowKunai.KunaiState.Dispelled)
            {
                lastKunaiActionTime = Time.time;
                float facing = owner.transform.localScale.x >= 0 ? 1f : -1f;
                Vector3 spawnPos = owner.transform.position + new Vector3(facing * 0.9f, 0.2f, 0f);

                GameObject kunaiGo = new GameObject("CustomYellowKunai");
                kunaiGo.transform.position = spawnPos;

                activeKunai = kunaiGo.AddComponent<NinjaYellowKunai>();
                activeKunai.Initialize(owner, facing, 16.0f);
                activeKunai.damage = 10;

                activeKunai.OnDispelled += (k) =>
                {
                    if (activeKunai == k) activeKunai = null;
                };

                AudioManager.PlayWarningSound();
                Debug.Log("[CustomSpecial] クナイを投擲しました！");
            }
        }

        private CharacterBase FindOpponent()
        {
            CharacterBase[] all = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (var c in all)
            {
                if (c != owner && !c.isDead) return c;
            }
            return null;
        }

        // =========================================================================
        // コルーチン演出ヘルパー
        // =========================================================================
        private IEnumerator FadeAndDestroy(GameObject obj, float duration)
        {
            float elapsed = 0f;
            SpriteRenderer sr = obj != null ? obj.GetComponent<SpriteRenderer>() : null;
            Color initColor = sr != null ? sr.color : Color.white;

            while (elapsed < duration && obj != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (sr != null) sr.color = new Color(initColor.r, initColor.g, initColor.b, initColor.a * (1f - t));
                yield return null;
            }

            if (obj != null) Destroy(obj);
        }

        private IEnumerator ScaleAndFade(GameObject obj, float maxScale, float duration)
        {
            float elapsed = 0f;
            SpriteRenderer sr = obj != null ? obj.GetComponent<SpriteRenderer>() : null;
            Vector3 initScale = obj != null ? obj.transform.localScale : Vector3.one;
            Color initColor = sr != null ? sr.color : Color.white;

            while (elapsed < duration && obj != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (obj != null) obj.transform.localScale = initScale * Mathf.Lerp(1.0f, maxScale, t);
                if (sr != null) sr.color = new Color(initColor.r, initColor.g, initColor.b, initColor.a * (1f - t));
                yield return null;
            }

            if (obj != null) Destroy(obj);
        }

        // =========================================================================
        // プロシージャル テクスチャ＆スプライト生成
        // =========================================================================
        public static Sprite GetOrCreateCircleSprite()
        {
            if (cachedCircleSprite != null) return cachedCircleSprite;
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    if (d <= radius)
                    {
                        float alpha = Mathf.Clamp01((radius - d) / 1.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return cachedCircleSprite;
        }

        public static Sprite GetOrCreateIceBlockSprite()
        {
            if (cachedIceBlockSprite != null) return cachedIceBlockSprite;
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color fill = new Color(0.85f, 0.96f, 1.0f, 1.0f);
            Color border = Color.white;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (x < 2 || x >= size - 2 || y < 2 || y >= size - 2)
                    {
                        tex.SetPixel(x, y, border);
                    }
                    else
                    {
                        tex.SetPixel(x, y, fill);
                    }
                }
            }
            tex.Apply();
            cachedIceBlockSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return cachedIceBlockSprite;
        }

        public static Sprite GetOrCreateFireBallSprite()
        {
            if (cachedFireBallSprite != null) return cachedFireBallSprite;
            int size = 48;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.46f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    if (d <= radius)
                    {
                        float norm = d / radius;
                        if (norm < 0.35f) tex.SetPixel(x, y, new Color(1f, 1f, 0.8f, 1f));
                        else if (norm < 0.7f) tex.SetPixel(x, y, new Color(1f, 0.6f, 0.1f, 0.95f));
                        else tex.SetPixel(x, y, new Color(0.9f, 0.15f, 0.05f, (1f - norm) * 2f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            cachedFireBallSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return cachedFireBallSprite;
        }

        public static Sprite GetOrCreateIceBallSprite()
        {
            if (cachedIceBallSprite != null) return cachedIceBallSprite;
            int size = 40;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.46f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    if (d <= radius)
                    {
                        float norm = d / radius;
                        if (norm < 0.3f) tex.SetPixel(x, y, new Color(0.95f, 1f, 1f, 1f));
                        else if (norm < 0.75f) tex.SetPixel(x, y, new Color(0.4f, 0.85f, 1f, 0.95f));
                        else tex.SetPixel(x, y, new Color(0.1f, 0.4f, 0.95f, (1f - norm) * 2.5f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            cachedIceBallSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return cachedIceBallSprite;
        }

        public static Sprite GetOrCreateLightningSprite()
        {
            if (cachedLightningSprite != null) return cachedLightningSprite;
            int width = 24;
            int height = 128;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float centerX = width * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Abs(x - centerX);
                    if (dx <= 3f) tex.SetPixel(x, y, Color.white);
                    else if (dx <= 8f) tex.SetPixel(x, y, new Color(0.3f, 0.8f, 1.0f, 0.8f));
                    else if (dx <= 11f) tex.SetPixel(x, y, new Color(0.1f, 0.4f, 1.0f, 0.3f));
                    else tex.SetPixel(x, y, Color.clear);
                }
            }
            tex.Apply();
            cachedLightningSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
            return cachedLightningSprite;
        }

        public static Sprite GetOrCreateGreatswordSprite()
        {
            if (cachedGreatswordSprite != null) return cachedGreatswordSprite;
            int width = 36;
            int height = 80;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Abs(x - 18f);
                    if (y >= 20 && y <= 75 && dx <= 12f)
                    {
                        if (dx <= 3f) tex.SetPixel(x, y, new Color(1f, 1f, 0.9f, 1f));
                        else tex.SetPixel(x, y, new Color(1f, 0.82f, 0.2f, 0.95f));
                    }
                    else if (y >= 14 && y < 20 && dx <= 16f)
                    {
                        tex.SetPixel(x, y, new Color(0.95f, 0.7f, 0.1f, 1f));
                    }
                    else if (y < 14 && dx <= 4f)
                    {
                        tex.SetPixel(x, y, new Color(0.2f, 0.2f, 0.3f, 1f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            cachedGreatswordSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.15f), 32f);
            return cachedGreatswordSprite;
        }
    }
}
