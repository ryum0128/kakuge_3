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

            GameObject floor = FloorManager.EnsureFloorExists();

            // シーンの変更をマークして保存できるようにする
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // 作成した床を選択状態にする
            Selection.activeGameObject = floor;
            EditorGUIUtility.PingObject(floor);
            
            Debug.Log("【完了】シーンに床および透明な壁（左右壁）を作成しました。");
        }
    }
}
#endif