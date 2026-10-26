using System.Collections;
using UnityEngine;

namespace FightingGameBase
{
    /// <summary>
    /// Ninjaのスペシャル攻撃用「少しだけ大きい黄色いクナイ」投擲オブジェクト。
    /// 
    /// 【完全な3段階シーケンス（1入力＝1アクション）】
    /// 1. 投げる（1回目入力）: 前方へ水平飛行。敵に当たれば10ダメージを与える。
    ///    - パリーされた場合は即座に弾かれて消滅。
    ///    - 敵命中時も自動落下せず飛行を継続（プレイヤーの明示的な入力で設置させるため）。
    /// 2. 設置（2回目入力）: 飛行中に再入力すると、その場から真下へ高速落下し地面に突き刺さって設置（Placed）される。
    /// 3. 瞬間移動（3回目入力）: 地面に設置完了後、再入力すると忍者が黄色い閃光とともにこの位置へ瞬間移動する。
    /// </summary>
    public class NinjaYellowKunai : MonoBehaviour
    {
        public enum KunaiState
        {
            Flying,     // 前方へ水平飛行中（ステップ1）
            Dropping,   // 真下へ急降下中（ステップ2の移動中）
            Placed,     // 地面に突き刺さって設置完了（ステップ2完了・ステップ3待機）
            Dispelled   // 消滅
        }

        public KunaiState CurrentState { get; private set; } = KunaiState.Flying;

        public CharacterBase owner { get; private set; }
        public int ownerPlayerID { get; private set; } = 1;
        public int damage = 10;
        public float flySpeed = 16.0f;
        public float dropSpeed = 24.0f;
        public float maxFlyTime = 2.2f;

        public System.Action<NinjaYellowKunai> OnDispelled;

        private float facing = 1f;
        private float flyTimer = 0f;
        private float targetGroundY = -1.2f;
        private bool hasDealtDamage = false;

        private Collider2D kunaiCol;
        private Hitbox kunaiHitbox;
        private SpriteRenderer mainRenderer;
        private SpriteRenderer auraRenderer;

        private static Sprite cachedKunaiSprite;
        private static Sprite cachedCircleSprite;

        public void Initialize(CharacterBase ownerNinja, float facingDirection, float speed = 16.0f)
        {
            owner = ownerNinja;
            ownerPlayerID = ownerNinja != null ? ownerNinja.playerID : 1;
            facing = facingDirection >= 0 ? 1f : -1f;
            flySpeed = speed;
            CurrentState = KunaiState.Flying;
            flyTimer = 0f;
            hasDealtDamage = false;

            SetupVisualsAndColliders();

            // 飛行方向へクナイを向ける（刃先を進行方向へ）
            transform.rotation = Quaternion.Euler(0f, 0f, facing > 0 ? 0f : 180f);
        }

        private void SetupVisualsAndColliders()
        {
            // 1. クナイ本体スプライト
            GameObject visualObj = new GameObject("KunaiVisual");
            visualObj.transform.SetParent(transform, false);
            mainRenderer = visualObj.AddComponent<SpriteRenderer>();
            mainRenderer.sprite = GetOrCreateKunaiSprite();
            mainRenderer.sortingOrder = 9;

            // 少しだけ大きいサイズ（標準クナイの約1.45倍）
            transform.localScale = Vector3.one * 1.45f;

            // 2. 黄色い電光オーラ
            GameObject auraObj = new GameObject("KunaiAura");
            auraObj.transform.SetParent(transform, false);
            auraRenderer = auraObj.AddComponent<SpriteRenderer>();
            auraRenderer.sprite = GetOrCreateCircleSprite();
            auraRenderer.color = new Color(1.0f, 0.90f, 0.15f, 0.40f);
            auraRenderer.sortingOrder = 8;
            auraObj.transform.localScale = new Vector3(1.6f, 0.85f, 1f);

            // 3. 当たり判定（Hitbox & Trigger）
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.45f;
            kunaiCol = circle;

            kunaiHitbox = gameObject.AddComponent<Hitbox>();
            kunaiHitbox.damage = damage;
            kunaiHitbox.ownerPlayerID = ownerPlayerID;
            kunaiHitbox.owner = owner;
            kunaiHitbox.isNormalAttack = false;
            kunaiHitbox.postureDamageMultiplier = 1.0f;

            // 命中時コールバック（ダメージ＆パリー判定）
            kunaiHitbox.OnHitLanded += HandleHitLanded;
        }

        private void HandleHitLanded(Hurtbox hurtbox, int finalDmg)
        {
            if (CurrentState == KunaiState.Dispelled) return;
            if (hasDealtDamage) return;

            if (hurtbox != null && hurtbox.owner != null)
            {
                CharacterBase target = hurtbox.owner;

                // 【パリー判定】相手がパリー受け付け中であれば、クナイは消滅する！
                if (target.IsInParryWindow())
                {
                    Debug.Log($"[NinjaYellowKunai] ★ 敵 (P{target.playerID}) がクナイをパリー！クナイは弾かれて消滅しました ★");
                    Dispel(true); // wasParried = true
                    return;
                }

                // 【通常命中】10ダメージを与える（自動落下はせず、飛行を継続。プレイヤーの第2入力で落下・設置する）
                hasDealtDamage = true;
                Debug.Log($"[NinjaYellowKunai] 敵 (P{target.playerID}) にクナイが命中！（{damage}ダメージ）");
            }
        }

