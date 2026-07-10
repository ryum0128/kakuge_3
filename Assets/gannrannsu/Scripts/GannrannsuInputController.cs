using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGameBase
{
    // =================================================================================
    // 【GannrannsuInputController（ガンランス専用の入力コントローラー）】
    // お手本の PlayerInputController をベースに、ガンランスキャラクター用に作りました。
    //
    // 【なぜ専用コントローラーが必要なの？】
    //   お手本の PlayerInputController は CharacterBase 型でメソッドを呼びます。
    //   しかしガンランスの攻撃メソッド（砲撃や竜撃砲）は「new」キーワードで
    //   独自に定義しているため、CharacterBase 型から呼ぶとお手本の攻撃が出てしまいます。
    //   GannrannsuCharacter 型で直接呼ぶことで、ガンランス固有の攻撃が正しく発動します！
    //
    //   移動とジャンプは CharacterBase から継承しているのでそのまま使えます。
    // =================================================================================
    [RequireComponent(typeof(GannrannsuCharacter))]
    public class GannrannsuInputController : MonoBehaviour
    {
        [Header("キー設定 (インスペクターで自由に変更可能)")]
        public Key leftKey = Key.A;              // 左移動
        public Key rightKey = Key.D;             // 右移動
        public Key jumpKey = Key.W;              // ジャンプ
        public Key normalAttackKey = Key.K;      // 通常攻撃（ランスの突き）
        public Key specialAttackKey = Key.L;     // 特殊攻撃（砲撃弾）

        [Header("設定")]
        [Tooltip("ZとXの同時押しと判定する猶予時間（秒）。0.05秒くらいがちょうどいいです")]
        public float simultaneousPressWindow = 0.05f;

        // ★ポイント：CharacterBase ではなく GannrannsuCharacter 型で持つ！
        //   こうすることで、ガンランス固有のメソッドが正しく呼ばれます。
        private GannrannsuCharacter character;

        // --- 入力タイミングの記録用 ---
        private float lastZPressTime = -1f;
        private float lastXPressTime = -1f;
        private bool isUltimateTriggered = false;
        private float chargeTimer = 0f; // ★チャージ時間の計測用

        void Start()
        {
            // GannrannsuCharacter を取得します
            character = GetComponent<GannrannsuCharacter>();
        }

        void Update()
        {
            if (character == null || character.isDead) return;
            if (Keyboard.current == null) return;

            HandleMovement();
            HandleAttacks();
        }

        // ==========================================
        // 移動とジャンプの処理
        // （お手本とまったく同じ！CharacterBase の Move/Jump がそのまま使えます）
        // ==========================================
        private void HandleMovement()
        {
            float direction = 0f;

            if (Keyboard.current[leftKey].isPressed) direction -= 1f;
            if (Keyboard.current[rightKey].isPressed) direction += 1f;

            // Move() は GannrannsuCharacter に new で定義されたものが呼ばれます（チャージ中は移動停止）
            character.Move(direction);

            if (Keyboard.current[jumpKey].wasPressedThisFrame)
            {
                // Jump() も GannrannsuCharacter に new で定義されたものが呼ばれます
                character.Jump();
            }
        }

        // ==========================================
        // 攻撃と「同時長押しチャージ」の処理
        // ==========================================
        private void HandleAttacks()
        {
            // 現在キーが押されているか
            bool zPressed = Keyboard.current[normalAttackKey].isPressed;
            bool xPressed = Keyboard.current[specialAttackKey].isPressed;

            // キーが「押された瞬間」
            bool zDown = Keyboard.current[normalAttackKey].wasPressedThisFrame;
            bool xDown = Keyboard.current[specialAttackKey].wasPressedThisFrame;

            if (zDown) lastZPressTime = Time.time;
            if (xDown) lastXPressTime = Time.time;

            // --- 1. ZとXの両方が押されている場合（同時押し必殺技判定） ---
            if (zPressed && xPressed)
            {
                if (!isUltimateTriggered)
                {
                    // ZとXの両方が、同時押し猶予時間内（0.05秒以内）に押されたら竜撃砲発動判定！
                    if (Time.time - lastZPressTime <= simultaneousPressWindow &&
                        Time.time - lastXPressTime <= simultaneousPressWindow)
                    {
                        if (character.chargeGauge >= character.maxChargeGauge)
                        {
                            character.AttackUltimate(); // ゲージ満タンなら即座に竜撃砲発射！
                            isUltimateTriggered = true; // ボタン押しっぱなしによる連続発射を防ぐ
                            
                            // 同時押しが成功したため、単発攻撃（突き・砲撃）の予約をクリアします
                            lastZPressTime = -1f;
                            lastXPressTime = -1f;
                        }
                        else
                        {
                            Debug.Log("竜撃砲：チャージゲージがたまっていません！");
                        }
                    }
                }
            }
            else
            {
                // ボタンが離されたら同時押しロックを解除
                if (Keyboard.current[normalAttackKey].wasReleasedThisFrame || Keyboard.current[specialAttackKey].wasReleasedThisFrame)
                {
                    isUltimateTriggered = false;
                }

                // --- 2. 単発入力の処理（同時押し猶予時間が経過した場合） ---
                // 通常攻撃 (Z) → ランスの突き
                if (lastZPressTime > 0 && Time.time - lastZPressTime > simultaneousPressWindow)
                {
                    character.AttackNormal();
                    lastZPressTime = -1f;
                }

                // 特殊攻撃 (X) → 砲撃弾発射
                if (lastXPressTime > 0 && Time.time - lastXPressTime > simultaneousPressWindow)
                {
                    character.AttackSpecial();
                    lastXPressTime = -1f;
                }
            }
        }
    }
}
