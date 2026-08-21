using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGameBase
{
    // =================================================================================
    // 【PlayerInputController（プレイヤーの入力・操作）】
    // このスクリプトをアタッチするだけで、キーボード入力でキャラクターを動かせるようになります。
    // [RequireComponent] と書くと、必ず「CharacterBase」も一緒に付きます。
    // =================================================================================
    [RequireComponent(typeof(CharacterBase))]
    public class PlayerInputController : MonoBehaviour
    {
        [Header("キー設定 (インスペクターで自由に変更可能)")]
        
        // Key.○○ と設定しておくことで、どのキーを押したら反応するかを変更できます
        public Key leftKey = Key.A;             // 左移動
        public Key rightKey = Key.D;            // 右移動
        public Key jumpKey = Key.Space;         // ジャンプ
        public Key normalAttackKey = Key.J;     // 通常攻撃
        public Key specialAttackKey = Key.K;    // 特殊攻撃
        public Key blockKey = Key.H;            // ブロック・パリー
        public Key dashKey = Key.L;             // ダッシュ・回避
        public Key stunAttackKey = Key.C;       // スタンスキル（行動不能）

        [Header("設定")]
        [Tooltip("通常攻撃キーと特殊攻撃キーの同時押しと判定する猶予時間（秒）。0.05秒くらいがちょうどいいです")]
        public float simultaneousPressWindow = 0.05f;

        private CharacterBase character; // キャラクター本体を操作するためのリモコンのようなもの
        
        // --- 入力タイミングの記録用（同時押し判定のために使います） ---
        private float lastNormalAttackPressTime = -1f;
        private float lastSpecialAttackPressTime = -1f;
        private bool isUltimateTriggered = false; // 必殺技が出たかどうか

        void Start()
        {
            // キャラクター本体（CharacterBase）を見つけて取得します
            character = GetComponent<CharacterBase>();
        }

        void Update()
        {
            // キャラクターがいない、または倒れている場合やスタン中は入力を受け付けません
            if (character == null || character.isDead || character.isStunned) return;
            
            // キーボードが接続されていない場合も何もしません
            if (Keyboard.current == null) return;

            // 移動の処理と、攻撃の処理をそれぞれ呼び出します
            HandleMovement();
            HandleAttacks();
            HandleBlock();
        }

        // ==========================================
        // 移動とジャンプの処理
        // ==========================================
        private void HandleMovement()
        {
            float direction = 0f;
            
            // 左キーが押されていれば -1 に、右キーが押されていれば +1 にします
            if (Keyboard.current[leftKey].isPressed) direction -= 1f;
            if (Keyboard.current[rightKey].isPressed) direction += 1f;

            // キャラクター本体に「この方向に移動して！」と命令します
            character.Move(direction);

            // ダッシュ・回避キー判定
            if (Keyboard.current[dashKey].wasPressedThisFrame)
            {
                character.TriggerDashOrEvade(direction);
            }

            // ジャンプキーが「押された瞬間（wasPressedThisFrame）」ならジャンプします
            if (Keyboard.current[jumpKey].wasPressedThisFrame)
            {
                character.Jump();
            }
        }

        // ==========================================
        // 攻撃と「同時押し」の処理
        // ==========================================
        private void HandleAttacks()
        {
            // キーが「押された瞬間」かどうかをチェックします
            bool normalAttackDown = Keyboard.current[normalAttackKey].wasPressedThisFrame;
            bool specialAttackDown = Keyboard.current[specialAttackKey].wasPressedThisFrame;

            // 押されたら、その時のゲーム内時間を記録しておきます
            if (normalAttackDown) lastNormalAttackPressTime = Time.time;
            if (specialAttackDown) lastSpecialAttackPressTime = Time.time;

            // --- 同時押しの判定 ---
            bool isSimultaneous = false;
            // 通常攻撃キーと特殊攻撃キーの両方が、最近（0.05秒以内）押されたかをチェックします
            if (Time.time - lastNormalAttackPressTime <= simultaneousPressWindow && 
                Time.time - lastSpecialAttackPressTime <= simultaneousPressWindow)
            {
                isSimultaneous = true; // 同時押し成功！
            }

            if (isSimultaneous)
            {
                // 同時押し（必殺技）
                if (!isUltimateTriggered)
                {
                    character.AttackUltimate();
                    isUltimateTriggered = true; // 暴発を防ぐためのロックをかけます
                    
                    // 次の攻撃を出せるように判定をリセットします
                    lastNormalAttackPressTime = -1f;
                    lastSpecialAttackPressTime = -1f;
                }
            }
            else
            {
                // --- 単発入力の処理（同時押しの猶予時間を過ぎていたら発動します） ---
                
                // 通常攻撃 (J)
                // 押してから少し時間が経った（同時押しじゃないと確定した）場合に出ます
                if (lastNormalAttackPressTime > 0 && Time.time - lastNormalAttackPressTime > simultaneousPressWindow)
                {
                    character.AttackNormal();
                    lastNormalAttackPressTime = -1f;
                    isUltimateTriggered = false;
                }
                
                // 特殊攻撃 (K)
                if (lastSpecialAttackPressTime > 0 && Time.time - lastSpecialAttackPressTime > simultaneousPressWindow)
                {
                    character.AttackSpecial();
                    lastSpecialAttackPressTime = -1f;
                    isUltimateTriggered = false;
                }
            }
            
            // キーを「離した瞬間」にロックを解除して、また必殺技を出せるようにします
            if (Keyboard.current[normalAttackKey].wasReleasedThisFrame || Keyboard.current[specialAttackKey].wasReleasedThisFrame)
            {
                isUltimateTriggered = false;
            }

            // --- スタンスキル (C) ---
            if (Keyboard.current[stunAttackKey].wasPressedThisFrame)
            {
                character.AttackStun();
            }
        }

        // ==========================================
        // ブロック・パリーの処理
        // ==========================================
        private void HandleBlock()
        {
            // Hキーを押した瞬間 → StartBlock（パリー窓を開く）
            if (Keyboard.current[blockKey].wasPressedThisFrame)
            {
                character.StartBlock();
            }
            // Hキーを離した瞬間 → StopBlock（ブロック解除）
            if (Keyboard.current[blockKey].wasReleasedThisFrame)
            {
                character.StopBlock();
            }
        }
    }
}
