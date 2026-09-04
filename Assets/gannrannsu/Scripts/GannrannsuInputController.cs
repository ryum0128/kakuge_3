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

        private bool keysInitialized = false;

        void Start()
        {
            // GannrannsuCharacter 繧貞叙蠕励＠縺ｾ縺・            character = GetComponent<GannrannsuCharacter>();
        }

        private void InitializeKeys()
        {
            if (character != null && (character.playerID == 2 || name.Contains("P2") || name.Contains("RightSide") || name.Contains("Player2")))
            {
                leftKey = Key.LeftArrow;
                rightKey = Key.RightArrow;
                jumpKey = Key.UpArrow;
                normalAttackKey = Key.I;
                specialAttackKey = Key.O;
            }
            keysInitialized = true;
        }

        void Update()
        {
            if (!keysInitialized)
            {
                InitializeKeys();
            }

            if (character == null || character.isDead) return;
            // キーボードもゲームパッドも接続されていない場合は何もしません
            if (Keyboard.current == null && Gamepad.all.Count == 0) return;

            HandleMovement();
            HandleAttacks();
        }

        // ==========================================
        // Xboxコントローラー対応
        // playerID 1 → 1台目のコントローラー、playerID 2 → 2台目のコントローラーを使います
        // ==========================================
        private int PlayerID => character != null ? character.playerID : 1;

        private bool IsLeftPressed() => (Keyboard.current != null && Keyboard.current[leftKey].isPressed) || GamepadInputHelper.GetMoveDirection(PlayerID) < 0f;
        private bool IsRightPressed() => (Keyboard.current != null && Keyboard.current[rightKey].isPressed) || GamepadInputHelper.GetMoveDirection(PlayerID) > 0f;
        private bool WasJumpPressedThisFrame() => (Keyboard.current != null && Keyboard.current[jumpKey].wasPressedThisFrame) || GamepadInputHelper.WasPressedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.Jump);
        private bool WasNormalAttackPressedThisFrame() => (Keyboard.current != null && Keyboard.current[normalAttackKey].wasPressedThisFrame) || GamepadInputHelper.WasPressedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.NormalAttack);
        private bool WasNormalAttackReleasedThisFrame() => (Keyboard.current != null && Keyboard.current[normalAttackKey].wasReleasedThisFrame) || GamepadInputHelper.WasReleasedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.NormalAttack);
        private bool WasSpecialAttackPressedThisFrame() => (Keyboard.current != null && Keyboard.current[specialAttackKey].wasPressedThisFrame) || GamepadInputHelper.WasPressedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.SpecialAttack);
        private bool WasSpecialAttackReleasedThisFrame() => (Keyboard.current != null && Keyboard.current[specialAttackKey].wasReleasedThisFrame) || GamepadInputHelper.WasReleasedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.SpecialAttack);

        // ==========================================
        // 移動とジャンプの処理
        // （お手本とまったく同じ！CharacterBase の Move/Jump がそのまま使えます）
        // ==========================================
        private void HandleMovement()
        {
            float direction = 0f;

            if (IsLeftPressed()) direction -= 1f;
            if (IsRightPressed()) direction += 1f;

            // Move() は GannrannsuCharacter に new で定義されたものが呼ばれます（チャージ中は移動停止）
            character.Move(direction);

            if (WasJumpPressedThisFrame())
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
            // キーが「押された瞬間」かどうかをチェックします
            bool zDown = WasNormalAttackPressedThisFrame();
            bool xDown = WasSpecialAttackPressedThisFrame();

            // 押されたら、その時のゲーム内時間を記録しておきます
            if (zDown) lastZPressTime = Time.time;
            if (xDown) lastXPressTime = Time.time;

            // --- 同時押しの判定 ---
            bool isSimultaneous = false;
            // ZキーとXキーの両方が、最近（0.05秒以内）押されたかをチェックします
            if (lastZPressTime > 0 && lastXPressTime > 0 &&
                Time.time - lastZPressTime <= simultaneousPressWindow &&
                Time.time - lastXPressTime <= simultaneousPressWindow)
            {
                isSimultaneous = true; // 同時押し成功！
            }

            if (isSimultaneous)
            {
                // 同時押し（必殺技）
                if (!isUltimateTriggered)
                {
                    if (character.chargeGauge >= character.maxChargeGauge && character.ultimateAttackCooldownTimer <= 0f)
                    {
                        character.AttackUltimate(); // ゲージ満タンかつクールタイム中ではないなら即座に竜撃砲発射！
                        isUltimateTriggered = true; // ボタン押しっぱなしによる連続発射を防ぐ
                        
                        // 同時押しが成功したため、単発攻撃（突き・砲撃）の予約をクリアします
                        lastZPressTime = -1f;
                        lastXPressTime = -1f;
                    }
                    else
                    {
                        Debug.Log("竜撃砲：チャージ不足またはクールタイム中！代わりに通常攻撃を発動します。");
                        character.AttackNormal(); // 発動できない場合は通常攻撃（ランスの突き）を発動
                        isUltimateTriggered = true; // ボタン押しっぱなしによる連続発射を防ぐ
                        
                        // 通常攻撃を発動したため、単発攻撃（突き・砲撃）の予約をクリアします
                        lastZPressTime = -1f;
                        lastXPressTime = -1f;
                    }
                }
            }
            else
            {
                // --- 単発入力の処理（同時押し猶予時間が経過した場合） ---
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

            // ボタンが離されたら同時押しロックを解除
            if (WasNormalAttackReleasedThisFrame() || WasSpecialAttackReleasedThisFrame())
            {
                isUltimateTriggered = false;
            }
        }
    }
}
