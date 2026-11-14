# ═══════════════════════════════════════════════════════════════
#  asf-to-px32.py — ASF 프레임 폴더 → 우리 규격 시트(가로 한 줄) + CI 규칙 미리 검사
# ═══════════════════════════════════════════════════════════════
# ASF(scale_frames → palette_tool apply)가 낸 정사각 프레임들을 재생 순서대로 이어 붙여
# Sprites/Px32 규격 시트(<이름>.png, 셀 × 프레임 가로 한 줄)를 만들고,
# CI sprites32(PixelArtSheetValidator)의 캐릭터 규칙을 Unity 없이 미리 검사한다. 최종 판정은 CI.
#   dimensions · alpha 0/255 · palette(팔레트 밖 색) · 프레임당 색 수(캐릭터 24) · 빈 프레임
#   · baseline(발 행 일치, Jump*/Fall* 예외) · drift(가로 중심·높이 ±2px, Death 예외 / 높이는 JumpStart·Land·Hit·Dash 예외)
#   · clip(셀 상·좌·우 1px 테두리)
# 규칙 출처: Assets/_Project/Editor/Px32/PixelArtSheetValidator.cs · Px32Rules.cs — 바뀌면 여기도 맞춘다.
#
# 사용:
#   py -3.12 -I tools/sprite/asf-to-px32.py --frames <dir> --name Hero_Run --expect-frames 8
#          --palette NEWIMAG/palette_v2.txt --out <폴더> [--cell 128] [--place ground-center|ground|asf]
#          [--source <raw.png>] [--lock-file tools/sprite/asf.lock.json] [--settings <settings.json>]
# 배치(--place): 승인 시트와 같게 발=맨 아래 행, 가로 bbox 중심=셀 중앙 으로 정수 이동만 한다(픽셀 값 불변).
#   승인된 Hero_Idle·Run 은 이동량 0 으로 그대로 통과(회귀 확인 2026-11-06).
# 출력: <폴더>/<이름>.png · <이름>_Preview_4x.png · <이름>.asf.json(출처·수치·검사) · 마지막 줄 RESULT=PASS|FAIL
#       exit 0 PASS / 1 FAIL / 2 사용법·입력 오류

import argparse, glob, hashlib, json, os, re, sys
from PIL import Image

MAX_COLORS = 24       # Player · Enemies · Traps
DRIFT_TOL = 2.0


def anim_of(name):
    i = name.rfind('_')
    return name[i + 1:] if i >= 0 else ''


def rules_for(name):
    a = anim_of(name).lower()
    return {
        'airborne': a.startswith('jump') or a in ('rise', 'apex') or a.startswith('fall') or name.lower().startswith('imp'),
        'death': a == 'death',
        'squash': a in ('jumpstart', 'apex', 'land', 'hit', 'dash', 'turn'),
        'shift': a in ('hit', 'runstop', 'turn'),
    }


# Px32Rules.CharacterSpec 복사본: (셀, 프레임 수). Px32Rules.cs 가 바뀌면 같이 고친다.
HERO_CELL = 128
HERO_FRAMES = {'Idle': 8, 'Run': 10, 'JumpStart': 4, 'Rise': 3, 'Apex': 3, 'Fall': 4, 'Land': 4, 'Dash': 6, 'Hit': 4, 'Death': 10,
               'BagOut': 6, 'LongIdle': 12, 'BagIn': 4, 'RunStart': 3, 'RunStop': 4, 'Turn': 3}
ENEMY_FRAMES = {'Idle': 4, 'Run': 6, 'Hit': 2, 'Death': 6}
ENEMY_CELL = (('Goblin', 96), ('Imp', 96), ('Skeleton', 128), ('Elite', 128), ('Orc', 160))


def sheet_spec(name):
    """규격표에 있으면 (셀, 프레임 수), 없으면 None. 대소문자: 접두사는 무시, 애니 이름은 Px32Rules 처럼 정확히."""
    anim = anim_of(name)
    if name.lower().startswith('hero_'):
        return (HERO_CELL, HERO_FRAMES[anim]) if anim in HERO_FRAMES else None
    for prefix, cell in ENEMY_CELL:
        if name.lower().startswith(prefix.lower()):
            return (cell, ENEMY_FRAMES[anim]) if anim in ENEMY_FRAMES else None
    return None


def load_palette(path):
    cols = set()
    for line in open(path, encoding='utf-8'):
        s = line.strip()
        if re.fullmatch(r'#?[0-9A-Fa-f]{6}', s):
            v = int(s.lstrip('#'), 16)
            cols.add(((v >> 16) & 255, (v >> 8) & 255, v & 255))
    return cols


def sha256(path):
    return hashlib.sha256(open(path, 'rb').read()).hexdigest()


