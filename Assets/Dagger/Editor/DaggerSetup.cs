#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FightingGameBase.Editor
{
    public class DaggerSetup
    {
        [MenuItem("FightingGame/ダガーキャラクターを作成する")]
        public static void CreateDaggerCharacter()
        {
            // 1. スプライト画像のアセット設定の最適化
            string spritePath = "Assets/Dagger/Dagger.png";
            TextureImporter textureImporter = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (textureImporter != null)
            {
                bool needsReimport = false;
                if (textureImporter.textureType != TextureImporterType.Sprite)
                {
                    textureImporter.textureType = TextureImporterType.Sprite;
                    needsReimport = true;
                }
                
                // TextureImporterSettings を取得してカスタムピボット（柄の位置）を設定
                TextureImporterSettings settings = new TextureImporterSettings();
                textureImporter.ReadTextureSettings(settings);
                
                if (settings.spriteAlignment != (int)SpriteAlignment.Custom ||
                    settings.spritePivot != new Vector2(0.5f, 0.15f))
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(0.5f, 0.15f); // 左右中央、下から15%の位置（柄の部分）
                    textureImporter.SetTextureSettings(settings);
                    needsReimport = true;
                }
                
                // ドット絵がにじまないようにポイントフィルタ（点フィルタ）を設定
                if (textureImporter.filterMode != FilterMode.Point)
                {
                    textureImporter.filterMode = FilterMode.Point;
                    needsReimport = true;
                }

                // 圧縮によるノイズを防ぐため非圧縮に設定
                if (textureImporter.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    needsReimport = true;
                }

                if (needsReimport)
                {
                    textureImporter.SaveAndReimport();
                    Debug.Log("ダガーのスプライト画像を柄（ピボット15%）基準に設定し、再インポートしました。");
                }
            }

            Sprite daggerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (daggerSprite == null)
            {
                Debug.LogWarning($"スプライト画像が見つかりませんでした: {spritePath}\n先に画像を配置してください。");
            }

            // 2. ルート（一番親）となるオブジェクトの作成
            GameObject root = new GameObject("DaggerCharacter");
            
            // 物理エンジンの設定
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.0f; // 重力落下は通常通り行い、地面に接地させる
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 食らい判定用のコライダー（地面衝突と攻撃を受ける用）
            // お手本キャラクター（TemplateCharacter）と同じサイズに設定
            CapsuleCollider2D hurtCollider = root.AddComponent<CapsuleCollider2D>();
            hurtCollider.size = new Vector2(1.0f, 2.0f);
            hurtCollider.offset = new Vector2(0f, 0f); 

            // キャラクターカスタムスクリプトと食らい判定を設定
            DaggerCharacter characterBase = root.AddComponent<DaggerCharacter>();
            Hurtbox hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.owner = characterBase;
            
            // 入力コントローラーをアタッチし、キーマップを設定
            PlayerInputController inputController = root.AddComponent<PlayerInputController>();
            inputController.leftKey = UnityEngine.InputSystem.Key.A;
            inputController.rightKey = UnityEngine.InputSystem.Key.D;
            inputController.jumpKey = UnityEngine.InputSystem.Key.Space;
            inputController.normalAttackKey = UnityEngine.InputSystem.Key.J;
            inputController.specialAttackKey = UnityEngine.InputSystem.Key.K;
            inputController.blockKey = UnityEngine.InputSystem.Key.H;

            // 3. 見た目（グラフィック）とアニメーション用の階層
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform);
            // お手本キャラクターと同じスケールと位置に設定
            visuals.transform.localScale = Vector3.one;
            visuals.transform.localPosition = Vector3.zero;
            
            SpriteRenderer sr = visuals.AddComponent<SpriteRenderer>();
            // お手本キャラクターと同じ四角のシンプルな見た目に設定
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1f, 2f);
            
            // 武器（ダガー）の見た目オブジェクトをVisualsの子として追加
            GameObject weapon = new GameObject("Weapon");
            weapon.transform.SetParent(visuals.transform);
            weapon.transform.localScale = Vector3.one;
            // 通常攻撃判定（PunchHitbox）と完全に同じ位置に配置
            weapon.transform.localPosition = new Vector3(0.7f, 0.5f, 0f);

            SpriteRenderer weaponSr = weapon.AddComponent<SpriteRenderer>();
            weaponSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            weaponSr.drawMode = SpriteDrawMode.Sliced; // Slicedモードを使用して正確なサイズを設定
            weaponSr.size = new Vector2(1.4f, 0.6f); // 当たり判定（PunchHitboxのBoxCollider2D）と同じサイズに設定
            weaponSr.color = new Color(0.7f, 0.7f, 0.7f, 1f); // 少しグレーに設定
            weaponSr.sortingOrder = 5; // キャラクターの手前に表示

            visuals.AddComponent<Animator>();

            // 浮遊エフェクトスクリプトを追加
            visuals.AddComponent<HoverEffect>();

            // 4. 攻撃判定（Hitbox）の階層
            GameObject hitboxObj = new GameObject("PunchHitbox");
            hitboxObj.transform.SetParent(root.transform);
            hitboxObj.transform.localPosition = new Vector3(0.7f, 0.5f, 0f); 
            
            BoxCollider2D hitCollider = hitboxObj.AddComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            hitCollider.size = new Vector2(1.4f, 0.6f);

            Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();
            hitbox.damage = 8; // ダガーなので一撃は軽めの8ダメージ（スピードで勝負）
            hitbox.ownerPlayerID = 1;

            hitboxObj.SetActive(false); // 初期状態は非アクティブ

            // 5. ブロック・パリー用シールドマーク（大剣Taikenと全く同じデザイン）
            GameObject shieldObj = new GameObject("BlockShield");
            shieldObj.transform.SetParent(visuals.transform); // 大剣と同様にVisualsの子にする
            shieldObj.transform.localPosition = new Vector3(0.2f, 0.2f, 0f);

            SpriteRenderer shieldSr = shieldObj.AddComponent<SpriteRenderer>();
            // ビルトインの円形スプライト（Knob）を使ってシールドを表現
            shieldSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            shieldSr.color = new Color(0.3f, 0.8f, 1.0f, 0.55f); // 大剣と同一の半透明ライトブルー
            shieldSr.transform.localScale = new Vector3(0.45f, 0.45f, 1f); // 大剣と同一の大きさ
            shieldSr.sortingOrder = 1; // キャラクターの前面に表示

            shieldObj.SetActive(false); // 初期状態は非表示

            // 6. ステータス（CharacterStats）の作成とセット
            string folderPath = "Assets/Dagger";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            string statsPath = folderPath + "/DaggerStats.asset";
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStats>();
                stats.maxHP = 100;       // 標準体力（100）
                stats.moveSpeed = 7.5f;  // ダガー使いらしく極めて速い移動速度（7.5f）
                stats.jumpForce = 12f;   // 標準的なジャンプ力
                AssetDatabase.CreateAsset(stats, statsPath);
                AssetDatabase.SaveAssets();
            }
            characterBase.stats = stats;

            // 7. 自動でプレハブ化して保存する
            string prefabPath = folderPath + "/Dagger.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);

            // 作成したオブジェクトを選択状態にする
            Selection.activeGameObject = root;
            
            Debug.Log($"【ダガー作成完了】ダガーキャラクターのプレハブを作成しました！\n場所: {prefabPath} に保存されています。");
        }
    }
}
#endif
