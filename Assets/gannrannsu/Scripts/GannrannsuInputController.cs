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

        void Awake()
        {
            character = GetComponent<GannrannsuCharacter>();
        }

        void Start()
        {
            if (character == null)
            {
                character = GetComponent<GannrannsuCharacter>();
            }
            InitializeKeys();
        }

        private void InitializeKeys()
        {
            if (character == null)
            {
                character = GetComponent<GannrannsuCharacter>();
            }

            bool isP2 = character != null && (character.playerID == 2 || name.Contains("P2") || name.Contains("RightSide") || name.Contains("Player2"));
            if (isP2)
            {
                if (leftKey == Key.A) leftKey = Key.LeftArrow;
                if (rightKey == Key.D) rightKey = Key.RightArrow;
                if (jumpKey == Key.W) jumpKey = Key.UpArrow;
                if (normalAttackKey == Key.K) normalAttackKey = Key.I;
                if (specialAttackKey == Key.L) specialAttackKey = Key.O;
            }
            keysInitialized = true;
        }

        void Update()
        {
            if (!keysInitialized)
            {
                InitializeKeys();
            }

            if (character == null)
            {
                character = GetComponent<GannrannsuCharacter>();
            }

            if (character == null || character.isDead) return;
            if (Keyboard.current == null) return;

            HandleMovement();
            HandleAttacks();
        }

        // ==========================================
        // 移動とジャンプの処理
        // ==========================================
        private void HandleMovement()
        {
            if (character == null || Keyboard.current == null) return;

            float direction = 0f;

            // 1. 設定されたキーの判定
            if (Keyboard.current[leftKey].isPressed) direction -= 1f;
            if (Keyboard.current[rightKey].isPressed) direction += 1f;

            // 2. プレイヤー環境に応じた入力フォールバック（直感的に確実に動かせるようにサポート）
            bool isP2 = (character.playerID == 2 || name.Contains("P2") || name.Contains("RightSide") || name.Contains("Player2"));
            if (isP2)
            {
                // 2P側: 矢印キー（Left/Right）および J/L キーの入力でも左右移動可能
                if (Keyboard.current[Key.LeftArrow].isPressed) direction -= 1f;
                if (Keyboard.current[Key.RightArrow].isPressed) direction += 1f;
                if (Keyboard.current[Key.J].isPressed) direction -= 1f;
                if (Keyboard.current[Key.L].isPressed) direction += 1f;
            }
            else
            {
                // 1P側: A/D キー
                if (Keyboard.current[Key.A].isPressed) direction -= 1f;
                if (Keyboard.current[Key.D].isPressed) direction += 1f;

                // 1人プレイやテストシーン（他のキャラクターがいない場合）は矢印キーでも移動可能
                var allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
                if (allChars.Length <= 1)
                {
                    if (Keyboard.current[Key.LeftArrow].isPressed) direction -= 1f;
                    if (Keyboard.current[Key.RightArrow].isPressed) direction += 1f;
                }
            }

            direction = Mathf.Clamp(direction, -1f, 1f);

            // Move() を呼び出して左右に移動・方向転換
            character.Move(direction);

            // ジャンプ処理
            bool jumpPressed = Keyboard.current[jumpKey].wasPressedThisFrame;
            if (isP2)
            {
                jumpPressed |= Keyboard.current[Key.UpArrow].wasPressedThisFrame || Keyboard.current[Key.I].wasPressedThisFrame;
            }
            else
            {
                jumpPressed |= Keyboard.current[Key.W].wasPressedThisFrame || Keyboard.current[Key.Space].wasPressedThisFrame;
                var allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
                if (allChars.Length <= 1)
                {
                    jumpPressed |= Keyboard.current[Key.UpArrow].wasPressedThisFrame;
                }
            }

            if (jumpPressed)
            {
                character.Jump();
            }
        }

        // ==========================================
        // 攻撃と「同時長押しチャージ」の処理
        // ==========================================
        private void HandleAttacks()
        {
            // キーが「押された瞬間」かどうかをチェックします
            bool zDown = Keyboard.current[normalAttackKey].wasPressedThisFrame;
            bool xDown = Keyboard.current[specialAttackKey].wasPressedThisFrame;

            bool isP2 = character != null && (character.playerID == 2 || name.Contains("P2") || name.Contains("RightSide") || name.Contains("Player2"));
            if (isP2)
            {
                zDown |= Keyboard.current[Key.I].wasPressedThisFrame || Keyboard.current[Key.U].wasPressedThisFrame || Keyboard.current[Key.Numpad1].wasPressedThisFrame;
                xDown |= Keyboard.current[Key.O].wasPressedThisFrame || Keyboard.current[Key.Numpad2].wasPressedThisFrame;
            }
            else
            {
                zDown |= Keyboard.current[Key.J].wasPressedThisFrame;
                xDown |= Keyboard.current[Key.K].wasPressedThisFrame;
            }

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
            bool zUp = Keyboard.current[normalAttackKey].wasReleasedThisFrame;
            bool xUp = Keyboard.current[specialAttackKey].wasReleasedThisFrame;
            if (isP2)
            {
                zUp |= Keyboard.current[Key.I].wasReleasedThisFrame || Keyboard.current[Key.U].wasReleasedThisFrame || Keyboard.current[Key.Numpad1].wasReleasedThisFrame;
                xUp |= Keyboard.current[Key.O].wasReleasedThisFrame || Keyboard.current[Key.Numpad2].wasReleasedThisFrame;
            }
            else
            {
                zUp |= Keyboard.current[Key.J].wasReleasedThisFrame;
                xUp |= Keyboard.current[Key.K].wasReleasedThisFrame;
            }

            if (zUp || xUp)
            {
                isUltimateTriggered = false;
            }
        }
    }
}