        void Update()
        {
            switch (CurrentState)
            {
                case KunaiState.Flying:
                    UpdateFlying();
                    break;
                case KunaiState.Dropping:
                    UpdateDropping();
                    break;
                case KunaiState.Placed:
                    UpdatePlaced();
                    break;
            }
        }

        private void UpdateFlying()
        {
            flyTimer += Time.deltaTime;

            // 前方へ高速直進
            transform.position += new Vector3(facing * flySpeed * Time.deltaTime, 0f, 0f);

            // 飛行中の黄色い光の微振動
            if (auraRenderer != null)
            {
                float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 25f);
                auraRenderer.transform.localScale = new Vector3(1.6f * pulse, 0.85f * pulse, 1f);
            }

            // 最大飛行時間を超えたら自動で落下・設置へ移行
            if (flyTimer >= maxFlyTime)
            {
                DropAndPlant();
            }
        }

        /// <summary>
        /// その場から真下へ高速落下して地面に突き刺さる（2回目入力時、または最大飛行時間到達時）
        /// </summary>
        public void DropAndPlant()
        {
            if (CurrentState != KunaiState.Flying) return;
            CurrentState = KunaiState.Dropping;

            // 直下の地面のY座標をレイキャストで検出
            RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 40f);
            if (hit.collider != null && !hit.collider.isTrigger)
            {
                targetGroundY = hit.point.y + 0.15f;
            }
            else
            {
                targetGroundY = -1.25f; // デフォルト床面
            }

            // 落下姿勢（刃先を下に向けて急速降下）
            transform.rotation = Quaternion.Euler(0f, 0f, -85f * facing);

