using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FightingGameBase
{
    // =================================================================================
    // 【GamepadInputHelper（Xboxコントローラー等の共通入力処理）】
    // 1P・2Pの各プレイヤーに割り当てたゲームパッドのボタンをチェックするための共通クラスです。
    // PC に接続されている順に、1台目のコントローラーが1P、2台目のコントローラーが2Pに
    // 割り当てられます（playerID 1 → Gamepad.all[0]、playerID 2 → Gamepad.all[1]）。
    //
    // キーボードとゲームパッドは同時に使えます（どちらの入力でも反応します）。
    // =================================================================================
    public static class GamepadInputHelper
    {
        public enum GamepadAction
        {
            Jump,           // ジャンプ（Aボタン）
            NormalAttack,   // 通常攻撃（Xボタン）
            SpecialAttack,  // 特殊攻撃（Yボタン）
            Block,          // ブロック・パリー（Bボタン または RBボタン / RT）
            Dash,           // ダッシュ・回避（RBボタン または LT）
            StunAttack,     // スタンスキル（LBボタン）
        }

        private const float StickDeadzone = 0.3f;

        // playerID（1 or 2）に対応するゲームパッドを取得します。繋がっていなければ null を返します。
        private static Gamepad GetGamepad(int playerID)
        {
            int index = playerID - 1;
            if (index >= 0 && index < Gamepad.all.Count)
            {
                return Gamepad.all[index];
            }
            // 1台のみ接続されている場合のフォールバック
            if (playerID == 1 && Gamepad.current != null)
            {
                return Gamepad.current;
            }
            return null;
        }

        // 左スティック・十字キーの左右方向を -1（左）、+1（右）で返します。入力なしは 0。
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

        // ボタンが押されっぱなし（長押し）かどうか
        public static bool IsHeld(int playerID, GamepadAction action)
        {
            Gamepad pad = GetGamepad(playerID);
            if (pad == null) return false;

            switch (action)
            {
                case GamepadAction.Jump:
                    return pad.buttonSouth.isPressed;
                case GamepadAction.NormalAttack:
                    return pad.buttonWest.isPressed;
                case GamepadAction.SpecialAttack:
                    return pad.buttonNorth.isPressed;
                case GamepadAction.Block:
                    // Bボタン または RBボタン または RT のいずれでもガード可能！
                    return pad.buttonEast.isPressed || pad.rightShoulder.isPressed || (pad.rightTrigger != null && pad.rightTrigger.isPressed);
                case GamepadAction.Dash:
                    return pad.rightShoulder.isPressed || (pad.leftTrigger != null && pad.leftTrigger.isPressed);
                case GamepadAction.StunAttack:
                    return pad.leftShoulder.isPressed;
                default:
                    return false;
            }
        }

        // ボタンが「押された瞬間」かどうか
        public static bool WasPressedThisFrame(int playerID, GamepadAction action)
        {
            Gamepad pad = GetGamepad(playerID);
            if (pad == null) return false;

            switch (action)
            {
                case GamepadAction.Jump:
                    return pad.buttonSouth.wasPressedThisFrame;
                case GamepadAction.NormalAttack:
                    return pad.buttonWest.wasPressedThisFrame;
                case GamepadAction.SpecialAttack:
                    return pad.buttonNorth.wasPressedThisFrame;
                case GamepadAction.Block:
                    return pad.buttonEast.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame || (pad.rightTrigger != null && pad.rightTrigger.wasPressedThisFrame);
                case GamepadAction.Dash:
                    return pad.rightShoulder.wasPressedThisFrame || (pad.leftTrigger != null && pad.leftTrigger.wasPressedThisFrame);
                case GamepadAction.StunAttack:
                    return pad.leftShoulder.wasPressedThisFrame;
                default:
                    return false;
            }
        }

        // ボタンが「離された瞬間」かどうか
        public static bool WasReleasedThisFrame(int playerID, GamepadAction action)
        {
            Gamepad pad = GetGamepad(playerID);
            if (pad == null) return false;

            switch (action)
            {
                case GamepadAction.Jump:
                    return pad.buttonSouth.wasReleasedThisFrame;
                case GamepadAction.NormalAttack:
                    return pad.buttonWest.wasReleasedThisFrame;
                case GamepadAction.SpecialAttack:
                    return pad.buttonNorth.wasReleasedThisFrame;
                case GamepadAction.Block:
                    return pad.buttonEast.wasReleasedThisFrame || pad.rightShoulder.wasReleasedThisFrame || (pad.rightTrigger != null && pad.rightTrigger.wasReleasedThisFrame);
                case GamepadAction.Dash:
                    return pad.rightShoulder.wasReleasedThisFrame || (pad.leftTrigger != null && pad.leftTrigger.wasReleasedThisFrame);
                case GamepadAction.StunAttack:
                    return pad.leftShoulder.wasReleasedThisFrame;
                default:
                    return false;
            }
        }
    }
}