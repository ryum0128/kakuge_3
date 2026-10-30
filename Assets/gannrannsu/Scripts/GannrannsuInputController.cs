using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGameBase
{
    // =================================================================================
    // 【GannrannsuInputController（ガンランス専用入力コントローラー）】
    // 格闘ゲーム共通の標準入力（J=通常攻撃, K=特殊攻撃, Space=ジャンプ, H=ガード, L=ダッシュ）
    // に完全対応しつつ、旧キー（W=ジャンプ, K=通常攻撃, L=特殊攻撃）の入力も受け付ける
    // デュアル対応コントローラーです。Xboxコントローラー入力にも完全対応しています。
    // =================================================================================
    [RequireComponent(typeof(GannrannsuCharacter))]
    public class GannrannsuInputController : MonoBehaviour
    {
        [Header("キー設定 (インスペクターで自由に変更可能)")]
        public Key leftKey = Key.A;              // 左移動
        public Key rightKey = Key.D;             // 右移動
        public Key jumpKey = Key.Space;          // ジャンプ（Space / W 両対応）
        public Key normalAttackKey = Key.J;      // 通常攻撃（J / K 両対応）
        public Key specialAttackKey = Key.K;     // 特殊攻撃（K / L 両対応）
        public Key blockKey = Key.H;             // ブロック・パリィ
        public Key dashKey = Key.L;              // ダッシュ・回避
        public Key stunAttackKey = Key.C;        // スタン攻撃

        [Header("同時押し判定猶予（秒）")]
        [Tooltip("通常＋特殊攻撃の同時押しと判定する猶予時間。0.05秒が標準")]
        public float simultaneousPressWindow = 0.05f;

        private GannrannsuCharacter character;

        // --- 入力タイミングの記録用 ---
        private float lastNormalAttackPressTime = -1f;
        private float lastSpecialAttackPressTime = -1f;
        private bool isUltimateTriggered = false;

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
            if (!keysInitialized)
            {
                InitializeKeysByContext();
            }
        }

        public void SetupKeys(int targetPlayerID)
        {
            if (character == null) character = GetComponent<GannrannsuCharacter>();
            if (character != null) character.playerID = targetPlayerID;

            if (targetPlayerID == 1)
            {
                leftKey = Key.A;
                rightKey = Key.D;
                jumpKey = Key.Space;
                normalAttackKey = Key.J;
                specialAttackKey = Key.K;
                blockKey = Key.H;
                dashKey = Key.L;
                stunAttackKey = Key.C;
            }
            else
            {
                leftKey = Key.LeftArrow;
                rightKey = Key.RightArrow;
                jumpKey = Key.UpArrow;
                normalAttackKey = Key.Numpad1;
                specialAttackKey = Key.Numpad2;
                blockKey = Key.Numpad3;
                dashKey = Key.Numpad5;
                stunAttackKey = Key.Numpad0;
            }
            keysInitialized = true;
        }

        private void InitializeKeysByContext()
        {
            if (character == null) character = GetComponent<GannrannsuCharacter>();

            if (character != null && (character.playerID == 2 || name.Contains("P2") || name.Contains("RightSide") || name.Contains("Player2")))
            {
                SetupKeys(2);
            }
            else
            {
                SetupKeys(1);
            }
        }

        void Update()
        {
            if (!keysInitialized)
            {
                InitializeKeysByContext();
            }

            // キャラクターが存在しない、死亡中、スタン中、被弾ロック中は入力を受け付けない
            if (character == null || character.isDead || character.isStunned || character.IsHurtLocked) return;

            // キーボードもゲームパッドも接続されていない場合は何もしない
            if (Keyboard.current == null && Gamepad.all.Count == 0) return;

            HandleMovement();

            // 攻撃硬直中は次の攻撃入力を開始しない
            if (character.IsAttacking) return;

            HandleAttacks();
            HandleBlock();
        }

        // ==========================================
        // 入力判定ヘルパー（キーボード + ゲームパッド + 旧キー互換）
        // ==========================================
        private int PlayerID => character != null ? character.playerID : 1;

        private bool IsLeftPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[leftKey].isPressed) return true;
            }
            return GamepadInputHelper.GetMoveDirection(PlayerID) < 0f;
        }

        private bool IsRightPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[rightKey].isPressed) return true;
            }
            return GamepadInputHelper.GetMoveDirection(PlayerID) > 0f;
        }

        private bool WasJumpPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[jumpKey].wasPressedThisFrame) return true;
                // 1P: Space でも W でもジャンプ可能
                if (PlayerID == 1 && Keyboard.current[Key.W].wasPressedThisFrame) return true;
            }
            return GamepadInputHelper.WasPressedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.Jump);
        }

        private bool WasNormalAttackPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[normalAttackKey].wasPressedThisFrame) return true;

                if (PlayerID == 1)
                {
                    // 1P: J でも K でも通常突き可能
                    if (Keyboard.current[Key.J].wasPressedThisFrame || Keyboard.current[Key.K].wasPressedThisFrame) return true;
                }
                else
                {
                    // 2P: Numpad1 / U / I で通常攻撃可能
                    if (Keyboard.current[Key.U].wasPressedThisFrame || Keyboard.current[Key.I].wasPressedThisFrame) return true;
                }
            }
            return GamepadInputHelper.WasPressedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.NormalAttack);
        }

        private bool WasNormalAttackReleased()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[normalAttackKey].wasReleasedThisFrame) return true;
                if (PlayerID == 1)
                {
                    if (Keyboard.current[Key.J].wasReleasedThisFrame || Keyboard.current[Key.K].wasReleasedThisFrame) return true;
                }
                else
                {
                    if (Keyboard.current[Key.U].wasReleasedThisFrame || Keyboard.current[Key.I].wasReleasedThisFrame) return true;
                }
            }
            return GamepadInputHelper.WasReleasedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.NormalAttack);
        }

        private bool WasSpecialAttackPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[specialAttackKey].wasPressedThisFrame) return true;

                if (PlayerID == 1)
                {
                    // 1P: K または L でも特殊砲撃可能
                    if (Keyboard.current[Key.K].wasPressedThisFrame || Keyboard.current[Key.L].wasPressedThisFrame) return true;
                }
                else
                {
                    // 2P: Numpad2 / I / O で特殊攻撃可能
                    if (Keyboard.current[Key.I].wasPressedThisFrame || Keyboard.current[Key.O].wasPressedThisFrame) return true;
                }
            }
            return GamepadInputHelper.WasPressedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.SpecialAttack);
        }

        private bool WasSpecialAttackReleased()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[specialAttackKey].wasReleasedThisFrame) return true;
                if (PlayerID == 1)
                {
                    if (Keyboard.current[Key.K].wasReleasedThisFrame || Keyboard.current[Key.L].wasReleasedThisFrame) return true;
                }
                else
                {
                    if (Keyboard.current[Key.I].wasReleasedThisFrame || Keyboard.current[Key.O].wasReleasedThisFrame) return true;
                }
            }
            return GamepadInputHelper.WasReleasedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.SpecialAttack);
        }

        private bool WasBlockPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[blockKey].wasPressedThisFrame) return true;
                if (PlayerID == 2 && Keyboard.current[Key.O].wasPressedThisFrame) return true;
            }
            return GamepadInputHelper.WasPressedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.Block);
        }

        private bool WasBlockReleased()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[blockKey].wasReleasedThisFrame) return true;
                if (PlayerID == 2 && Keyboard.current[Key.O].wasReleasedThisFrame) return true;
            }
            return GamepadInputHelper.WasReleasedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.Block);
        }

        private bool WasDashPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[dashKey].wasPressedThisFrame) return true;
                if (PlayerID == 2 && Keyboard.current[Key.P].wasPressedThisFrame) return true;
            }
            return GamepadInputHelper.WasPressedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.Dash);
        }

        private bool WasStunPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[stunAttackKey].wasPressedThisFrame) return true;
                if (PlayerID == 2 && Keyboard.current[Key.Y].wasPressedThisFrame) return true;
            }
            return GamepadInputHelper.WasPressedThisFrame(PlayerID, GamepadInputHelper.GamepadAction.StunAttack);
        }

        // ==========================================
        // 移動とジャンプ、ダッシュの処理
        // ==========================================
        private void HandleMovement()
        {
            float direction = 0f;

            if (IsLeftPressed()) direction -= 1f;
            if (IsRightPressed()) direction += 1f;

            character.Move(direction);

            if (character.IsAttacking) return;

            // ダッシュ／回避
            if (WasDashPressed())
            {
                character.TriggerDashOrEvade(direction);
            }

            // ジャンプ
            if (WasJumpPressed())
            {
                character.Jump();
            }
        }

        // ==========================================
        // 攻撃と竜撃砲（必殺技）の処理
        // ==========================================
        private void HandleAttacks()
        {
            bool normalDown = WasNormalAttackPressed();
            bool specialDown = WasSpecialAttackPressed();

            if (normalDown) lastNormalAttackPressTime = Time.time;
            if (specialDown) lastSpecialAttackPressTime = Time.time;

            // --- 同時押し（必殺技：竜撃砲）の判定 ---
            bool isSimultaneous = false;
            if (lastNormalAttackPressTime > 0 && lastSpecialAttackPressTime > 0 &&
                Time.time - lastNormalAttackPressTime <= simultaneousPressWindow &&
                Time.time - lastSpecialAttackPressTime <= simultaneousPressWindow)
            {
                isSimultaneous = true;
            }

            if (isSimultaneous)
            {
                if (!isUltimateTriggered)
                {
                    if (character.chargeGauge >= character.maxChargeGauge && character.ultimateAttackCooldownTimer <= 0f)
                    {
                        character.AttackUltimate();
                        isUltimateTriggered = true;
                        lastNormalAttackPressTime = -1f;
                        lastSpecialAttackPressTime = -1f;
                    }
                    else
                    {
                        // ゲージ不足またはクールダウン中は通常突き
                        character.AttackNormal();
                        isUltimateTriggered = true;
                        lastNormalAttackPressTime = -1f;
                        lastSpecialAttackPressTime = -1f;
                    }
                }
            }
            else
            {
                // 単発通常攻撃（猶予時間を過ぎたら確定発動）
                if (lastNormalAttackPressTime > 0 && Time.time - lastNormalAttackPressTime > simultaneousPressWindow)
                {
                    character.AttackNormal();
                    lastNormalAttackPressTime = -1f;
                    isUltimateTriggered = false;
                }

                // 単発特殊攻撃（砲撃）
                if (lastSpecialAttackPressTime > 0 && Time.time - lastSpecialAttackPressTime > simultaneousPressWindow)
                {
                    character.AttackSpecial();
                    lastSpecialAttackPressTime = -1f;
                    isUltimateTriggered = false;
                }
            }

            if (WasNormalAttackReleased() || WasSpecialAttackReleased())
            {
                isUltimateTriggered = false;
            }

            // スタン攻撃
            if (WasStunPressed())
            {
                character.AttackStun();
            }
        }

        // ==========================================
        // ブロック・パリーの処理
        // ==========================================
        private void HandleBlock()
        {
            if (WasBlockPressed())
            {
                character.StartBlock();
            }
            if (WasBlockReleased())
            {
                character.StopBlock();
            }
        }
    }
}