            Debug.Log($"[NinjaYellowKunai] クナイ落下開始！目標地面Y: {targetGroundY:F2}");
        }

        private void UpdateDropping()
        {
            // 真下へ急速落下
            transform.position += new Vector3(0f, -dropSpeed * Time.deltaTime, 0f);

            // 地面に到達
            if (transform.position.y <= targetGroundY)
            {
                transform.position = new Vector3(transform.position.x, targetGroundY, transform.position.z);
                PlantOnGround();
            }
        }

        private void PlantOnGround()
        {
            CurrentState = KunaiState.Placed;

            // 地面に斜め約65度でグサッと突き刺さる角度
            transform.rotation = Quaternion.Euler(0f, 0f, -65f * facing);

            // 設置後は多重ヒットを防ぐため当たり判定をOFF
            if (kunaiCol != null) kunaiCol.enabled = false;

            // 着地時の小爆発・衝撃波エフェクト
            StartCoroutine(SpawnGroundImpactVFX());

            // 飛雷神マーカー波紋アニメーションを開始
            StartCoroutine(PlantedMarkerPulseRoutine());

            Debug.Log("[NinjaYellowKunai] ★ クナイが地面に設置完了しました！3回目の入力で黄色い閃光とともに瞬間移動可能 ★");
        }

        private void UpdatePlaced()
        {
            // 設置中の電光スパークパルス
            if (auraRenderer != null)
            {
                float pulse = 1f + 0.25f * Mathf.Sin(Time.time * 12f);
                auraRenderer.color = new Color(1.0f, 0.92f, 0.2f, 0.5f * pulse);
            }
        }

        private IEnumerator PlantedMarkerPulseRoutine()
        {
            while (CurrentState == KunaiState.Placed)
            {
                // 地面に黄色い波紋リングがフワッと広がる
                GameObject wave = new GameObject("MarkerWave");
                wave.transform.SetParent(transform, false);
                wave.transform.localPosition = Vector3.zero;
                SpriteRenderer waveSr = wave.AddComponent<SpriteRenderer>();
                waveSr.sprite = GetOrCreateCircleSprite();
                waveSr.color = new Color(1.0f, 0.88f, 0.2f, 0.75f);
                waveSr.sortingOrder = 7;

                float waveDuration = 0.65f;
                float wElapsed = 0f;
                while (wElapsed < waveDuration && CurrentState == KunaiState.Placed)
                {
                    wElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(wElapsed / waveDuration);
                    if (wave != null)
                    {
                        wave.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.8f, t);
                        waveSr.color = new Color(1.0f, 0.88f, 0.2f, Mathf.Lerp(0.75f, 0f, t));
                    }
                    yield return null;
                }

                if (wave != null) Destroy(wave);
                yield return new WaitForSeconds(0.25f);
            }
        }

        private IEnumerator SpawnGroundImpactVFX()
        {
            for (int i = 0; i < 4; i++)
            {
                GameObject spark = new GameObject("Spark");
                spark.transform.position = transform.position + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0f, 0.3f), 0f);
                SpriteRenderer sparkSr = spark.AddComponent<SpriteRenderer>();
                sparkSr.sprite = GetOrCreateCircleSprite();
                sparkSr.color = new Color(1f, 0.95f, 0.3f, 0.9f);
                sparkSr.sortingOrder = 10;
                spark.transform.localScale = Vector3.one * Random.Range(0.12f, 0.22f);
                StartCoroutine(FadeSparkRoutine(spark, new Vector2(Random.Range(-2f, 2f), Random.Range(1.5f, 3.5f))));
            }
            yield return null;
        }

        private IEnumerator FadeSparkRoutine(GameObject spark, Vector2 velocity)
        {
            float elapsed = 0f;
            float duration = 0.25f;
            SpriteRenderer sr = spark.GetComponent<SpriteRenderer>();

            while (elapsed < duration && spark != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                spark.transform.position += (Vector3)(velocity * Time.deltaTime);
                if (sr != null)
                {
                    sr.color = new Color(1f, 0.95f, 0.3f, 1f - t);
                }
                yield return null;
            }

            if (spark != null) Destroy(spark);
        }

        /// <summary>
        /// クナイの消滅処理（パリーされた場合またはテレポート完了後）
        /// </summary>
        public void Dispel(bool wasParried)
        {
            if (CurrentState == KunaiState.Dispelled) return;
            CurrentState = KunaiState.Dispelled;

            OnDispelled?.Invoke(this);

            if (wasParried)
            {
                AudioManager.PlayParrySound();
                StartCoroutine(SpawnParrySparks(transform.position));
            }

            Destroy(gameObject);
        }

        private IEnumerator SpawnParrySparks(Vector3 pos)
        {
            for (int i = 0; i < 6; i++)
            {
                GameObject spark = new GameObject("ParrySpark");
                spark.transform.position = pos;
                SpriteRenderer sr = spark.AddComponent<SpriteRenderer>();
                sr.sprite = GetOrCreateCircleSprite();
                sr.color = new Color(1.0f, 1.0f, 0.8f, 1.0f);
                sr.sortingOrder = 15;
                spark.transform.localScale = Vector3.one * 0.18f;

                Vector2 vel = new Vector2(Random.Range(-4f, 4f), Random.Range(-2f, 4f));
                StartCoroutine(FadeSparkRoutine(spark, vel));
            }
            yield return null;
        }

        // =========================================================================
        // プロシージャル クナイ テクスチャ生成
        // =========================================================================
        public static Sprite GetOrCreateKunaiSprite()
        {
            if (cachedKunaiSprite != null) return cachedKunaiSprite;

            int width = 64;
            int height = 64;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color clear = Color.clear;
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            float centerY = 31.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dy = Mathf.Abs(y - centerY);

                    // 1. 円環リング (Ring / Pommel)
                    float ringCenterX = 10.5f;
                    float distRing = Mathf.Sqrt((x - ringCenterX) * (x - ringCenterX) + dy * dy);
                    if (distRing <= 6.5f && distRing >= 2.8f)
                    {
                        pixels[y * width + x] = new Color(0.95f, 0.78f, 0.1f, 1.0f);
                        continue;
                    }

                    // 2. 柄 (Handle)
                    if (x >= 16 && x <= 28 && dy <= 2.8f)
                    {
                        bool stripe = ((x + y) / 3) % 2 == 0;
                        pixels[y * width + x] = stripe ? new Color(0.18f, 0.20f, 0.28f, 1.0f) : new Color(0.95f, 0.75f, 0.1f, 1.0f);
                        continue;
                    }

                    // 3. 鍔 (Guard)
                    if (x >= 28 && x <= 31 && dy <= 7.0f)
                    {
                        pixels[y * width + x] = new Color(0.95f, 0.85f, 0.15f, 1.0f);
                        continue;
                    }

                    // 4. 菱形ブレード（Blade）
                    if (x >= 31 && x <= 60)
                    {
                        float bladeMaxHalfWidth = 7.5f;
                        float currentHalfWidth;
                        if (x <= 40)
                        {
                            currentHalfWidth = Mathf.Lerp(4.0f, bladeMaxHalfWidth, (x - 31) / 9.0f);
                        }
                        else
                        {
                            currentHalfWidth = Mathf.Lerp(bladeMaxHalfWidth, 0.0f, (x - 40) / 20.0f);
                        }

                        if (dy <= currentHalfWidth)
                        {
                            if (dy <= 1.2f)
                            {
                                pixels[y * width + x] = new Color(1.0f, 1.0f, 0.85f, 1.0f);
                            }
                            else if (y > centerY)
                            {
                                pixels[y * width + x] = new Color(1.0f, 0.92f, 0.25f, 1.0f);
                            }
                            else
                            {
                                pixels[y * width + x] = new Color(0.90f, 0.75f, 0.10f, 1.0f);
                            }
                        }
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();

            cachedKunaiSprite = Sprite.Create(
                tex,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                32f
            );
            return cachedKunaiSprite;
        }

        private static Sprite GetOrCreateCircleSprite()
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
