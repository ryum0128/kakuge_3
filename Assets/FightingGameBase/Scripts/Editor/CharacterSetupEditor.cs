#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FightingGameBase.Editor
{
    public class CharacterSetupEditor
    {
        // Unityの上のメニューバーに「FightingGame」という項目を追加します
        [MenuItem("FightingGame/お手本キャラクターを作成する")]
        public static void CreateTemplateCharacter()
        {
            // 1. ルート（一番親）となるオブジェクトの作成
            GameObject root = new GameObject("TemplateCharacter");
            
            // 物理エンジンの設定（格ゲーっぽく重力を強めに、回転しないように設定）
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f; 
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 食らい判定用のコライダー（縦長のピル型）
            CapsuleCollider2D hurtCollider = root.AddComponent<CapsuleCollider2D>();
            hurtCollider.size = new Vector2(1f, 2f);

            // 用意したスクリプトをアタッチ
            CharacterBase characterBase = root.AddComponent<CharacterBase>();
            Hurtbox hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.owner = characterBase;
            
            // 入力コントローラーもアタッチ
            PlayerInputController inputController = root.AddComponent<PlayerInputController>();

            // 2. 見た目（グラフィック）とアニメーション用の階層
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform);
            visuals.transform.localPosition = Vector3.zero;
            
            SpriteRenderer sr = visuals.AddComponent<SpriteRenderer>(); // 2D画像を表示するためのコンポーネント
            // 見やすいように仮の画像（四角形）をセットし、当たり判定と同じサイズにする
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1f, 2f);
            
            visuals.AddComponent<Animator>();       // アニメーションを制御するコンポーネント

            // 3. 攻撃判定（Hitbox）の階層
            GameObject hitboxObj = new GameObject("PunchHitbox");
            hitboxObj.transform.SetParent(root.transform);
            // 少し前方の、手が出るあたりに仮配置
            hitboxObj.transform.localPosition = new Vector3(0.8f, 0.5f, 0f); 
            
            // 攻撃判定用のコライダー（トリガーにするのがポイント！）
            BoxCollider2D hitCollider = hitboxObj.AddComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            hitCollider.size = new Vector2(0.8f, 0.5f);

            // 用意したHitboxスクリプトをアタッチ
            Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();
            hitbox.damage = 10;
            hitbox.ownerPlayerID = 1;

            // 最初は攻撃していないので、非アクティブ（オフ）にしておく
            hitboxObj.SetActive(false);

            // 4. ステータス（CharacterStats）の自動作成とセット
            string folderPath = "Assets/FightingGameBase/Prefabs";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            string statsPath = folderPath + "/TemplateCharacterStats.asset";
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStats>();
                AssetDatabase.CreateAsset(stats, statsPath);
                AssetDatabase.SaveAssets();
            }
            characterBase.stats = stats;

            // 5. 自動でプレハブ化して保存する
            string prefabPath = folderPath + "/TemplateCharacter.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);

            // 作成したオブジェクトを選択状態にする
            Selection.activeGameObject = root;
            
            Debug.Log($"【完了】お手本キャラクターを全部込々にしてプレハブとして作成しました！\n場所: {prefabPath} に保存されています。");
        }
    }
}
#endif
