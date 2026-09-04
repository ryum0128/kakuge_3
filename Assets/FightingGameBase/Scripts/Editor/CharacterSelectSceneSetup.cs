#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FightingGameBase.Editor
{
    // =================================================================================
    // 【CharacterSelectSceneSetup】
    // 数字キーで武器を選んでから対戦を始める「キャラクターセレクトシーン」を
    // メニューから自動生成するためのエディタ拡張です。
    // =================================================================================
    public class CharacterSelectSceneSetup
    {
        [MenuItem("FightingGame/キャラクターセレクトシーンを作成する")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // デフォルトで入ってくる3D用のDirectional Lightは2Dスプライトには不要なので削除
            GameObject light = GameObject.Find("Directional Light");
            if (light != null)
            {
                Object.DestroyImmediate(light);
            }

            // 1. 床（他の対戦シーンと同じ見た目・サイズ）
            GameObject floor = new GameObject("Floor");
            floor.transform.position = new Vector3(0f, -2f, 0f);
            SpriteRenderer sr = floor.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(25f, 1f);
            sr.color = new Color(0.18f, 0.20f, 0.25f, 1f);
            sr.sortingOrder = -1;
            BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
            floorCollider.size = new Vector2(25f, 1f);

            // 2. カメラを他の対戦シーンと同じ画角に合わせる
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.transform.position = new Vector3(0f, 0f, -10f);
            }

            // 3. キャラクターセレクト用マネージャー
            GameObject managerGo = new GameObject("CharacterSelectManager");
            CharacterSelectManager manager = managerGo.AddComponent<CharacterSelectManager>();
            manager.weapons = new[]
            {
                MakeEntry("TAIKEN", "Assets/TAIKEN/Taiken.prefab"),
                MakeEntry("ダガー", "Assets/Dagger/Dagger.prefab"),
                MakeEntry("ライトセーバー", "Assets/ライトセーバー/LightsaberCharacter.prefab"),
                MakeEntry("鞭", "Assets/鞭/WhipCharacter.prefab"),
                MakeEntry("ハンマー", "Assets/hammer/Prefabs/HammerCharacter.prefab"),
                MakeEntry("ガンランス", "Assets/gannrannsu/Prefabs/GannrannsuCharacter.prefab"),
            };

            EditorSceneManager.MarkSceneDirty(scene);

            string path = "Assets/Battles/CharacterSelect.unity";
            bool saved = EditorSceneManager.SaveScene(scene, path);
            if (saved)
            {
                Debug.Log($"【完了】キャラクターセレクトシーンを作成しました: {path}");
            }
            else
            {
                Debug.LogError("キャラクターセレクトシーンの保存に失敗しました。");
            }
        }

        private static CharacterSelectManager.WeaponEntry MakeEntry(string displayName, string prefabPath)
        {
            var entry = new CharacterSelectManager.WeaponEntry { displayName = displayName };
            entry.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (entry.prefab == null)
            {
                Debug.LogWarning($"プレハブが見つかりませんでした: {prefabPath}");
            }
            return entry;
        }
    }
}
#endif
