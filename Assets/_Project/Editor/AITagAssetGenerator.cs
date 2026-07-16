using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AITagAssetGenerator — AI 태그 35종 + 기본 아키타입 에셋 자동 생성
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 기획 확정표(이동 11 / 함정 방어·면역 12 / 스펙 12 = 35종)를 코드에 내장해
    /// <see cref="AITagDefinition"/> 에셋을 일괄 생성/갱신합니다. 또한 기본
    /// <see cref="AgentArchetype"/>("지구둥글론자") 에셋도 함께 확보합니다.
    /// <para>
    /// 멱등 실행: 이미 있는 에셋은 로드하여 <c>EditorInit</c>로 값을 덮어쓰고,
    /// 없으면 새로 만듭니다. 이동 태그의 <c>movementTrait</c>·<c>eventHooks</c> 는
    /// <see cref="TagTable"/> 의 traitType/hookTypes 매핑에 따라 Trait/Hook SO 에셋을
    /// 멱등 생성/배선합니다(<c>EditorSetBehaviors</c>). <c>icon</c> 등 아직 매핑이 없는
    /// 참조 필드만 건드리지 않습니다.
    /// </para>
    /// </summary>
    public static class AITagAssetGenerator
    {
        private const string TAG_DIR        = "Assets/_Project/Settings/AI/Tags";
        private const string ARCHETYPE_DIR  = "Assets/_Project/Settings/AI/Archetypes";
        private const string TRAIT_DIR      = "Assets/_Project/Settings/AI/Traits";
        private const string HOOK_DIR       = "Assets/_Project/Settings/AI/Hooks";
        private const string GOBLIN_PREFAB  = "Assets/_Project/ResourcceEX/Prefabs/Enemies/Enemy_Goblin_Raider.prefab";

        // 배타 그룹 상수 — 같은 그룹 번호끼리는 동시 부여 금지
        private const int GROUP_SPEED = 1; // 이속 변경 태그끼리 중복 금지
        private const int GROUP_JUMP  = 2; // 점프 프로파일

        private static readonly StatModifier[] NoMods = new StatModifier[0];

        /// <summary>태그 1종의 테이블 행.</summary>
        private struct TagRow
        {
            public string        id;
            public AITagCategory category;
            public string        name;
            public string        desc;
            public int           group;
            public StatModifier[] mods;
            public DamageType    immunities;
            public SpecialFlag   flags;

            /// <summary>이동 태그의 런타임 로직 SO 타입 (null = 이동 태그 아님).</summary>
            public System.Type traitType;

            /// <summary>수명주기 이벤트 훅 SO 타입 목록 (null/빈 배열 = 훅 없음).</summary>
            public System.Type[] hookTypes;

            public TagRow(string id, AITagCategory category, string name, string desc,
                          int group = 0, StatModifier[] mods = null,
                          DamageType immunities = DamageType.None, SpecialFlag flags = SpecialFlag.None,
                          System.Type traitType = null, System.Type[] hookTypes = null)
            {
                this.id         = id;
                this.category   = category;
                this.name       = name;
                this.desc       = desc;
                this.group      = group;
                this.mods       = mods ?? NoMods;
                this.immunities = immunities;
                this.flags      = flags;
                this.traitType  = traitType;
                this.hookTypes  = hookTypes;
            }
        }

        private static StatModifier Mod(AgentStatType stat, StatModifierOp op, float value)
        {
            return new StatModifier { stat = stat, op = op, value = value };
        }

        // ── 태그 35종 테이블 (기획 확정값) ────────────────────────────────────
        private static readonly TagRow[] TagTable =
        {
            // ── 이동(Movement) 11종 ──────────────────────────────────────────
            new TagRow("M-01", AITagCategory.Movement, "천진난만",
                "착지 즉시 쿨타임 없이 점프(가로 2칸)",
                traitType: typeof(AutoHopTrait)),
            new TagRow("M-02", AITagCategory.Movement, "과속/직진",
                "기본 이동 속도 +50%", GROUP_SPEED,
                new[] { Mod(AgentStatType.MoveSpeed, StatModifierOp.Multiply, 1.5f) }),
            new TagRow("M-03", AITagCategory.Movement, "신중함",
                "전방 2칸 내 함정 감지 시 1초 정지 후 이동",
                traitType: typeof(CautiousPauseTrait)),
            new TagRow("M-04", AITagCategory.Movement, "안전제일",
                "전방 낭떠러지 감지 시 낙하하지 않고 반전",
                traitType: typeof(CliffReverseTrait)),
            new TagRow("M-05", AITagCategory.Movement, "태평함",
                "기본 이동 속도 -30%", GROUP_SPEED,
                new[] { Mod(AgentStatType.MoveSpeed, StatModifierOp.Multiply, 0.7f) }),
            new TagRow("M-06", AITagCategory.Movement, "갈지자",
                "2칸 전진마다 뒤로 1칸 무빙",
                traitType: typeof(ZigzagTrait)),
            new TagRow("M-07", AITagCategory.Movement, "겁쟁이",
                "수평 라인 화살 슈터 감지 시 0.5초 엎드린 후 전진",
                traitType: typeof(CowardTrait)),
            new TagRow("M-08", AITagCategory.Movement, "높이뛰기",
                "점프 가로 짧고 세로 3칸 비상", GROUP_JUMP,
                traitType: typeof(HighJumpTrait)),
            new TagRow("M-09", AITagCategory.Movement, "멀리뛰기",
                "점프 높이 낮고 가로 4칸 주파", GROUP_JUMP,
                traitType: typeof(LongJumpTrait)),
            new TagRow("M-10", AITagCategory.Movement, "잠만보",
                "3칸 전진마다 1초 수면 정지",
                traitType: typeof(SleepwalkerTrait)),
            new TagRow("M-11", AITagCategory.Movement, "스피드스타",
                "기본 이동속도 2배", GROUP_SPEED,
                new[] { Mod(AgentStatType.MoveSpeed, StatModifierOp.Multiply, 2.0f) }),

            // ── 방어/면역(Defense) 12종 ──────────────────────────────────────
            new TagRow("D-01", AITagCategory.Defense, "둥글둥글",
                "가시 함정 완전 면역", immunities: DamageType.Spike),
            new TagRow("D-02", AITagCategory.Defense, "조심조심",
                "화살 슈터 투사체 완전 면역", immunities: DamageType.Arrow),
            new TagRow("D-03", AITagCategory.Defense, "뚝배기 보호",
                "드롭 해머 찍기 완전 면역", immunities: DamageType.Hammer),
            new TagRow("D-04", AITagCategory.Defense, "공중부양",
                "지면에서 떠서 이동, 가시 센서 미작동", flags: SpecialFlag.Hover),
            new TagRow("D-05", AITagCategory.Defense, "낙하산",
                "낙하 시 활공 모드로 천천히 하강", flags: SpecialFlag.Glide),
            new TagRow("D-06", AITagCategory.Defense, "철벽 방패",
                "전방 화살 면역, 후방 피격은 유효", flags: SpecialFlag.FrontShieldOnly),
            new TagRow("D-07", AITagCategory.Defense, "스펀지 몸",
                "화살을 데미지 없이 흡수(소멸)", flags: SpecialFlag.AbsorbProjectile),
            new TagRow("D-08", AITagCategory.Defense, "피뢰침",
                "광역 피해를 자신에게 흡수 (전기/마법 예약)", flags: SpecialFlag.LightningRod),
            new TagRow("D-09", AITagCategory.Defense, "함정 혐오",
                "피격 시 해당 함정 기능 영구 정지", flags: SpecialFlag.DestroyTrapOnHit),
            new TagRow("D-10", AITagCategory.Defense, "등산가",
                "오르막 +100%, 평지 -30%",
                traitType: typeof(MountaineerTrait)),
            new TagRow("D-11", AITagCategory.Defense, "지름길 중독",
                "아래 가시 유무 무관 최단 경로로 즉시 낙하"),
            new TagRow("D-12", AITagCategory.Defense, "저주부르미",
                "피격 시 해당 함정 저주 (효과 기획 미정)", flags: SpecialFlag.CurseTrapOnHit),

            // ── 스펙(Stat) 12종 ───────────────────────────────────────────────
            new TagRow("S-01", AITagCategory.Stat, "뚱뚱이",
                "기본 체력 +1", mods: new[] { Mod(AgentStatType.MaxHP, StatModifierOp.Add, 1f) }),
            new TagRow("S-02", AITagCategory.Stat, "경량화",
                "넉백 거리 2배",
                mods: new[] { Mod(AgentStatType.KnockbackMultiplier, StatModifierOp.Multiply, 2f) }),
            new TagRow("S-03", AITagCategory.Stat, "유리몸",
                "체력 1 고정, 미세 충격에도 넉백 극대화",
                mods: new[] { Mod(AgentStatType.KnockbackMultiplier, StatModifierOp.Multiply, 3f) }),
            new TagRow("S-04", AITagCategory.Stat, "도둑",
                "처치 시 재화 획득, 실패 시 재화 도난"),
            new TagRow("S-05", AITagCategory.Stat, "가속",
                "이동속도 무한 증가 (상한 수치 기획 확인 필요)", GROUP_SPEED,
                traitType: typeof(AccelerateTrait)),
            new TagRow("S-06", AITagCategory.Stat, "거대화",
                "크기 2배, 체력 3",
                mods: new[]
                {
                    Mod(AgentStatType.Scale, StatModifierOp.Multiply, 2f),
                    Mod(AgentStatType.MaxHP, StatModifierOp.Add, 2f),
                }),
            new TagRow("S-07", AITagCategory.Stat, "기사단",
                "사망 시 가장 가까운 아군에게 쉴드 1 부여",
                hookTypes: new[] { typeof(KnightShieldHook) }),
            new TagRow("S-08", AITagCategory.Stat, "가성비 타파",
                "스폰 시 유저 보유 코스트 5 차감"),
            new TagRow("S-09", AITagCategory.Stat, "백스텝",
                "함정 피격 시 뒤로 2칸 강제 반동",
                hookTypes: new[] { typeof(BackstepOnHitHook) }),
            new TagRow("S-10", AITagCategory.Stat, "뒷걸음질",
                "플레이어 감지 시 2칸 후퇴 후 전진", flags: SpecialFlag.RetreatFromPlayer),
            new TagRow("S-11", AITagCategory.Stat, "스프린터",
                "스폰 후 5칸 +100% 질주, 이후 -50%", GROUP_SPEED,
                traitType: typeof(SprinterTrait)),
            new TagRow("S-12", AITagCategory.Stat, "유연함",
                "착지 경직 1초 면역", flags: SpecialFlag.NoLandingStun),
        };

        // ── 메뉴 ──────────────────────────────────────────────────────────────

        [MenuItem("ReTrap/Setup/AI 태그 에셋 생성 (35종)")]
        public static void GenerateAll()
        {
            EnsureFolder("Assets/_Project", "Settings");
            EnsureFolder("Assets/_Project/Settings", "AI");
            EnsureFolder("Assets/_Project/Settings/AI", "Tags");
            EnsureFolder("Assets/_Project/Settings/AI", "Archetypes");
            EnsureFolder("Assets/_Project/Settings/AI", "Traits");
            EnsureFolder("Assets/_Project/Settings/AI", "Hooks");

            int created = 0, updated = 0;
            GenerateTags(ref created, ref updated);
            GenerateDefaultArchetype(ref created, ref updated);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AITagAssetGenerator] 완료 — 생성 {created}개, 갱신 {updated}개");
        }

        private static void GenerateTags(ref int created, ref int updated)
        {
            foreach (TagRow row in TagTable)
            {
                string idPart    = row.id.Replace("-", "");
                // 태그명에 경로 예약 문자가 있으면 파일명에서 치환 ("과속/직진" 등)
                string safeName  = row.name.Replace('/', '_').Replace('\\', '_')
                                           .Replace(':', '_').Replace(' ', '_');
                string assetPath = $"{TAG_DIR}/Tag_{idPart}_{safeName}.asset";

                var def   = AssetDatabase.LoadAssetAtPath<AITagDefinition>(assetPath);
                bool isNew = def == null;
                if (isNew)
                    def = ScriptableObject.CreateInstance<AITagDefinition>();

                def.EditorInit(row.id, row.category, row.name, row.desc, row.group,
                                row.mods, row.immunities, row.flags);

                // 이동 Trait/이벤트 훅 배선 — 테이블이 진실 소스이므로 null/빈 배열도 그대로 대입.
                MovementTrait trait = GetOrCreateAsset<MovementTrait>(TRAIT_DIR, row.traitType, ref created, ref updated);
                TagEventHook[] hooks = BuildHooks(row.hookTypes, ref created, ref updated);
                def.EditorSetBehaviors(trait, hooks);

                if (isNew)
                {
                    AssetDatabase.CreateAsset(def, assetPath);
                    created++;
                }
                else
                {
                    EditorUtility.SetDirty(def);
                    updated++;
                }
            }
        }

        /// <summary>지정 타입의 Trait/Hook SO 에셋을 멱등 생성/로드합니다 (null 타입 → null 반환).</summary>
        private static T GetOrCreateAsset<T>(string dir, System.Type type, ref int created, ref int updated)
            where T : ScriptableObject
        {
            if (type == null) return null;

            string assetPath = $"{dir}/{type.Name}.asset";
            var obj = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            bool isNew = obj == null;
            if (isNew)
            {
                obj = (T)ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(obj, assetPath);
                created++;
            }
            else
            {
                updated++;
            }

            return obj;
        }

        /// <summary>훅 타입 배열로부터 SO 에셋 배열을 멱등 생성/로드합니다.</summary>
        private static TagEventHook[] BuildHooks(System.Type[] hookTypes, ref int created, ref int updated)
        {
            if (hookTypes == null || hookTypes.Length == 0)
                return System.Array.Empty<TagEventHook>();

            var result = new TagEventHook[hookTypes.Length];
            for (int i = 0; i < hookTypes.Length; i++)
                result[i] = GetOrCreateAsset<TagEventHook>(HOOK_DIR, hookTypes[i], ref created, ref updated);

            return result;
        }

        private static void GenerateDefaultArchetype(ref int created, ref int updated)
        {
            const string assetPath = ARCHETYPE_DIR + "/Archetype_Default.asset";

            var archetype = AssetDatabase.LoadAssetAtPath<AgentArchetype>(assetPath);
            bool isNew = archetype == null;
            if (isNew)
                archetype = ScriptableObject.CreateInstance<AgentArchetype>();

            VerificationAgent agentPrefab = null;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GOBLIN_PREFAB);
            if (prefab == null)
            {
                Debug.LogWarning($"[AITagAssetGenerator] 프리팹을 찾을 수 없음: {GOBLIN_PREFAB}");
            }
            else
            {
                agentPrefab = prefab.GetComponent<VerificationAgent>();
                if (agentPrefab == null)
                    Debug.LogWarning($"[AITagAssetGenerator] {GOBLIN_PREFAB} 에 VerificationAgent 컴포넌트가 없음");
            }

            AIPersonality personality = null;
            string[] personalityGuids = AssetDatabase.FindAssets("t:AIPersonality");
            if (personalityGuids.Length > 0)
            {
                string personalityPath = AssetDatabase.GUIDToAssetPath(personalityGuids[0]);
                personality = AssetDatabase.LoadAssetAtPath<AIPersonality>(personalityPath);
            }

            archetype.EditorInit("지구둥글론자", agentPrefab, 4f, 1, 1f, personality);

            if (isNew)
            {
                AssetDatabase.CreateAsset(archetype, assetPath);
                created++;
            }
            else
            {
                EditorUtility.SetDirty(archetype);
                updated++;
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
