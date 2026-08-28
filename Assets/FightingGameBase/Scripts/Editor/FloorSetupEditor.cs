#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace FightingGameBase.Editor
{
    public class FloorSetupEditor
    {
        [MenuItem("FightingGame/床（地面）を作成する")]
        public static void CreateFloor()
        {
            // すでにシーンに床が存在するか確認
            GameObject existingFloor = GameObject.Find("Floor");
            if (existingFloor != null)
            {
                Selection.activeGameObject = existingFloor;
                EditorGUIUtility.PingObject(existingFloor);
                Debug.Log("すでにシーン内に 'Floor' が存在します。選択状態にしました。");
                return;
            }

            // 1. 床オブジェクトの作成
            GameObject floor = new GameObject("Floor");
            floor.transform.position = new Vector3(0f, -2f, 0f);

            // 2. 見た目（スプライト）の設定
            SpriteRenderer sr = floor.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(25f, 1f); // 横幅 25、厚さ 1
            // スタイリッシュなダークグレー（格闘ゲームの床っぽい色）
            sr.color = new Color(0.18f, 0.20f, 0.25f, 1f);
            sr.sortingOrder = -1; // キャラクターの後ろに描画されるように設定

            // 3. 当たり判定（コライダー）の設定
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(25f, 1f);

            // 4. 透明な壁（左右の枠外移動防止用コライダー）の設定
            if (GameObject.Find("Left_Invisible_Wall") == null)
            {
                GameObject leftWall = new GameObject("Left_Invisible_Wall");
                leftWall.transform.position = new Vector3(-12.5f, 8f, 0f);
                BoxCollider2D leftCol = leftWall.AddComponent<BoxCollider2D>();
                leftCol.size = new Vector2(1f, 25f);
            }

            if (GameObject.Find("Right_Invisible_Wall") == null)
            {
                GameObject rightWall = new GameObject("Right_Invisible_Wall");
                rightWall.transform.position = new Vector3(12.5f, 8f, 0f);
                BoxCollider2D rightCol = rightWall.AddComponent<BoxCollider2D>();
                rightCol.size = new Vector2(1f, 25f);
            }

            // シーンの変更をマークして保存できるようにする
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // 作成した床を選択状態にする
            Selection.activeGameObject = floor;
            
            Debug.Log("【完了】シーンに床および透明な壁（左右境界）を作成しました。");
        }
    }
}
#endif
