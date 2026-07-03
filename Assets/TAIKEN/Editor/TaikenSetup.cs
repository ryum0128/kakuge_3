#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FightingGameBase.Editor
{
    public class TaikenSetup
    {
        [MenuItem("FightingGame/大剣キャラクターを作成する")]
        public static void CreateFloatingGreatsword()
        {
            // 1. スプライト画像のアセット設定の最適化
            string spritePath = "Assets/TAIKEN/FloatingGreatsword.png";
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
                    Debug.Log("大剣のスプライト画像を柄（ピボット15%）基準に設定し、再インポートしました。");
                }
            }

            Sprite swordSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (swordSprite == null)
            {
                Debug.LogWarning($"スプライト画像が見つかりませんでした: {spritePath}\n先に画像を配置してください。");
            }

            // 2. ルート（一番親）となるオブジェクトの作成
            GameObject root = new GameObject("FloatingGreatsword");
            
            // 物理エンジンの設定（ふわふわ浮遊感を出すため、少しだけ調整）
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.0f; // 重力落下は通常通り行い、地面に接地させる
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 食らい判定用のコライダー（地面衝突と攻撃を受ける用）
            // 大剣の細身の形状にピッタリ合わせる（幅 0.5、高さ 2.4、底面が地面に接地する配置）
            CapsuleCollider2D hurtCollider = root.AddComponent<CapsuleCollider2D>();
            hurtCollider.size = new Vector2(0.5f, 2.4f);
            hurtCollider.offset = new Vector2(0f, 1.2f); 

            // キャラクター基礎スクリプトと食らい判定を設定
            TaikenCharacter characterBase = root.AddComponent<TaikenCharacter>();
            Hurtbox hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.owner = characterBase;
            
            // 入力コントローラーをアタッチし、キーマップを設定
            PlayerInputController inputController = root.AddComponent<PlayerInputController>();
            inputController.leftKey = UnityEngine.InputSystem.Key.A;
            inputController.rightKey = UnityEngine.InputSystem.Key.D;
            inputController.jumpKey = UnityEngine.InputSystem.Key.Space;
            inputController.normalAttackKey = UnityEngine.InputSystem.Key.J;
            inputController.specialAttackKey = UnityEngine.InputSystem.Key.K;

            // 3. 見た目（グラフィック）とアニメーション用の階層
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform);
            // 柄の位置が浮かび上がるように設定（Yを0.6fオフセット）
            visuals.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            
            SpriteRenderer sr = visuals.AddComponent<SpriteRenderer>();
            if (swordSprite != null)
            {
                sr.sprite = swordSprite;
            }
            else
            {
                // 画像がない場合の仮のUIスプライト
                sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(1f, 2f);
            }
            
            visuals.AddComponent<Animator>();

            // ★ 浮遊エフェクトスクリプトを追加
            visuals.AddComponent<HoverEffect>();

            // 4. 攻撃判定（Hitbox）の階層
            GameObject hitboxObj = new GameObject("PunchHitbox");
            hitboxObj.transform.SetParent(root.transform);
            // 振り下ろした際にさらに遠くまで届くように、攻撃判定を前方に1マス分長く設定
            hitboxObj.transform.localPosition = new Vector3(2.1f, 0.8f, 0f); 
            
            BoxCollider2D hitCollider = hitboxObj.AddComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            hitCollider.size = new Vector2(4.2f, 2.0f); // 横方向のリーチをさらに拡大（4.2）

            Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();
            hitbox.damage = 15; // 大剣なので攻撃力は高めの15ダメージ
            hitbox.ownerPlayerID = 1;

            hitboxObj.SetActive(false); // 初期状態は非アクティブ

            // 5. ブロック・パリー用シールドマーク
            GameObject shieldObj = new GameObject("BlockShield");
            shieldObj.transform.SetParent(root.transform);
            shieldObj.transform.localPosition = new Vector3(1.0f, 1.0f, 0f); // 大剣の正面に配置

            SpriteRenderer shieldSr = shieldObj.AddComponent<SpriteRenderer>();
            // ビルトインの円形スプライト（Knob）を使ってシールドを表現
            shieldSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            shieldSr.color = new Color(0.3f, 0.8f, 1.0f, 0.55f); // 半透明シアン（魔法陣っぽい）
            shieldSr.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
            shieldSr.sortingOrder = 1; // キャラクターの前面に表示

            shieldObj.SetActive(false); // 初期状態は非表示

            // 5. ステータス（CharacterStats）の作成とセット
            string folderPath = "Assets/TAIKEN";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            string statsPath = folderPath + "/FloatingGreatswordStats.asset";
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStats>();
                stats.maxHP = 120;       // 体力を少し多めに（120）
                stats.moveSpeed = 6f;    // 浮いているので少し速めに（6.0f）
                stats.jumpForce = 13f;   // ジャンプ力も少し高く（13.0f）
                AssetDatabase.CreateAsset(stats, statsPath);
                AssetDatabase.SaveAssets();
            }
            characterBase.stats = stats;

            // 6. 自動でプレハブ化して保存する
            string prefabPath = folderPath + "/FloatingGreatsword.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);

            // 作成したオブジェクトを選択状態にする
            Selection.activeGameObject = root;
            
            Debug.Log($"【大剣作成完了】浮いてる大剣キャラクターのプレハブを作成しました！\n場所: {prefabPath} に保存されています。");
        }
    }
}
#endif
