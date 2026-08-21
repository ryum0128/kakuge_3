#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FightingGameBase.Editor
{
    /// <summary>
    /// 鞭キャラクター（WhipCharacter）のプレハブ、ステータス、アニメーション自動作成用エディター拡張
    /// </summary>
    [InitializeOnLoad]
    public class WhipCharacterSetupEditor
    {
        static WhipCharacterSetupEditor()
        {
            EditorApplication.delayCall += AutoCreateWhipCharacter;
        }

        private static void AutoCreateWhipCharacter()
        {
            string whipFolderPath = "Assets/鞭";
            string whipPrefabPath = whipFolderPath + "/WhipCharacter.prefab";
            if (!System.IO.File.Exists(whipPrefabPath))
            {
                CreateWhipCharacterInternal();
            }
        }

        [MenuItem("FightingGame/鞭キャラクターを作成する")]
        public static void CreateWhipCharacterMenu()
        {
            CreateWhipCharacterInternal();
        }

        public static void CreateWhipCharacterInternal()
        {
            // 1. ルート（一番親）となるオブジェクトの作成
            GameObject root = new GameObject("WhipCharacter");
            
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f; 
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D hurtCollider = root.AddComponent<CapsuleCollider2D>();
            hurtCollider.size = new Vector2(1f, 2f);

            // スクリプトのアタッチ
            WhipCharacter characterBase = root.AddComponent<WhipCharacter>();
            Hurtbox hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.owner = characterBase;
            
            PlayerInputController inputController = root.AddComponent<PlayerInputController>();

            // 2. 見た目（グラフィック）とアニメーション用の階層
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform);
            visuals.transform.localPosition = Vector3.zero;
            visuals.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
            
            SpriteRenderer sr = visuals.AddComponent<SpriteRenderer>();
            
            // 鞭キャラ用の画像があれば自動適用
            string spritePath = "Assets/鞭/WhipCharacterSprite.png";
            EnsureFullRectSprite(spritePath);
            Sprite customSprite = null;
            if (System.IO.File.Exists(spritePath))
            {
                customSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            }

            if (customSprite != null)
            {
                sr.sprite = customSprite;
                hurtCollider.size = new Vector2(customSprite.bounds.size.x * 0.35f, customSprite.bounds.size.y * 0.35f);
            }
            else
            {
                sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                sr.drawMode = SpriteDrawMode.Simple;
                visuals.transform.localScale = new Vector3(0.35f, 0.7f, 1f);
                hurtCollider.size = new Vector2(1f, 2f);
                sr.color = new Color(0.9f, 0.2f, 0.5f); // 鞭キャラ用にマゼンタピンクの色をつける
            }
            
            visuals.AddComponent<FloatingBehavior>();

            // 3. 攻撃判定（WhipHitbox）の階層（超長リーチ設定）
            GameObject hitboxObj = new GameObject("WhipHitbox");
            hitboxObj.transform.SetParent(root.transform);
            // 鞭の長いリーチに合わせて前方に配置 (X: 2.2f)
            hitboxObj.transform.localPosition = new Vector3(2.2f, 0.4f, 0f); 
            
            BoxCollider2D hitCollider = hitboxObj.AddComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            // 横長コライダー（横幅 4.2f）で超長距離攻撃
            hitCollider.size = new Vector2(4.2f, 0.9f);

            Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();
            hitbox.damage = 12;
            hitbox.ownerPlayerID = 1;
            hitboxObj.SetActive(false);

            characterBase.whipHitbox = hitbox;

            // 4. ステータス（CharacterStats）の自動作成とセット
            string folderPath = "Assets/鞭";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            string statsPath = folderPath + "/WhipCharacterStats.asset";
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStats>();
                stats.maxHP = 100;
                stats.moveSpeed = 6.2f;
                stats.jumpForce = 13.0f;
                AssetDatabase.CreateAsset(stats, statsPath);
                AssetDatabase.SaveAssets();
            }
            characterBase.stats = stats;

            // 5. アニメーション（.anim, .controller）の自動作成とアサイン
            AnimatorController animController = CreateWhipAnimations(folderPath);
            Animator anim = visuals.AddComponent<Animator>();
            anim.runtimeAnimatorController = animController;

            // 6. プレハブ化して保存
            string prefabPath = folderPath + "/WhipCharacter.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);

            Selection.activeGameObject = root;
            
            Debug.Log($"【完了】鞭キャラクター（超長リーチ）をプレハブとして作成しました！\n場所: {prefabPath} に保存されています。");
        }

        private static AnimatorController CreateWhipAnimations(string folderPath)
        {
            string animFolder = folderPath + "/Animations";
            if (!System.IO.Directory.Exists(animFolder))
            {
                System.IO.Directory.CreateDirectory(animFolder);
            }

            // 1. 通常攻撃 (.anim)
            AnimationClip lashClip = new AnimationClip { name = "Whip_Lash" };
            AnimationCurve lashRotZ = new AnimationCurve(
                new Keyframe(0.00f, 0.0f),
                new Keyframe(0.07f, 35.0f),  // 巻き上げ
                new Keyframe(0.16f, -80.0f), // 高速スナップ振り下ろし
                new Keyframe(0.21f, -65.0f), // 反動
                new Keyframe(0.35f, 0.0f)    // 回収
            );
            AnimationCurve lashPosX = new AnimationCurve(
                new Keyframe(0.00f, 0.0f),
                new Keyframe(0.07f, -0.25f),
                new Keyframe(0.16f, 0.45f),
                new Keyframe(0.21f, 0.35f),
                new Keyframe(0.35f, 0.0f)
            );
            EditorCurveBinding bRotZ = EditorCurveBinding.FloatCurve("Visuals", typeof(Transform), "m_LocalRotation.z");
            EditorCurveBinding bPosX = EditorCurveBinding.FloatCurve("Visuals", typeof(Transform), "m_LocalPosition.x");
            AnimationUtility.SetEditorCurve(lashClip, bRotZ, lashRotZ);
            AnimationUtility.SetEditorCurve(lashClip, bPosX, lashPosX);
            AssetDatabase.CreateAsset(lashClip, animFolder + "/Whip_Lash.anim");

            // 2. 特殊攻撃 (.anim)
            AnimationClip snapClip = new AnimationClip { name = "Whip_OverheadSnap" };
            AnimationCurve snapRotZ = new AnimationCurve(
                new Keyframe(0.00f, 0.0f),
                new Keyframe(0.12f, 50.0f),  // 大上段溜め
                new Keyframe(0.22f, -100.0f),// 地面スラム
                new Keyframe(0.30f, -90.0f), // 衝撃
                new Keyframe(0.48f, 0.0f)    // 復帰
            );
            AnimationUtility.SetEditorCurve(snapClip, bRotZ, snapRotZ);
            AssetDatabase.CreateAsset(snapClip, animFolder + "/Whip_OverheadSnap.anim");

            // 3. 必殺技 (.anim)
            AnimationClip flurryClip = new AnimationClip { name = "Whip_Flurry" };
            AnimationCurve flurryRotZ = new AnimationCurve(
                new Keyframe(0.00f, 0.0f),
                new Keyframe(0.10f, 20.0f),
                new Keyframe(0.20f, -45.0f),
                new Keyframe(0.32f, 45.0f),
                new Keyframe(0.44f, -90.0f),
                new Keyframe(0.65f, 0.0f)
            );
            AnimationUtility.SetEditorCurve(flurryClip, bRotZ, flurryRotZ);
            AssetDatabase.CreateAsset(flurryClip, animFolder + "/Whip_Flurry.anim");

            // 4. Controller
            string controllerPath = animFolder + "/WhipAnimatorController.controller";
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

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
            AnimatorState idleState = stateMachine.AddState("Idle");
            stateMachine.defaultState = idleState;

            AnimatorState lashState = stateMachine.AddState("WhipLash");
            lashState.motion = lashClip;
            AnimatorState snapState = stateMachine.AddState("WhipOverheadSnap");
            snapState.motion = snapClip;
            AnimatorState flurryState = stateMachine.AddState("WhipFlurry");
            flurryState.motion = flurryClip;

            AnimatorStateTransition t1 = stateMachine.AddAnyStateTransition(lashState);
            t1.AddCondition(AnimatorConditionMode.If, 0, "AttackNormal");
            t1.hasExitTime = false;

            AnimatorStateTransition t2 = stateMachine.AddAnyStateTransition(snapState);
            t2.AddCondition(AnimatorConditionMode.If, 0, "AttackSpecial");
            t2.hasExitTime = false;

            AnimatorStateTransition t3 = stateMachine.AddAnyStateTransition(flurryState);
            t3.AddCondition(AnimatorConditionMode.If, 0, "AttackUltimate");
            t3.hasExitTime = false;

            lashState.AddTransition(idleState).hasExitTime = true;
            snapState.AddTransition(idleState).hasExitTime = true;
            flurryState.AddTransition(idleState).hasExitTime = true;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return controller;
        }

        private static void EnsureFullRectSprite(string path)
        {
            if (System.IO.File.Exists(path))
            {
                TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null)
                {
                    bool changed = false;
                    if (imp.textureType != TextureImporterType.Sprite)
                    {
                        imp.textureType = TextureImporterType.Sprite;
                        imp.spriteImportMode = SpriteImportMode.Single;
                        changed = true;
                    }
                    TextureImporterSettings settings = new TextureImporterSettings();
                    imp.ReadTextureSettings(settings);
                    if (settings.spriteMeshType != SpriteMeshType.FullRect)
                    {
                        settings.spriteMeshType = SpriteMeshType.FullRect;
                        imp.SetTextureSettings(settings);
                        changed = true;
                    }
                    if (changed)
                    {
                        imp.SaveAndReimport();
                    }
                }
            }
        }
    }
}
#endif
