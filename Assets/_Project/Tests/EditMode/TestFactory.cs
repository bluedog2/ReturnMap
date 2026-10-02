using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ReTrap.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TestFactory — EditMode 테스트용 SO(태그·아키타입·스폰 테이블) 생성 헬퍼
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 에셋 없이 메모리상 ScriptableObject 를 만들어 주는 테스트 전용 팩토리.
    /// 만든 객체는 <see cref="DestroyAll"/> 로 TearDown 에서 정리한다.
    /// </summary>
    internal sealed class TestFactory
    {
        private readonly List<Object> _created = new List<Object>();

        public AITagDefinition Tag(string id, AITagCategory category = AITagCategory.Stat,
            int exclusionGroup = 0, StatModifier[] mods = null,
            DamageType immunities = DamageType.None, SpecialFlag flags = SpecialFlag.None)
        {
            var tag = ScriptableObject.CreateInstance<AITagDefinition>();
            tag.EditorInit(id, category, id, "", exclusionGroup, mods, immunities, flags);
            _created.Add(tag);
            return tag;
        }

        public AgentArchetype Archetype(float moveSpeed = 4f, int maxHp = 3)
        {
            var a = ScriptableObject.CreateInstance<AgentArchetype>();
            a.EditorInit("테스트", null, moveSpeed, maxHp, 1f, null);
            _created.Add(a);
            return a;
        }

        /// <summary>스폰 테이블 — SerializedObject 로 private 필드를 채운다.</summary>
        public StageSpawnTable Table(
            (AgentArchetype archetype, int count, AITagDefinition[] fixedTags, int randomSlots)[] entries,
            (AITagDefinition tag, float weight)[] weights)
        {
            var table = ScriptableObject.CreateInstance<StageSpawnTable>();
            _created.Add(table);

            var so = new SerializedObject(table);

            SerializedProperty e = so.FindProperty("entries");
            e.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty el = e.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("archetype").objectReferenceValue = entries[i].archetype;
                el.FindPropertyRelative("count").intValue = entries[i].count;
                el.FindPropertyRelative("randomTagSlots").intValue = entries[i].randomSlots;

                SerializedProperty fx = el.FindPropertyRelative("fixedTags");
                var fixedTags = entries[i].fixedTags ?? new AITagDefinition[0];
                fx.arraySize = fixedTags.Length;
                for (int j = 0; j < fixedTags.Length; j++)
                    fx.GetArrayElementAtIndex(j).objectReferenceValue = fixedTags[j];
            }

            SerializedProperty w = so.FindProperty("baseWeights");
            w.arraySize = weights.Length;
            for (int i = 0; i < weights.Length; i++)
            {
                SerializedProperty el = w.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("tag").objectReferenceValue = weights[i].tag;
                el.FindPropertyRelative("baseWeight").floatValue = weights[i].weight;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        public static StageSpawnTable.TagWeightEntry W(AITagDefinition tag, float weight)
            => new StageSpawnTable.TagWeightEntry { tag = tag, baseWeight = weight };

        public void DestroyAll()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }
    }
}
