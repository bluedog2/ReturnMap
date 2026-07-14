using System.Runtime.CompilerServices;

// ═══════════════════════════════════════════════════════════════════════════
//  AssemblyInfo — 런타임 어셈블리(Assembly-CSharp) 친구 어셈블리 선언
// ═══════════════════════════════════════════════════════════════════════════

// asmdef 가 없어 런타임 스크립트는 Assembly-CSharp, 에디터 스크립트는
// Assembly-CSharp-Editor 로 분리 컴파일된다. 두 어셈블리는 서로 다르므로
// internal 멤버는 기본적으로 보이지 않는다 — 에디터 전용 초기화 메서드
// (예: AITagDefinition.EditorInit, AgentArchetype.EditorInit)를 internal 로
// 유지하면서 Editor 어셈블리에서 직접 호출할 수 있도록 친구 어셈블리로 선언한다.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
