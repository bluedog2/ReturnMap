using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  BackgroundTileImporter — 폴더의 스프라이트를 배경 타일로 일괄 등록
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 메뉴 <b>ReTrap → Setup → 배경 타일 일괄 등록 (폴더)</b>:
    /// 선택한 폴더 안의 모든 스프라이트를 어드레서블 그룹 'Backgrounds' 에 등록하고
    /// <see cref="TilePaletteConfig"/> 의 backgroundTiles 에 추가합니다.
    /// 챕터 타일셋(Environment/.../ChapterN)을 통째로 배경 팔레트에 넣을 때 사용합니다.
    /// <para>이미 등록된 스프라이트는 건너뛰므로 여러 번 실행해도 안전(증분 추가)합니다.</para>
    /// </summary>
    public static class BackgroundTileImporter
    {
        private const string PalettePath = "Assets/_Project/Settings/TilePaletteConfig.asset";

        [MenuItem("ReTrap/Setup/배경 타일 일괄 등록 (폴더)")]
        public static void Run()
        {
            string abs = EditorUtility.OpenFolderPanel(
                "배경 타일로 등록할 폴더 선택",
                "Assets/_Project/ResourcceEX/Sprites/Environment", "");
            if (string.IsNullOrEmpty(abs)) return;

            // 절대경로 → Assets 상대경로
            string dataPath = Application.dataPath.Replace('\\', '/');
            abs = abs.Replace('\\', '/');
            if (!abs.StartsWith(dataPath))
            {
                EditorUtility.DisplayDialog("오류", "프로젝트 Assets 폴더 안을 선택하세요.", "확인");
                return;
            }
            string rel = "Assets" + abs.Substring(dataPath.Length);

            int added = ImportFolder(rel, out int total);
            EditorUtility.DisplayDialog("배경 타일 등록",
                $"{rel}\n\n새로 추가: {added}개\n팔레트 총 배경 타일: {total}개", "확인");
        }

        /// <summary>폴더 내 모든 스프라이트를 배경 타일로 등록. 새로 추가된 개수 반환.</summary>
        public static int ImportFolder(string folder, out int total)
        {
            total = 0;
            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteConfig>(PalettePath);
            if (palette == null) { Debug.LogError("[BackgroundTileImporter] 팔레트 없음: " + PalettePath); return 0; }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) { Debug.LogError("[BackgroundTileImporter] Addressable Settings 없음"); return 0; }
            var group = settings.FindGroup("Backgrounds") ?? settings.DefaultGroup;

            // 폴더 내 스프라이트 — 파일명 순 정렬 (격자 순서 r01_c01 …)
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            Array.Sort(guids, (a, b) =>
                string.Compare(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b), StringComparison.Ordinal));

            var so  = new SerializedObject(palette);
            var arr = so.FindProperty("backgroundTiles");

            // 기존 등록 GUID (중복 방지)
            var existing = new HashSet<string>();
            for (int i = 0; i < arr.arraySize; i++)
                existing.Add(arr.GetArrayElementAtIndex(i).FindPropertyRelative("m_AssetGUID").stringValue);

            int added = 0;
            foreach (var g in guids)
            {
                if (existing.Contains(g)) continue;
                string path   = AssetDatabase.GUIDToAssetPath(g);
                var    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                // 어드레서블 등록
                var entry = settings.CreateOrMoveEntry(g, group);
                entry.address = "Bg/" + sprite.name;

                // backgroundTiles 추가 (Single sprite → SubObjectName 불필요)
                arr.arraySize++;
                var el = arr.GetArrayElementAtIndex(arr.arraySize - 1);
                el.FindPropertyRelative("m_AssetGUID").stringValue     = g;
                el.FindPropertyRelative("m_SubObjectName").stringValue = "";

                existing.Add(g);
                added++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();

            total = arr.arraySize;
            Debug.Log($"[BackgroundTileImporter] {folder} → 배경 타일 +{added} (총 {total})");
            return added;
        }
    }
}
