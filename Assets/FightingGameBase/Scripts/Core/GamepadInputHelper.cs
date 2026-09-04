using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FightingGameBase
{
    // =================================================================================
    // 【GamepadInputHelper（Xboxコントローラー用の共通入力処理）】
    // 1P・2Pの各プレイヤーに割り当てたゲームパッドのボタンをチェックするための共通クラスです。
    // PC に接続されている順番に、1台目のコントローラーが1P、2台目のコントローラーが2Pに
    // 割り当てられます（playerID 1 → Gamepad.all[0]、playerID 2 → Gamepad.all[1]）。
    //
    // キーボードとゲームパッドは同時に使えます（どちらの入力でも反応します）。
    // PlayerInputController・HammerInputController・GannrannsuInputController の
    // どれからでも共通して使えるように、動作の「役割」ごとにメソッドを用意しています。
    // =================================================================================
    public static class GamepadInputHelper
    {
        // ボタンの役割（キャラクターによって使わない役割があってもOK）
        public enum GamepadAction
        {
            Jump,           // ジャンプ（Aボタン）
            NormalAttack,   // 通常攻撃（Xボタン）
            SpecialAttack,  // 特殊攻撃（Yボタン）
            Block,          // ブロック・パリー（Bボタン）
            Dash,           // ダッシュ・回避（RBボタン）
            StunAttack,     // スタンスキル（LBボタン）
        }

        // スティックをどれくらい倒したら「入力あり」とみなすか
        private const float StickDeadzone = 0.3f;

        // playerID（1 or 2）に対応するゲームパッドを取得します。繋がっていなければ null を返します。
        private static Gamepad GetGamepad(int playerID)
        {
            int index = playerID - 1;
            if (index < 0 || index >= Gamepad.all.Count) return null;
            return Gamepad.all[index];
        }

        // 左スティック・十字キーの左右方向を -1（左）〜 1（右）で返します。入力なしは 0。
        public static float GetMoveDirection(int playerID)
        {
            Gamepad pad = GetGamepad(playerID);
            if (pad == null) return 0f;

            float stickX = pad.leftStick.x.ReadValue();
            if (stickX > StickDeadzone) return 1f;
            if (stickX < -StickDeadzone) return -1f;

            if (pad.dpad.left.isPressed) return -1f;
            if (pad.dpad.right.isPressed) return 1f;

            return 0f;
        }

        private static ButtonControl GetButton(Gamepad pad, GamepadAction action)
        {
            switch (action)
            {
                case GamepadAction.Jump: return pad.buttonSouth;
                case GamepadAction.NormalAttack: return pad.buttonWest;
                case GamepadAction.SpecialAttack: return pad.buttonNorth;
                case GamepadAction.Block: return pad.buttonEast;
                case GamepadAction.Dash: return pad.rightShoulder;
                case GamepadAction.StunAttack: return pad.leftShoulder;
                default: return null;
            }
        }

        // ボタンが押されっぱなしかどうか
        public static bool IsHeld(int playerID, GamepadAction action)
        {
            Gamepad pad = GetGamepad(playerID);
            return pad != null && GetButton(pad, action).isPressed;
        }

        // ボタンが「押された瞬間」かどうか
        public static bool WasPressedThisFrame(int playerID, GamepadAction action)
        {
            Gamepad pad = GetGamepad(playerID);
            return pad != null && GetButton(pad, action).wasPressedThisFrame;
        }

        // ボタンが「離された瞬間」かどうか
        public static bool WasReleasedThisFrame(int playerID, GamepadAction action)
        {
            Gamepad pad = GetGamepad(playerID);
            return pad != null && GetButton(pad, action).wasReleasedThisFrame;
        }
    }
}
