using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【DragonBlastProjectile（竜撃砲の弾・とび道具）】
    // 竜撃砲を放ったときに前方に飛んでいく巨大なエネルギー弾です。
    // 特徴：
    //   ・巨大な当たり判定と、激しく明滅するオレンジ＆イエローのコア
    //   ・後方に伸びる太いグラデーション付きのトレイル（軌跡）
    //   ・敵に命中すると、画面いっぱいに広がる大爆発エフェクトを発生させて消滅します
    // =================================================================================
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class DragonBlastProjectile : MonoBehaviour
    {
        [Tooltip("弾が自動的に消えるまでの時間（秒）")]
        public float lifetime = 3f;
        
        private float moveDirection;
        private float speed;
        private int damage;
        private int ownerPlayerID;
        
        private LineRenderer trailRenderer;
        private System.Collections.Generic.List<Vector3> trailPositions = new System.Collections.Generic.List<Vector3>();

        // ランタイムにアセットに依存せず真っ白なスプライトを動的生成するメソッド
        private static Sprite CreateDefaultSprite()
        {
            Texture2D tex = new Texture2D(2, 2);
            Color[] colors = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 竜撃砲の弾を初期化します。
        /// </summary>
        public void Initialize(float direction, float projectileSpeed, int projectileDamage, int playerID)
        {
            moveDirection = direction;
            speed = projectileSpeed;
            damage = projectileDamage;
            ownerPlayerID = playerID;

            // 1. 物理（Rigidbody2D）の設定
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f; // 重力の影響を受けない
            rb.linearVelocity = new Vector2(moveDirection * speed, 0f); // 真横に飛ぶ

            // 2. コライダーの設定
            BoxCollider2D col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2.5f, 1.5f); // 巨大な竜撃砲の当たり判定サイズ

            // 3. 向きの設定
            transform.localScale = new Vector3(moveDirection, 1f, 1f);

            // 4. 見た目（外側のオレンジ色の炎）のセットアップ
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDefaultSprite();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(2.5f, 1.5f);
            sr.color = new Color(1f, 0.3f, 0f, 1f); // 鮮やかなオレンジレッド

            // 5. 見た目（内側の白黄色のコア）のセットアップ
            GameObject core = new GameObject("Core");
            core.transform.SetParent(transform);
            core.transform.localPosition = Vector3.zero;
            SpriteRenderer coreSr = core.AddComponent<SpriteRenderer>();
            coreSr.sprite = sr.sprite;
            coreSr.drawMode = SpriteDrawMode.Sliced;
            coreSr.size = new Vector2(2.2f, 1.0f); // 外側より少し小さく配置
            coreSr.color = new Color(1f, 0.95f, 0.5f, 1f); // 輝く白黄色

            // 6. トレイル（ビームの尾尾）の作成
            CreateTrail();

            // 7. 自動消滅タイマー
            Hitbox hb = gameObject.AddComponent<Hitbox>();
            hb.damage = damage;
            hb.ownerPlayerID = ownerPlayerID;
            hb.isProjectile = true;
            hb.isNormalAttack = false;

            Destroy(gameObject, lifetime);
        }

        private void CreateTrail()
        {
            GameObject trailObj = new GameObject("Trail");
            trailObj.transform.SetParent(transform);
            trailObj.transform.localPosition = Vector3.zero;

            trailRenderer = trailObj.AddComponent<LineRenderer>();
            trailRenderer.positionCount = 10;
            trailRenderer.useWorldSpace = true;
            
            // 竜撃砲の極太ビーム感を出すための太さ設定
            trailRenderer.startWidth = 1.4f;
            trailRenderer.endWidth = 0.0f;
            
            // シンプルなスプライト用マテリアルを割り当てて色を乗せる
            trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
            
            // オレンジから赤へフェードアウトするグラデーション
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(new Color(1f, 0.4f, 0f), 0f), 
                    new GradientColorKey(new Color(0.8f, 0f, 0f), 1f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(0.8f, 0f), 
                    new GradientAlphaKey(0f, 1f) 
                }
            );
            trailRenderer.colorGradient = gradient;

            // 初期位置でリストを満たす
            Vector3 startPos = transform.position;
            for (int i = 0; i < trailRenderer.positionCount; i++)
            {
                trailRenderer.SetPosition(i, startPos);
            }
        }

        void Update()
        {
            // パルス効果：エネルギー弾が小刻みに拡大縮小し、バチバチ感を演出
            float pulse = 1f + Mathf.PingPong(Time.time * 25f, 0.15f);
            transform.localScale = new Vector3(moveDirection * pulse, pulse, 1f);

            // トレイルの軌跡を更新
            if (trailRenderer != null)
            {
                trailPositions.Insert(0, transform.position);
                if (trailPositions.Count > 10)
                {
                    trailPositions.RemoveAt(trailPositions.Count - 1);
                }

                for (int i = 0; i < trailPositions.Count; i++)
                {
                    trailRenderer.SetPosition(i, trailPositions[i]);
                }
                for (int i = trailPositions.Count; i < 10; i++)
                {
                    trailRenderer.SetPosition(i, trailPositions[trailPositions.Count - 1]);
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Hurtbox hurtbox = other.GetComponent<Hurtbox>();
            if (hurtbox != null && hurtbox.owner != null && hurtbox.owner.playerID != ownerPlayerID)
            {
                // 大ダメージを与える！
                hurtbox.TakeDamage(damage, GetComponent<Hitbox>());
                Debug.Log($"【命中】竜撃砲が対戦相手に直撃！ {damage} ダメージ！");

                // 爆発エフェクトをその場に展開
                CreateExplosionEffect();

                // 弾本体は消滅
                Destroy(gameObject);
            }
        }

        private void CreateExplosionEffect()
        {
            GameObject explosion = new GameObject("DragonBlastExplosion");
            explosion.transform.position = transform.position;

            SpriteRenderer sr = explosion.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDefaultSprite();
            sr.color = new Color(1f, 0.6f, 0f, 1f);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1f, 1f);

            explosion.AddComponent<ExplosionScaler>();
        }
    }

    // =================================================================================
    // 【ExplosionScaler（爆発演出制御）】
    // 竜撃砲が当たった時に、ブワッと広がってフェードアウトする演出を司ります。
    // =================================================================================
    public class ExplosionScaler : MonoBehaviour
    {
        private float elapsed = 0f;
        private float duration = 0.5f;
        private SpriteRenderer sr;

        private static Sprite CreateDefaultSprite()
        {
            Texture2D tex = new Texture2D(2, 2);
            Color[] colors = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }

        void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            transform.localScale = Vector3.one;

            // 白い熱い光のコアを追加
            GameObject core = new GameObject("Core");
            core.transform.SetParent(transform);
            core.transform.localPosition = Vector3.zero;
            SpriteRenderer coreSr = core.AddComponent<SpriteRenderer>();
            coreSr.sprite = CreateDefaultSprite();
            coreSr.color = Color.white;
            coreSr.drawMode = SpriteDrawMode.Sliced;
            coreSr.size = new Vector2(0.7f, 0.7f);
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // 急速に大きく拡大
            float scale = Mathf.Lerp(1.0f, 6.5f, t);
            transform.localScale = new Vector3(scale, scale, 1f);

            // 外側の色をオレンジからフェードアウト
            if (sr != null)
            {
                sr.color = new Color(1f, Mathf.Lerp(0.6f, 0f, t), 0f, 1f - t);
            }
        }
    }
}
