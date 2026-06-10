using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapPrefabBuilder — 플레이스홀더 함정 프리팹 생성 + Build UI 씬 배선
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → 함정 프리팹 + Build UI 세팅</b>:
    /// <list type="number">
    ///   <item>SpikeTrap / ArrowShooter(+Arrow) / DropHammer 플레이스홀더 프리팹 생성</item>
    ///   <item>Managers 에 BuildPhaseController + TrapMutationManager 추가</item>
    ///   <item>프리팹 리스트 자동 할당 + 씬 저장</item>
    /// </list>
    /// 이미 있는 항목은 건너뛰므로 여러 번 실행해도 안전합니다.
    /// 아트 확정 후 프리팹의 Visual 스프라이트만 교체하면 됩니다.
    /// </summary>
    public static class TrapPrefabBuilder
    {
        private const string PrefabDir   = "Assets/_Project/Prefabs/Traps";
        private const string TileSprite  = "Assets/_Project/ResourcceEX/Sprites/Environment/CorruptedCastleTile.png";

        [MenuItem("ReTrap/Setup/함정 프리팹 + Build UI 세팅")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("함정 세팅",
                    "플레이 모드에서는 실행할 수 없습니다.", "확인");
                return;
            }

            EnsureFolder();
            Sprite sprite = LoadPlaceholderSprite();

            var arrow  = BuildArrowProjectile(sprite);
            var spike  = BuildSpikeTrap(sprite);
            var shooter= BuildArrowShooter(sprite, arrow);
            var hammer = BuildDropHammer(sprite);

            WireScene(new List<GameObject> { spike, shooter, hammer });

            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[TrapPrefabBuilder] ✅ 함정 프리팹 + Build UI 세팅 완료");
        }

        // ── 폴더 / 스프라이트 ────────────────────────────────────────────────

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Traps");
        }

        private static Sprite LoadPlaceholderSprite()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(TileSprite);
            if (s == null)
                Debug.LogWarning("[TrapPrefabBuilder] 타일 스프라이트를 찾지 못해 " +
                                 "스프라이트 없이 생성합니다 (색상 블록도 안 보일 수 있음).");
            return s;
        }

        // ── 프리팹 빌더 공통 ─────────────────────────────────────────────────

        private static GameObject SavePrefab(GameObject temp, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab  = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            Debug.Log($"[TrapPrefabBuilder] 프리팹 생성 → {path}");
            return prefab;
        }

        private static GameObject ExistingPrefab(string name)
            => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");

        private static SpriteRenderer AddVisual(GameObject root, Sprite sprite, Color tint,
                                                Vector2 scale, Vector2 localPos)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = localPos;
            visual.transform.localScale    = new Vector3(scale.x, scale.y, 1f);

            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.color        = tint;
            sr.sortingOrder = 20; // 타일(0)·슬롯(10) 위
            return sr;
        }

        private static Rigidbody2D AddKinematicRb(GameObject root)
        {
            var rb = root.GetComponent<Rigidbody2D>();
            if (rb == null) rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType     = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            return rb;
        }

        // ── 개별 프리팹 ───────────────────────────────────────────────────────

        /// <summary>Arrow 발사체 — Trigger + Rigidbody2D(중력 0).</summary>
        private static GameObject BuildArrowProjectile(Sprite sprite)
        {
            var existing = ExistingPrefab("Arrow");
            if (existing != null) return existing;

            var go = new GameObject("Arrow");
            AddVisual(go, sprite, new Color(0.9f, 0.9f, 0.3f), new Vector2(0.5f, 0.15f), Vector2.zero);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size      = new Vector2(0.5f, 0.15f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType     = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            go.AddComponent<Arrow>();
            return SavePrefab(go, "Arrow");
        }

        /// <summary>SpikeTrap — 셀 하단 가시. 루트 콜라이더가 damageArea 폴백.</summary>
        private static GameObject BuildSpikeTrap(Sprite sprite)
        {
            var existing = ExistingPrefab("SpikeTrap");
            if (existing != null) return existing;

            var go = new GameObject("SpikeTrap");
            AddKinematicRb(go);

            var sr = AddVisual(go, sprite, new Color(0.85f, 0.2f, 0.2f),
                               new Vector2(0.8f, 0.4f), new Vector2(0f, -0.3f));

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size      = new Vector2(0.8f, 0.4f);
            col.offset    = new Vector2(0f, -0.3f);

            var trap = go.AddComponent<SpikeTrap>();
            var so   = new SerializedObject(trap);
            so.FindProperty("spikeVisual").objectReferenceValue = sr.transform;
            so.FindProperty("baseCost").intValue                = 10;
            so.FindProperty("dangerLevel").intValue             = 3;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(go, "SpikeTrap");
        }

        /// <summary>ArrowShooter — FirePoint 자식 + Arrow 프리팹 연결.</summary>
        private static GameObject BuildArrowShooter(Sprite sprite, GameObject arrowPrefab)
        {
            var existing = ExistingPrefab("ArrowShooter");
            if (existing != null) return existing;

            var go = new GameObject("ArrowShooter");
            AddKinematicRb(go);
            AddVisual(go, sprite, new Color(0.3f, 0.5f, 0.9f),
                      new Vector2(0.6f, 0.6f), Vector2.zero);

            // 발사 지점 (벽 바깥쪽으로 facingRight 에 따라 좌우 — 기본 오른쪽)
            var firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(go.transform, false);
            firePoint.transform.localPosition = new Vector3(0.45f, 0f, 0f);

            // 루트 트리거 (플레이어 직접 접촉 감지용 — 슈터 본체는 데미지 없음이라 작게)
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size      = new Vector2(0.6f, 0.6f);

            var trap = go.AddComponent<ArrowShooter>();
            var so   = new SerializedObject(trap);
            so.FindProperty("arrowPrefab").objectReferenceValue = arrowPrefab;
            so.FindProperty("firePoint").objectReferenceValue   = firePoint.transform;
            so.FindProperty("baseCost").intValue                = 25;
            so.FindProperty("dangerLevel").intValue             = 5;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(go, "ArrowShooter");
        }

        /// <summary>DropHammer — Ground/Player 레이어 마스크 자동 설정.</summary>
        private static GameObject BuildDropHammer(Sprite sprite)
        {
            var existing = ExistingPrefab("DropHammer");
            if (existing != null) return existing;

            var go = new GameObject("DropHammer");
            AddKinematicRb(go); // DropHammer 는 RequireComponent(Rigidbody2D)

            AddVisual(go, sprite, new Color(0.4f, 0.35f, 0.45f),
                      new Vector2(0.9f, 0.7f), Vector2.zero);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size      = new Vector2(0.9f, 0.7f);

            int groundLayer = LayerMask.NameToLayer("Ground");
            int playerLayer = LayerMask.NameToLayer("Player");

            var trap = go.AddComponent<DropHammer>();
            var so   = new SerializedObject(trap);
            so.FindProperty("groundLayer").intValue   = groundLayer >= 0 ? (1 << groundLayer) : 0;
            so.FindProperty("detectionMask").intValue = playerLayer >= 0 ? (1 << playerLayer) : ~0;
            so.FindProperty("baseCost").intValue      = 50;
            so.FindProperty("dangerLevel").intValue   = 8;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(go, "DropHammer");
        }

        // ── 씬 배선 ───────────────────────────────────────────────────────────

        private static void WireScene(List<GameObject> prefabs)
        {
            var managers = GameObject.Find("Managers");
            if (managers == null)
            {
                Debug.LogWarning("[TrapPrefabBuilder] 'Managers' 오브젝트가 없어 새로 만듭니다.");
                managers = new GameObject("Managers");
            }

            // TrapMutationManager (누락 시 추가)
            if (managers.GetComponent<TrapMutationManager>() == null)
            {
                managers.AddComponent<TrapMutationManager>();
                Debug.Log("[TrapPrefabBuilder] TrapMutationManager 추가");
            }

            // BuildPhaseController + 프리팹 리스트
            var ctrl = managers.GetComponent<BuildPhaseController>();
            if (ctrl == null) ctrl = managers.AddComponent<BuildPhaseController>();

            var so   = new SerializedObject(ctrl);
            var list = so.FindProperty("trapPrefabs");
            list.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(managers.scene);
            Debug.Log($"[TrapPrefabBuilder] BuildPhaseController 배선 완료 — 함정 {prefabs.Count}종");
        }
    }
}
