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
        [Tooltip("竜撃砲のチャージに必要な時間（秒）")]
        public float chargeRequiredTime = 1.5f;

        // 現在チャージ中かどうか
        public bool isCharging { get; private set; }

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

        // ※ Start() は定義しません！ → CharacterBase.Start() が自動で呼ばれて、
        //    基底クラスの rb, animator, currentHP が正しく初期化されます。

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
