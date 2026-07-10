using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【GannrannsuCharacter（ガンランスキャラクター）】
    // 浮遊するガンランスのキャラクター！
    // CharacterBase（お手本）を継承して、ガンランス固有の能力を追加しています。
    // 特徴：
    //   ・常に少し浮いている（重力が軽い）
    //   ・特殊攻撃で「砲撃弾」を前方に飛ばす！
    //   ・同時押し必殺技で「竜撃砲」（大ダメージ＋反動で後ろに下がる）
    //
    // 【技術的な解説】
    //   CharacterBase の Move() や Jump() はそのまま使えます（継承の力！）。
    //   ただし CharacterBase のメソッドは virtual（上書き可能）ではないため、
    //   攻撃メソッドは「new」で隠して独自実装し、専用の入力コントローラー
    //   （GannrannsuInputController）から呼び出す設計にしています。
    //
    //   初期化は Awake()（CharacterBase.Start() より先に実行される）で行い、
    //   CharacterBase.Start() が正常に rb や animator を初期化できるようにしています。
    // =================================================================================
    public class GannrannsuCharacter : CharacterBase
    {
        [Header("===== ガンランス専用設定 =====")]

        [Tooltip("砲撃弾のプレハブ（飛んでいく弾）")]
        public GameObject shellPrefab;

        [Tooltip("砲撃弾が出る位置（ランスの先端）")]
        public Transform firePoint;

        [Tooltip("竜撃砲専用の攻撃判定オブジェクト（広い射程）")]
        public GameObject dragonBlastHitbox;

        [Tooltip("砲撃弾の飛ぶ速さ")]
        public float shellSpeed = 12f;

        [Tooltip("砲撃弾のダメージ")]
        public int shellDamage = 15;

        [Tooltip("竜撃砲のダメージ（必殺技！大ダメージ！）")]
        public int dragonBlastDamage = 40;

        [Tooltip("竜撃砲の反動で後ろに飛ばされる力")]
        public float dragonBlastRecoil = 8f;

        [Tooltip("浮遊する力（この値の分だけ重力に逆らいます）")]
        public float hoverForce = 15f;

        [Tooltip("浮遊する高さの上限")]
        public float maxHoverHeight = 3f;

        [Header("===== 竜撃砲チャージ設定 =====")]
        [Tooltip("竜撃砲のチャージに必要な時間（秒）（※後方互換性のため残しています。現在はチャージゲージ満タンで発動します）")]
        public float chargeRequiredTime = 1.5f;

        // 現在チャージ中かどうか（※現在は使用されません）
        public bool isCharging { get; private set; }

        [Header("===== 竜撃砲ゲージ設定 =====")]
        [Tooltip("チャージゲージの最大値")]
        public float maxChargeGauge = 100f;

        [Tooltip("現在のチャージゲージ値")]
        public float chargeGauge = 0f;

        [Tooltip("1秒あたりの自動チャージ量")]
        public float passiveChargeRate = 5f;

        [Tooltip("通常攻撃（突き）が命中したときのチャージ増加量")]
        public float normalHitChargeGain = 10f;

        [Tooltip("特殊攻撃（砲撃）が命中したときのチャージ増加量")]
        public float specialHitChargeGain = 20f;

        [Header("===== 竜撃砲レーザーエフェクト設定 =====")]
        [Tooltip("レーザーが表示される時間（秒）")]
        public float laserDuration = 0.5f;

        [Tooltip("レーザーの太さ")]
        public float laserWidth = 0.25f;

        [Tooltip("レーザーの射程（ワールド単位）")]
        public float laserRange = 20f;

        // --- GUI用テクスチャとスタイル ---
        private Texture2D bgTex;
        private Texture2D fillTex;
        private GUIStyle textStyle;

        // --- ガンランス専用の部品参照 ---
        // ※ CharacterBase にも rb/animator がありますが private（非公開）なので、
        //    ガンランス固有の処理（浮遊・竜撃砲の反動など）用に自分でも持っておきます。
        private Rigidbody2D myRb;
        private Animator myAnimator;
        private bool isHovering = true; // 浮遊しているかどうか

        // 移動とジャンプの上書き（チャージ中は動けないようにする）
        public new void Move(float direction)
        {
            if (isDead) return;

            if (isCharging)
            {
                // チャージ中は移動させないように速度を0にする
                myRb.linearVelocity = new Vector2(0f, myRb.linearVelocity.y);
                if (myAnimator != null) myAnimator.SetFloat("Speed", 0f);
                return;
            }
            base.Move(direction);
        }

        public new void Jump()
        {
            if (isDead || isCharging) return;
            base.Jump();
        }

        // =========================================================
        // チャージ制御メソッド
        // =========================================================
        public void StartCharge()
        {
            if (isDead) return;
            isCharging = true;
            if (myAnimator != null) myAnimator.SetTrigger("StartCharge"); // アニメーション用トリガー
            Debug.Log("ガンランス：竜撃砲チャージ開始！！エネルギー充填中...");
        }

        public void CancelCharge()
        {
            if (!isCharging || isDead) return;
            isCharging = false;
            if (myAnimator != null) myAnimator.SetTrigger("CancelCharge");
            Debug.Log("ガンランス：チャージキャンセル");
        }

        public void CompleteCharge()
        {
            if (!isCharging || isDead) return;
            isCharging = false;
            Debug.Log("ガンランス：チャージ完了！！竜撃砲を放ちます！");
            AttackUltimate();
        }

        // Awake() は Start() より先に呼ばれるので、ここでガンランス固有のセットアップをします。
        // こうすることで CharacterBase.Start() が正常に動きます！
        void Awake()
        {
            myRb = GetComponent<Rigidbody2D>();
            myAnimator = GetComponentInChildren<Animator>();

            // ガンランスは浮いているので、重力を軽くします！
            myRb.gravityScale = 1.0f;
        }

        // 基底クラスの Start() をオーバーライド（継承の初期化とコールバック登録）
        void Start()
        {
            base.Start();

            // 子オブジェクトからHitbox（通常攻撃）を取得して、命中時のコールバックを登録
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

        // Update() はガンランス固有の浮遊処理を行います。
        // ※ これにより CharacterBase.Update() は隠れますが、
        //    接地判定などの処理もここに含めているので大丈夫です。
        void Update()
        {
            if (isDead || GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            // --- 浮遊処理 ---
            // 地面の近くまで落ちてきたら、上向きの力を加えて浮かせます
            if (isHovering && transform.position.y < maxHoverHeight)
            {
                myRb.AddForce(Vector2.up * hoverForce * Time.deltaTime, ForceMode2D.Force);
            }

            // 接地判定（CharacterBase.Update() の代わり）
            isGrounded = Mathf.Abs(myRb.linearVelocity.y) < 0.1f;

            if (myAnimator != null)
            {
                myAnimator.SetBool("IsGrounded", isGrounded);
                myAnimator.SetBool("IsHovering", isHovering);
            }

            // --- チャージゲージの自動蓄積 ---
            AddCharge(passiveChargeRate * Time.deltaTime);
        }

        // チャージを増減させる関数
        public void AddCharge(float amount)
        {
            if (isDead) return;
            chargeGauge = Mathf.Clamp(chargeGauge + amount, 0f, maxChargeGauge);
        }

        // --- GUI表示用の設定 ---
        private void InitTextures()
        {
            if (bgTex == null)
            {
                bgTex = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.6f)); // 半透明の黒背景
            }
            if (fillTex == null)
            {
                fillTex = MakeTex(2, 2, Color.orange); // オレンジ色のゲージ
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

        // 画面上にチャージゲージを綺麗に表示します
        void OnGUI()
        {
            if (isDead) return;
            InitTextures();

            // プレイヤーIDに応じて表示位置を左右に振り分けます
            float width = 200f;
            float height = 20f;
            float x = (playerID == 1) ? 20f : Screen.width - width - 20f;
            float y = 90f; // 体力ゲージの下付近

            // 背景描画
            GUI.DrawTexture(new Rect(x, y, width, height), bgTex);

            // ゲージの割合幅を計算
            float fillRatio = chargeGauge / maxChargeGauge;
            float fillWidth = (width - 4f) * fillRatio;
            
            Color barColor = Color.orange;
            string text = $"竜撃砲 CHARGE: {Mathf.Floor(chargeGauge)}%";
            
            // 満タン時のピカピカ点滅エフェクト
            if (chargeGauge >= maxChargeGauge)
            {
                float flash = Mathf.PingPong(Time.time * 4f, 1f);
                barColor = Color.Lerp(new Color(1f, 0.3f, 0f), new Color(1f, 0.9f, 0f), flash);
                text = "竜撃砲 READY! (Z+X)";
            }
            
            // ゲージ描画
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(x + 2f, y + 2f, fillWidth, height - 4f), fillTex);
            GUI.color = Color.white; // リセット

            // テキスト表示
            GUI.Label(new Rect(x, y, width, height), text, textStyle);
        }

        // =========================================================
        // 通常攻撃: ランスの突き（お手本と同じ近距離攻撃）
        // ※ GannrannsuInputController から呼ばれます
        // =========================================================
        public new void AttackNormal()
        {
            if (isDead) return;

            if (myAnimator != null) myAnimator.SetTrigger("AttackNormal");
            Debug.Log("ガンランス：突き攻撃！");

            // 子オブジェクトからHitbox（攻撃判定）を探して、一時的にオンにします
            Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
            if (hitbox != null)
            {
                StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, 0.25f));
            }
        }

        // =========================================================
        // 特殊攻撃: 砲撃弾を飛ばす！（遠距離攻撃）
        // =========================================================
        public new void AttackSpecial()
        {
            if (isDead) return;

            if (myAnimator != null) myAnimator.SetTrigger("AttackSpecial");
            Debug.Log("ガンランス：砲撃発射！！");

            // 砲撃弾のプレハブが設定されていれば、弾を生成して飛ばします
            if (shellPrefab != null && firePoint != null)
            {
                // 弾を作成します（位置はfirePoint、回転はなし）
                GameObject shell = Instantiate(shellPrefab, firePoint.position, Quaternion.identity);

                // 弾のスクリプトに設定を渡します
                GannrannsuShell shellScript = shell.GetComponent<GannrannsuShell>();
                if (shellScript != null)
                {
                    // キャラクターが向いている方向を取得します
                    float direction = Mathf.Sign(transform.localScale.x);
                    shellScript.Initialize(direction, shellSpeed, shellDamage, playerID);
                }
            }
            else
            {
                Debug.LogWarning("砲撃弾のプレハブか発射位置が設定されていません！");
            }
        }

        // =========================================================
        // 必殺技: 竜撃砲！（同時押しで発動する大技）
        // =========================================================
        public new void AttackUltimate()
        {
            if (isDead) return;

            // 必殺技発動時にゲージを消費してゼロに戻す
            chargeGauge = 0f;

            if (myAnimator != null) myAnimator.SetTrigger("AttackUltimate");
            Debug.Log("ガンランス：竜撃砲発動！！！");

            // --- 竜撃砲専用の広い攻撃判定を出す ---
            if (dragonBlastHitbox != null)
            {
                // 専用のHitboxコンポーネントを取得して設定を適用
                Hitbox hitbox = dragonBlastHitbox.GetComponent<Hitbox>();
                if (hitbox != null)
                {
                    hitbox.damage = dragonBlastDamage;
                    hitbox.ownerPlayerID = playerID;
                }
                
                // 0.4秒間だけ判定を出します
                StartCoroutine(ActivateHitboxTemporarily(dragonBlastHitbox, 0.4f));
            }
            else
            {
                // 専用Hitboxが未設定の場合は、突き判定を流用（フォールバック）
                Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
                if (hitbox != null)
                {
                    int originalDamage = hitbox.damage;
                    hitbox.damage = dragonBlastDamage;
                    StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, 0.4f));
                    StartCoroutine(ResetDamageAfterDelay(hitbox, originalDamage, 0.45f));
                }
            }

            // --- 反動で後ろに吹っ飛びます ---
            float recoilDirection = -Mathf.Sign(transform.localScale.x);
            myRb.AddForce(new Vector2(recoilDirection * dragonBlastRecoil, 2f), ForceMode2D.Impulse);

            // --- 赤いレーザーエフェクトを発射！ ---
            FireLaserEffect();
        }

        // =========================================================
        // 竜撃砲の赤いレーザーエフェクト（LineRenderer を使用）
        // =========================================================
        private void FireLaserEffect()
        {
            StartCoroutine(LaserCoroutine());
        }

        private System.Collections.IEnumerator LaserCoroutine()
        {
            // レーザーを担うGameObjectを動的に作成
            GameObject laserObj = new GameObject("DragonBlastLaser");
            LineRenderer lr = laserObj.AddComponent<LineRenderer>();

            // 発射方向（キャラクターの向き）
            float dir = Mathf.Sign(transform.localScale.x);

            // =============================================
            // 当たり判定（DragonBlastHitbox）からサイズを計算
            // =============================================
            float hitboxSizeX = laserRange;  // フォールバック用
            float hitboxSizeY = laserWidth;  // フォールバック用
            float hitboxStartX = 0f;         // キャラクターからの開始オフセット（ローカル）

            if (dragonBlastHitbox != null)
            {
                BoxCollider2D col = dragonBlastHitbox.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    // Boundsはワールド空間（スケール込み）
                    Bounds b = col.bounds;
                    hitboxSizeY = b.size.y;  // レーザーの太さ = ヒットボックスの高さ
                    hitboxSizeX = b.size.x;  // レーザーの長さ = ヒットボックスの幅

                    // 開始・終了位置をヒットボックスの左端・右端に合わせる
                    // dir > 0（右向き）なら左端がstart、右端がend
                    hitboxStartX = (dir > 0) ? b.min.x : b.max.x;
                }
            }

            // LineRenderer の初期設定
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            // マテリアル（組み込みのSprites/Defaultで色が乗る）
            lr.material = new Material(Shader.Find("Sprites/Default"));

            // アニメーションループ
            float elapsed = 0f;
            while (elapsed < laserDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / laserDuration; // 0→1

                // ちらつき（フリッカー）
                float flicker = 1f - Mathf.PerlinNoise(elapsed * 30f, 0f) * 0.3f;

                // 色：赤→オレンジに推移して、時間とともに透明にフェード
                float alpha = (1f - t) * flicker;
                lr.startColor = new Color(1f, Mathf.Lerp(0f,  0.3f, t), 0f, alpha);
                lr.endColor   = new Color(1f, Mathf.Lerp(0.2f, 0.6f, t), 0f, alpha * 0.4f);

                // 太さも時間とともに細くなる（ヒットボックスの高さに合わせてスタート）
                float currentWidth = hitboxSizeY * (1f - t * 0.5f) * flicker;
                lr.startWidth = currentWidth;
                lr.endWidth   = currentWidth; // 均一にしてヒットボックスの高さと合わせる

                // =========================================
                // 位置：ヒットボックスのboundsを毎フレーム再取得（キャラが動いても追従）
                // =========================================
                Vector3 startPos, endPos;
                if (dragonBlastHitbox != null)
                {
                    BoxCollider2D col = dragonBlastHitbox.GetComponent<BoxCollider2D>();
                    if (col != null)
                    {
                        Bounds b = col.bounds;
                        // dir > 0 なら右向き：左端→右端、左向き：右端→左端
                        startPos = new Vector3((dir > 0) ? b.min.x : b.max.x, b.center.y, 0f);
                        endPos   = new Vector3((dir > 0) ? b.max.x : b.min.x, b.center.y, 0f);
                    }
                    else
                    {
                        // fallback
                        Vector3 fp = firePoint != null ? firePoint.position : transform.position;
                        startPos = fp;
                        endPos   = fp + new Vector3(dir * hitboxSizeX, 0f, 0f);
                    }
                }
                else
                {
                    Vector3 fp = firePoint != null ? firePoint.position : transform.position;
                    startPos = fp;
                    endPos   = fp + new Vector3(dir * hitboxSizeX, 0f, 0f);
                }

                lr.SetPosition(0, startPos);
                lr.SetPosition(1, endPos);

                yield return null;
            }

            // レーザー消去
            Destroy(laserObj);
        }

        // 一定時間後にダメージを元に戻すコルーチン
        private System.Collections.IEnumerator ResetDamageAfterDelay(Hitbox hitbox, int originalDamage, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (hitbox != null)
            {
                hitbox.damage = originalDamage;
            }
        }

        // ActivateHitboxTemporarily はお手本（CharacterBase）と同じ仕組みです
        private System.Collections.IEnumerator ActivateHitboxTemporarily(GameObject hitboxObj, float duration)
        {
            hitboxObj.SetActive(true);  // 攻撃判定をオン
            yield return new WaitForSeconds(duration);
            hitboxObj.SetActive(false); // 攻撃判定をオフ
        }
    }
}
