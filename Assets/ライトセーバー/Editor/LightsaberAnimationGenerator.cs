#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FightingGameBase.Editor
{
    /// <summary>
    /// ライトセーバーの Unity アニメーションファイル（.anim, .controller）を自動生成するエディター拡張
    /// </summary>
    [InitializeOnLoad]
    public class LightsaberAnimationGenerator
    {
        static LightsaberAnimationGenerator()
        {
            EditorApplication.delayCall += EnsureAnimationsExist;
        }

        private static void EnsureAnimationsExist()
        {
            string animFolder = "Assets/ライトセーバー/Animations";
            string controllerPath = animFolder + "/LightsaberAnimatorController.controller";

            // アニメーションコントローラーがまだ作成されていなければ自動生成
            if (!System.IO.File.Exists(controllerPath))
            {
                CreateLightsaberAnimations();
            }
        }

        [MenuItem("FightingGame/ライトセーバーのアニメーションファイルを生成する (.anim / .controller)")]
        public static void CreateLightsaberAnimations()
        {
            string folderPath = "Assets/ライトセーバー/Animations";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            // -------------------------------------------------------------
            // 1. 通常振り下ろしアニメーションクリップ (.anim)
            // -------------------------------------------------------------
            AnimationClip slashClip = new AnimationClip { name = "Lightsaber_OverheadSlash" };

            // Z軸回転カーブ（振りかぶり 25° → 振り下ろし -75° → 反動 -65° → 復帰 0°）
            AnimationCurve slashRotZ = new AnimationCurve(
                new Keyframe(0.00f, 0.0f),
                new Keyframe(0.05f, 25.0f),  // 振りかぶり
                new Keyframe(0.13f, -75.0f), // 高速振り下ろし
                new Keyframe(0.18f, -65.0f), // 反動
                new Keyframe(0.32f, 0.0f)    // 復帰
            );

            // 位置Xカーブ（後退 → 前方踏み込み → 復帰）
            AnimationCurve slashPosX = new AnimationCurve(
                new Keyframe(0.00f, 0.0f),
                new Keyframe(0.05f, -0.1f),
                new Keyframe(0.13f, 0.25f),
                new Keyframe(0.18f, 0.20f),
                new Keyframe(0.32f, 0.0f)
            );

            // 位置Yカーブ（上昇 → 着地）
            AnimationCurve slashPosY = new AnimationCurve(
                new Keyframe(0.00f, 0.0f),
                new Keyframe(0.05f, 0.15f),
                new Keyframe(0.13f, -0.10f),
                new Keyframe(0.18f, -0.05f),
                new Keyframe(0.32f, 0.0f)
            );

            EditorCurveBinding bindRotZ = EditorCurveBinding.FloatCurve("Visuals", typeof(Transform), "m_LocalRotation.z");
            EditorCurveBinding bindPosX = EditorCurveBinding.FloatCurve("Visuals", typeof(Transform), "m_LocalPosition.x");
            EditorCurveBinding bindPosY = EditorCurveBinding.FloatCurve("Visuals", typeof(Transform), "m_LocalPosition.y");

            AnimationUtility.SetEditorCurve(slashClip, bindRotZ, slashRotZ);
            AnimationUtility.SetEditorCurve(slashClip, bindPosX, slashPosX);
            AnimationUtility.SetEditorCurve(slashClip, bindPosY, slashPosY);

            string slashClipPath = folderPath + "/Lightsaber_OverheadSlash.anim";
            AssetDatabase.CreateAsset(slashClip, slashClipPath);

            // -------------------------------------------------------------
            // 2. 重撃振り下ろしスラムアニメーションクリップ (.anim)
            // -------------------------------------------------------------
            AnimationClip heavyClip = new AnimationClip { name = "Lightsaber_HeavySlam" };

            AnimationCurve heavyRotZ = new AnimationCurve(
                new Keyframe(0.00f, 0.0f),
                new Keyframe(0.11f, 45.0f),  // 大きな振りかぶり
                new Keyframe(0.20f, -95.0f), // 地面スラム
                new Keyframe(0.27f, -90.0f), // インパクト
                new Keyframe(0.45f, 0.0f)    // 復帰
            );

            AnimationCurve heavyPosX = new AnimationCurve(
                new Keyframe(0.00f, 0.0f),
                new Keyframe(0.11f, -0.2f),
                new Keyframe(0.20f, 0.40f),
                new Keyframe(0.27f, 0.35f),
                new Keyframe(0.45f, 0.0f)
            );

            AnimationUtility.SetEditorCurve(heavyClip, bindRotZ, heavyRotZ);
            AnimationUtility.SetEditorCurve(heavyClip, bindPosX, heavyPosX);

            string heavyClipPath = folderPath + "/Lightsaber_HeavySlam.anim";
            AssetDatabase.CreateAsset(heavyClip, heavyClipPath);

            // -------------------------------------------------------------
            // 3. 3段連続振り下ろしアニメーションクリップ (.anim)
            // -------------------------------------------------------------
            AnimationClip tripleClip = new AnimationClip { name = "Lightsaber_TripleSlash" };

            AnimationCurve tripleRotZ = new AnimationCurve(
                new Keyframe(0.00f, 0.0f),
                new Keyframe(0.10f, 30.0f),  // 1段目 振りかぶり
                new Keyframe(0.20f, -60.0f), // 1段目 振り下ろし
                new Keyframe(0.30f, 40.0f),  // 2段目 逆振りかぶり
                new Keyframe(0.40f, -85.0f), // 3段目 フィニッシュ振り下ろし
                new Keyframe(0.60f, 0.0f)    // 復帰
            );

            AnimationUtility.SetEditorCurve(tripleClip, bindRotZ, tripleRotZ);

            string tripleClipPath = folderPath + "/Lightsaber_TripleSlash.anim";
            AssetDatabase.CreateAsset(tripleClip, tripleClipPath);

            // -------------------------------------------------------------
            // 4. Animator Controller (.controller) の構築
            // -------------------------------------------------------------
            string controllerPath = folderPath + "/LightsaberAnimatorController.controller";
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // パラメーター追加
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Damage", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Stunned", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackStun", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackNormal", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackSpecial", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackUltimate", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            // ステート作成
            AnimatorState idleState = stateMachine.AddState("Idle");
            stateMachine.defaultState = idleState;

            AnimatorState slashState = stateMachine.AddState("OverheadSlash");
            slashState.motion = slashClip;

            AnimatorState heavyState = stateMachine.AddState("HeavySlam");
            heavyState.motion = heavyClip;

            AnimatorState tripleState = stateMachine.AddState("TripleSlash");
            tripleState.motion = tripleClip;

            // 遷移（Transitions）の設定
            AnimatorStateTransition t1 = stateMachine.AddAnyStateTransition(slashState);
            t1.AddCondition(AnimatorConditionMode.If, 0, "AttackNormal");
            t1.hasExitTime = false;

            AnimatorStateTransition t2 = stateMachine.AddAnyStateTransition(heavyState);
            t2.AddCondition(AnimatorConditionMode.If, 0, "AttackSpecial");
            t2.hasExitTime = false;

            AnimatorStateTransition t3 = stateMachine.AddAnyStateTransition(tripleState);
            t3.AddCondition(AnimatorConditionMode.If, 0, "AttackUltimate");
            t3.hasExitTime = false;

            // 攻撃終了から Idle への復帰
            AnimatorStateTransition back1 = slashState.AddTransition(idleState);
            back1.hasExitTime = true;
            back1.exitTime = 0.9f;

            AnimatorStateTransition back2 = heavyState.AddTransition(idleState);
            back2.hasExitTime = true;
            back2.exitTime = 0.9f;

            AnimatorStateTransition back3 = tripleState.AddTransition(idleState);
            back3.hasExitTime = true;
            back3.exitTime = 0.9f;

            // プレハブの Animator にコントローラーを自動セット
            string prefabPath = "Assets/ライトセーバー/LightsaberCharacter.prefab";
            GameObject prefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabObj != null)
            {
                Transform visuals = prefabObj.transform.Find("Visuals");
                if (visuals != null)
                {
                    Animator anim = visuals.GetComponent<Animator>();
                    if (anim == null)
                    {
                        anim = visuals.gameObject.AddComponent<Animator>();
                    }
                    anim.runtimeAnimatorController = controller;
                    EditorUtility.SetDirty(prefabObj);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"【完了】Unityアニメーションファイル（.anim, .controller）を作成し、プレハブに割り当てました！\n保存場所: {folderPath}");
        }
    }
}
#endif
