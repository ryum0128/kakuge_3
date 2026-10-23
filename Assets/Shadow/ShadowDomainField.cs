using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FightingGameBase
{
    /// <summary>
    /// Shadowのスペシャル攻撃【影の領域（沈黙フィールド）】
    /// 発動者の位置を中心にステージ幅の半分の広さを持つ闇の結界を生成。
    /// この領域内にいる間は、自身を含めすべてのキャラクターのスペシャル攻撃が封印される。
    /// 10秒経過で自動消滅。
    /// </summary>
    public class ShadowDomainField : MonoBehaviour
    {
        public static readonly List<ShadowDomainField> activeFields = new List<ShadowDomainField>();

        [Header("フィールド基本設定")]
        public float fieldWidth = 12.5f;
        public float fieldHeight = 16.0f;
        public float duration = 10.0f;

        private float timer = 0f;
        private bool isExpiring = false;

        private Transform rootVisuals;
        private SpriteRenderer bgRenderer;
        private SpriteRenderer leftBorderRenderer;
        private SpriteRenderer rightBorderRenderer;
        private SpriteRenderer topBorderRenderer;
        private SpriteRenderer floorMistRenderer;

        private static Sprite cachedSquareSprite;
        private static Sprite cachedSilenceIconSprite;

        // 領域内にいるキャラクターの頭上に表示する沈黙アイコン管理
        private readonly Dictionary<CharacterBase, GameObject> silenceIndicators = new Dictionary<CharacterBase, GameObject>();

        public static bool IsPositionInSilenceField(Vector3 position)
        {
            for (int i = activeFields.Count - 1; i >= 0; i--)
            {
                if (activeFields[i] == null)
                {
                    activeFields.RemoveAt(i);
                    continue;
                }
                if (activeFields[i].ContainsPosition(position))
                {
                    return true;
                }
            }
            return false;
        }

        public bool ContainsPosition(Vector3 position)
        {
            float halfW = fieldWidth * 0.5f;
            float minX = transform.position.x - halfW;
            float maxX = transform.position.x + halfW;
            float minY = transform.position.y - fieldHeight * 0.5f;
            float maxY = transform.position.y + fieldHeight * 0.5f;

            return (position.x >= minX && position.x <= maxX && position.y >= minY && position.y <= maxY);
        }

        void OnEnable()
        {
            if (!activeFields.Contains(this))
            {
                activeFields.Add(this);
            }
        }

        void OnDisable()
        {
            activeFields.Remove(this);
            ClearAllIndicators();
        }

        void OnDestroy()
        {
            activeFields.Remove(this);
            ClearAllIndicators();
        }

        private void ClearAllIndicators()
        {
            foreach (var kvp in silenceIndicators)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            silenceIndicators.Clear();
        }

        /// <summary>
        /// 影の領域（沈黙フィールド）を生成
        /// </summary>
        public static ShadowDomainField Spawn(Vector3 casterPosition, float fieldDuration = 10f)
        {
            GameObject floor = GameObject.Find("Floor");
            float stageWidth = 25f;
            float floorY = -2f;
            if (floor != null)
            {
                SpriteRenderer fsr = floor.GetComponent<SpriteRenderer>();
                if (fsr != null)
                {
                    stageWidth = fsr.size.x;
                    floorY = floor.transform.position.y + fsr.size.y * 0.5f;
                }
                else
                {
                    BoxCollider2D bc = floor.GetComponent<BoxCollider2D>();
                    if (bc != null)
                    {
                        stageWidth = bc.size.x;
                        floorY = floor.transform.position.y + bc.size.y * 0.5f;
                    }
                }
            }

            float width = stageWidth * 0.5f; // ステージの半分
            float height = 16f;

            // キャスターの位置を中心、Yはステージ全体（ジャンプ含む）を覆う高さに調整
            Vector3 spawnCenter = new Vector3(casterPosition.x, floorY + height * 0.45f, 0f);

            GameObject fieldObj = new GameObject("ShadowDomainField");
            fieldObj.transform.position = spawnCenter;

            ShadowDomainField domain = fieldObj.AddComponent<ShadowDomainField>();
            domain.fieldWidth = width;
            domain.fieldHeight = height;
            domain.duration = fieldDuration;
            domain.BuildVisuals();

            // 展開時の衝撃波
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.TriggerScreenShake(0.22f, 0.12f);
            }

            Debug.Log($"<color=#9933ff>[Shadow Domain] 影の領域（沈黙フィールド）展開！ 中心: {spawnCenter.x:F1}, 幅: {width:F1} (ステージ半分), 持続: {fieldDuration}秒</color>");

            return domain;
        }

        public void BuildVisuals()
        {
            GameObject root = new GameObject("VisualsRoot");
            root.transform.SetParent(transform, false);
            rootVisuals = root.transform;

            Sprite square = GetOrCreateSquareSprite();

            // 1. 領域背景（深紫〜漆黒の薄闇ヴェール）
            GameObject bgObj = new GameObject("VoidVeil");
            bgObj.transform.SetParent(rootVisuals, false);
            bgRenderer = bgObj.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = square;
            bgRenderer.drawMode = SpriteDrawMode.Sliced;
            bgRenderer.size = new Vector2(fieldWidth, fieldHeight);
            bgRenderer.color = new Color(0.09f, 0.02f, 0.18f, 0.42f);
            bgRenderer.sortingOrder = -2; // キャラクターの背後に配置

            // 2. 左境界の紫光ピラー（境界線の可視化）
            GameObject leftObj = new GameObject("LeftBorder");
            leftObj.transform.SetParent(rootVisuals, false);
            leftObj.transform.localPosition = new Vector3(-fieldWidth * 0.5f, 0f, 0f);
            leftBorderRenderer = leftObj.AddComponent<SpriteRenderer>();
            leftBorderRenderer.sprite = square;
            leftBorderRenderer.drawMode = SpriteDrawMode.Sliced;
            leftBorderRenderer.size = new Vector2(0.22f, fieldHeight);
            leftBorderRenderer.color = new Color(0.80f, 0.25f, 1.0f, 0.85f);
            leftBorderRenderer.sortingOrder = 4;

            // 3. 右境界の紫光ピラー（境界線の可視化）
            GameObject rightObj = new GameObject("RightBorder");
            rightObj.transform.SetParent(rootVisuals, false);
            rightObj.transform.localPosition = new Vector3(fieldWidth * 0.5f, 0f, 0f);
            rightBorderRenderer = rightObj.AddComponent<SpriteRenderer>();
            rightBorderRenderer.sprite = square;
            rightBorderRenderer.drawMode = SpriteDrawMode.Sliced;
            rightBorderRenderer.size = new Vector2(0.22f, fieldHeight);
            rightBorderRenderer.color = new Color(0.80f, 0.25f, 1.0f, 0.85f);
            rightBorderRenderer.sortingOrder = 4;

            // 4. 天井境界線
            GameObject topObj = new GameObject("TopBorder");
            topObj.transform.SetParent(rootVisuals, false);
            topObj.transform.localPosition = new Vector3(0f, fieldHeight * 0.5f, 0f);
            topBorderRenderer = topObj.AddComponent<SpriteRenderer>();
            topBorderRenderer.sprite = square;
            topBorderRenderer.drawMode = SpriteDrawMode.Sliced;
            topBorderRenderer.size = new Vector2(fieldWidth, 0.15f);
            topBorderRenderer.color = new Color(0.65f, 0.20f, 0.95f, 0.70f);
            topBorderRenderer.sortingOrder = 4;

            // 5. 地面の黒霧
            GameObject mistObj = new GameObject("FloorMist");
            mistObj.transform.SetParent(rootVisuals, false);
            mistObj.transform.localPosition = new Vector3(0f, -fieldHeight * 0.5f + 0.6f, 0f);
            floorMistRenderer = mistObj.AddComponent<SpriteRenderer>();
            floorMistRenderer.sprite = square;
            floorMistRenderer.drawMode = SpriteDrawMode.Sliced;
            floorMistRenderer.size = new Vector2(fieldWidth, 1.2f);
            floorMistRenderer.color = new Color(0.40f, 0.05f, 0.60f, 0.35f);
            floorMistRenderer.sortingOrder = -1;

            // 展開アニメーション（Xスケールが中央から一瞬で広がる）
            StartCoroutine(ExpandFieldRoutine());
        }

        private IEnumerator ExpandFieldRoutine()
        {
            if (rootVisuals == null) yield break;
            rootVisuals.localScale = new Vector3(0.05f, 1f, 1f);

            float dur = 0.20f;
            float el = 0f;
            while (el < dur)
            {
                el += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, el / dur);
                if (rootVisuals != null)
                {
                    rootVisuals.localScale = new Vector3(Mathf.Lerp(0.05f, 1.0f, t), 1f, 1f);
                }
                yield return null;
            }
            if (rootVisuals != null) rootVisuals.localScale = Vector3.one;
        }

        void Update()
        {
            timer += Time.deltaTime;

            // 背景の微細な明滅パルス
            if (bgRenderer != null)
            {
                float pulse = 0.38f + Mathf.PingPong(Time.time * 2f, 0.08f);
                Color c = bgRenderer.color;
                c.a = pulse;
                bgRenderer.color = c;
            }

            // 境界ピラーの妖しい発光ゆらめき
            if (leftBorderRenderer != null && rightBorderRenderer != null)
            {
                float borderAlpha = 0.75f + Mathf.PingPong(Time.time * 4f, 0.20f);
                Color bc = leftBorderRenderer.color;
                bc.a = borderAlpha;
                leftBorderRenderer.color = bc;
                rightBorderRenderer.color = bc;
            }

            // 領域内のキャラクターの判定と沈黙アイコンの管理
            UpdateCharacterSilenceStates();

            // 残り1秒でフェードアウト
            float remaining = duration - timer;
            if (remaining <= 1.0f && !isExpiring)
            {
                isExpiring = true;
                StartCoroutine(FadeOutRoutine(remaining));
            }

            if (timer >= duration)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateCharacterSilenceStates()
        {
            CharacterBase[] allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            HashSet<CharacterBase> insideChars = new HashSet<CharacterBase>();

            for (int i = 0; i < allChars.Length; i++)
            {
                CharacterBase c = allChars[i];
                if (c == null || c.isDead) continue;

                if (ContainsPosition(c.transform.position))
                {
                    insideChars.Add(c);
                    if (!silenceIndicators.ContainsKey(c))
                    {
                        GameObject ind = CreateSilenceIndicator(c);
                        silenceIndicators[c] = ind;
                    }
                }
            }

            // 領域外へ出たキャラクターのアイコンを削除
            List<CharacterBase> toRemove = null;
            foreach (var kvp in silenceIndicators)
            {
                if (kvp.Key == null || !insideChars.Contains(kvp.Key))
                {
                    if (toRemove == null) toRemove = new List<CharacterBase>();
                    toRemove.Add(kvp.Key);
                }
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    CharacterBase c = toRemove[i];
                    if (silenceIndicators.TryGetValue(c, out GameObject ind))
                    {
                        if (ind != null) Destroy(ind);
                        silenceIndicators.Remove(c);
                    }
                }
            }

            // アイコンのゆらめき更新
            foreach (var kvp in silenceIndicators)
            {
                if (kvp.Value != null)
                {
                    float bob = Mathf.Sin(Time.time * 5f) * 0.08f;
                    kvp.Value.transform.localPosition = new Vector3(0f, 2.3f + bob, 0f);
                }
            }
        }

        private GameObject CreateSilenceIndicator(CharacterBase target)
        {
            GameObject indObj = new GameObject("SilenceStatusIndicator");
            indObj.transform.SetParent(target.transform, false);
            indObj.transform.localPosition = new Vector3(0f, 2.3f, 0f);

            SpriteRenderer sr = indObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateSilenceIconSprite();
            sr.sortingOrder = 20; // 最前面に表示
            sr.color = new Color(0.95f, 0.4f, 1.0f, 0.95f);
            indObj.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            return indObj;
        }

        private IEnumerator FadeOutRoutine(float fadeDuration)
        {
            float elapsed = 0f;
            Color bgInit = bgRenderer != null ? bgRenderer.color : Color.clear;
            Color bInit = leftBorderRenderer != null ? leftBorderRenderer.color : Color.clear;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / fadeDuration);

                if (bgRenderer != null)
                {
                    Color c = bgInit;
                    c.a = bgInit.a * t;
                    bgRenderer.color = c;
                }
                if (leftBorderRenderer != null)
                {
                    Color c = bInit;
                    c.a = bInit.a * t;
                    leftBorderRenderer.color = c;
                }
                if (rightBorderRenderer != null)
                {
                    Color c = bInit;
                    c.a = bInit.a * t;
                    rightBorderRenderer.color = c;
                }
                if (topBorderRenderer != null)
                {
                    Color c = topBorderRenderer.color;
                    c.a = 0.70f * t;
                    topBorderRenderer.color = c;
                }
                if (floorMistRenderer != null)
                {
                    Color c = floorMistRenderer.color;
                    c.a = 0.35f * t;
                    floorMistRenderer.color = c;
                }
                yield return null;
            }
        }

        // =========================================================
        // プロシージャルスプライト生成
        // =========================================================

        private static Sprite GetOrCreateSquareSprite()
        {
            if (cachedSquareSprite != null) return cachedSquareSprite;

#if UNITY_EDITOR
            cachedSquareSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (cachedSquareSprite != null) return cachedSquareSprite;
#endif

            int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, Color.white);
                }
            }
            tex.Apply();
            cachedSquareSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
            return cachedSquareSprite;
        }

        private static Sprite GetOrCreateSilenceIconSprite()
        {
            if (cachedSilenceIconSprite != null) return cachedSilenceIconSprite;

            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.42f;

            Color purple = new Color(0.85f, 0.25f, 1.0f, 1.0f);
            Color dark = new Color(0.12f, 0.02f, 0.20f, 0.95f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        // 外枠（紫光リング）
                        if (dist >= radius - 2.5f)
                        {
                            tex.SetPixel(x, y, purple);
                        }
                        else
                        {
                            // 斜線（封印スラッシュ）: x + y == size 付近
                            if (Mathf.Abs((x + y) - size) <= 2)
                            {
                                tex.SetPixel(x, y, purple);
                            }
                            else
                            {
                                tex.SetPixel(x, y, dark);
                            }
                        }
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            cachedSilenceIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return cachedSilenceIconSprite;
        }
    }
}