def frame_files(d):
    # ASF 이름(…-1.png, …-10.png)도 숫자 순서로
    key = lambda p: [int(t) if t.isdigit() else t for t in re.split(r'(\d+)', os.path.basename(p))]
    return sorted(glob.glob(os.path.join(d, '*.png')), key=key)


def place(im, cell, mode, rules):
    """승인 시트와 같은 배치로 정수 픽셀 이동만 한다(픽셀 값은 그대로).
    ground: 최하단 불투명 행 → cell-1 (Jump*/Fall* 제외)
    ground-center: + 가로 bbox 중심 → cell/2 (Death 제외) — CI drift 규칙과 같은 기준
    셀 밖으로 밀리면 None(잘라내지 않음)."""
    if mode == 'asf': return im, (0, 0)
    box = im.getbbox()
    if box is None: return im, (0, 0)
    x0, y0, x1, y1 = box  # x1·y1 는 끝(포함 안 함)
    dy = 0 if rules['airborne'] else (cell - y1)
    dx = 0 if (mode == 'ground' or rules['death']) else round(cell / 2 - (x0 + x1) / 2)
    if x0 + dx < 0 or x1 + dx > cell or y0 + dy < 0 or y1 + dy > cell: return None, (dx, dy)
    out = Image.new('RGBA', im.size, (0, 0, 0, 0)); out.paste(im, (dx, dy))
    return out, (dx, dy)


def check(sheet, cell, frames, palette, rules):
    W, H = sheet.size
    px = sheet.load()
    fails, metrics = [], []
    if W != cell * frames or H != cell:
        fails.append(f'dimensions: {W}x{H} != {cell * frames}x{cell}')
        return fails, metrics
    bad_alpha = bad_pal = 0
    boxes = []
    for f in range(frames):
        x0 = f * cell
        colors = set(); minx = miny = 10 ** 9; maxx = maxy = -1; edge = [0, 0, 0]
        for y in range(cell):
            for x in range(cell):
                r, g, b, a = px[x0 + x, y]
                if a not in (0, 255): bad_alpha += 1
                if a == 0: continue
                if a == 255:
                    colors.add((r, g, b))
                    if (r, g, b) not in palette: bad_pal += 1
                minx = min(minx, x); maxx = max(maxx, x); miny = min(miny, y); maxy = max(maxy, y)
                if y == 0: edge[0] += 1
                if x == 0: edge[1] += 1
                if x == cell - 1: edge[2] += 1
        if maxx < 0:
            fails.append(f'baseline: 프레임 {f} 비어 있음'); boxes.append(None); continue
        if len(colors) > MAX_COLORS: fails.append(f'palette: 프레임 {f} 색 수 {len(colors)} > {MAX_COLORS}')
        if sum(edge): fails.append(f'clip: 프레임 {f} 셀 테두리 침범 (상 {edge[0]} · 좌 {edge[1]} · 우 {edge[2]})')
        boxes.append((minx, miny, maxx, maxy))
        metrics.append({'frame': f, 'colors': len(colors), 'feet_row': maxy, 'height': maxy - miny + 1,
                        'center_x': (minx + maxx + 1) / 2})
    if bad_alpha: fails.append(f'alpha: 0/255 외 값 {bad_alpha}개')
    if bad_pal: fails.append(f'palette: 팔레트 밖 색 픽셀 {bad_pal}개')
    live = [m for m in metrics]
    if live and not rules['airborne']:
        ref = live[0]['feet_row']
        for m in live[1:]:
            if m['feet_row'] != ref: fails.append(f"baseline: 프레임 {m['frame']} 발 행 {m['feet_row']} != {ref}")
    if live and not rules['death']:
        mc = sum(m['center_x'] for m in live) / len(live)
        mh = sum(m['height'] for m in live) / len(live)
        for m in live:
            if not rules['shift'] and abs(m['center_x'] - mc) > DRIFT_TOL:
                fails.append(f"drift: 프레임 {m['frame']} 가로 중심 {m['center_x']:.1f} 이 평균 {mc:.1f} 에서 ±{DRIFT_TOL:g}px 초과")
            if not rules['squash'] and abs(m['height'] - mh) > DRIFT_TOL:
                fails.append(f"drift: 프레임 {m['frame']} 높이 {m['height']} 가 평균 {mh:.1f} 에서 ±{DRIFT_TOL:g}px 초과")
    return fails, metrics


def main():
    ap = argparse.ArgumentParser(description='ASF 프레임 → Px32 규격 시트 + CI 규칙 미리 검사')
    ap.add_argument('--frames', required=True, help='ASF 최종 프레임 폴더(재생 순서 = 파일 번호 순)')
    ap.add_argument('--name', required=True, help='시트 이름(확장자 없이), 예: Hero_Run')
    ap.add_argument('--expect-frames', type=int, required=True, help='Px32Rules 규격 프레임 수')
    ap.add_argument('--palette', required=True, help='팔레트 txt/hex (예: NEWIMAG/palette_v2.txt)')
    ap.add_argument('--out', required=True, help='출력 폴더 (예: NEWIMAG/<묶음>)')
    ap.add_argument('--cell', type=int, default=128)
    ap.add_argument('--source', help='GPT 원본 시트(출처 기록용)')
    ap.add_argument('--lock-file', help='asf.lock.json(출처 기록용)')
    ap.add_argument('--settings', help='ASF 실행 설정 JSON 파일(출처 기록용)')
    ap.add_argument('--place', choices=['asf', 'ground', 'ground-center'], default='ground-center',
                    help='최종 배치: asf=그대로 / ground=발을 맨 아래 행 / ground-center=+가로 bbox 중심을 셀 중앙(기본, 승인 시트·CI 기준)')
    a = ap.parse_args()

    files = frame_files(a.frames)
    if len(files) != a.expect_frames:
        print(f'error: 프레임 {len(files)}장 != 규격 {a.expect_frames}장 ({a.frames})'); print('RESULT=FAIL'); return 2
    ims = [Image.open(p).convert('RGBA') for p in files]
    for p, im in zip(files, ims):
        if im.size != (a.cell, a.cell):
            print(f'error: {os.path.basename(p)} 크기 {im.size} != {a.cell}x{a.cell}'); print('RESULT=FAIL'); return 2

    rules = rules_for(a.name)
    sheet = Image.new('RGBA', (a.cell * len(ims), a.cell), (0, 0, 0, 0))
    shifts, place_fails = [], []
    for i, im in enumerate(ims):
        placed, sh = place(im, a.cell, a.place, rules)
        shifts.append(list(sh))
        if placed is None:
            place_fails.append(f'clip: 프레임 {i} 를 {a.place} 배치하면 셀 밖으로 나감 (이동 {sh}) — 몸 크기·기준점 조정 또는 GPT 재요청')
            placed = im
        sheet.paste(placed, (i * a.cell, 0))

    fails, metrics = check(sheet, a.cell, a.expect_frames, load_palette(a.palette), rules)
    # 규격 대조: -Rows×-Cols(-Count) 가 규격과 다르면 시트는 CI dimensions 에서 떨어진다
    spec = sheet_spec(a.name)
    spec_fails = []
    if spec is None:
        print(f'WARN 규격표에 없는 이름: {a.name} — 프레임 수·셀을 대조하지 못함 (CI 는 셀=높이로 추정)')
    else:
        if a.expect_frames != spec[1]:
            spec_fails.append(f'dimensions: 규격 {a.name} = {spec[1]}프레임인데 {a.expect_frames}프레임 '
                              f'— GPT 가 칸 수를 다르게 그렸으면 재요청, -Rows/-Cols/-Count 를 잘못 줬으면 고쳐 다시 실행')
        if a.cell != spec[0]:
            spec_fails.append(f'dimensions: 규격 {a.name} 셀 = {spec[0]}px 인데 -Cell {a.cell} — -Cell {spec[0]} 으로 다시 실행')
    fails = spec_fails + place_fails + fails

    os.makedirs(a.out, exist_ok=True)
    sheet_path = os.path.join(a.out, a.name + '.png')
    sheet.save(sheet_path)
    sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(os.path.join(a.out, a.name + '_Preview_4x.png'))
    prov = {
        'sheet': os.path.basename(sheet_path), 'sha256': sha256(sheet_path), 'cell': a.cell, 'frames': a.expect_frames,
        'rules': rules, 'result': 'FAIL' if fails else 'PASS', 'fails': fails, 'metrics': metrics,
        'source': {'path': a.source, 'sha256': sha256(a.source)} if a.source else None,
        'asf': json.load(open(a.lock_file, encoding='utf-8')) if a.lock_file else None,
        'settings': json.load(open(a.settings, encoding='utf-8-sig')) if a.settings else None,
        'frame_files': [os.path.basename(p) for p in files],
        'place': a.place, 'place_shifts': shifts,
    }
    json.dump(prov, open(os.path.join(a.out, a.name + '.asf.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=2)

    for m in metrics:
        print(f"f{m['frame']}: 색 {m['colors']} · 발 행 {m['feet_row']} · 높이 {m['height']} · 가로 중심 {m['center_x']:.1f}")
    for f in fails: print('FAIL ' + f)
    print('SHEET=' + sheet_path)
    print('RESULT=' + ('FAIL' if fails else 'PASS'))
    return 1 if fails else 0


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')  # -I 는 PYTHONUTF8 을 무시하므로 직접
    sys.exit(main())
