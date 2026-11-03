#!/usr/bin/env node
// ═══════════════════════════════════════════════════════════════
//  check-sheet.mjs — GPT 가 그린 이펙트 시트 규격 검사 + 검수용 비교 미리보기 (Unity 불필요)
// ═══════════════════════════════════════════════════════════════
// PixelArtSheetValidator(CI sprites32)의 이펙트 규칙을 Unity 없이 미리 본다. 최종 판정은 CI.
//   크기(가로 = 셀 × 프레임, 정사각 셀) · 알파 0/255 · 팔레트 밖 색 · 프레임당 16색 · 빈 프레임 · 셀 상·좌·우 1px 테두리
//   + (참고) 1픽셀 고립점 수, 프레임 간 중심 이동
//
//   node tools/vfx/check-sheet.mjs <sheet.png> <palette.txt> [--frames N] [--preview out.png --hero hero.png [--old old.png]]
//
// --preview: 주인공 첫 프레임 | (기존 시트) | 새 시트 를 같은 3배로 나란히(어두운 배경) — 실제 게임 속 크기 비교용
// 출력: 항목별 PASS/FAIL 줄 + 마지막 줄 RESULT=PASS|FAIL, exit 0/1

import { readFileSync, writeFileSync } from "node:fs";
import { decodePNG, encodePNG, loadPalette } from "./png.mjs";

const MAX_COLORS = 16;
const args = process.argv.slice(2), flag = (k) => { const i = args.indexOf("--" + k); return i < 0 ? null : args[i + 1]; };
const [sheetPath, palPath] = args;
if (!palPath) { console.error("usage: check-sheet.mjs <sheet.png> <palette.txt> [--frames N] [--preview out.png --hero hero.png [--old old.png]]"); process.exit(1); }

const img = decodePNG(readFileSync(sheetPath));
const pal = new Set(loadPalette(palPath).map((p) => p.join(",")));
const cell = img.h, n = flag("frames") ? +flag("frames") : img.w / cell;
const fails = [], notes = [];

if (img.w !== cell * n || !Number.isInteger(n)) fails.push(`dimensions: ${img.w}×${img.h} 가 정사각 셀 ${cell}px × ${n}프레임이 아님`);
let badAlpha = 0, badPal = 0, lone = 0;
const centers = [];
for (let f = 0; f < n; f++) {
  const colors = new Set();
  let cnt = 0, edge = 0, sx = 0, sy = 0;
  const at = (x, y) => x >= 0 && y >= 0 && x < cell && y < cell && img.px[(y * img.w + f * cell + x) * 4 + 3] === 255;
  for (let y = 0; y < cell; y++) for (let x = 0; x < cell; x++) {
    const i = (y * img.w + f * cell + x) * 4, a = img.px[i + 3];
    if (a !== 0 && a !== 255) badAlpha++;
    if (a !== 255) continue;
    const key = `${img.px[i]},${img.px[i + 1]},${img.px[i + 2]}`;
    colors.add(key); if (!pal.has(key)) badPal++;
    cnt++; sx += x; sy += y;
    if (y === 0 || x === 0 || x === cell - 1) edge++;
    let nb = 0; for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) if ((dx || dy) && at(x + dx, y + dy)) nb++;
    if (!nb) lone++;
  }
  if (!cnt) fails.push(`baseline: 프레임 ${f} 비어 있음`);
  if (colors.size > MAX_COLORS) fails.push(`palette: 프레임 ${f} 색 수 ${colors.size} > ${MAX_COLORS}`);
  if (edge) fails.push(`clip: 프레임 ${f} 셀 테두리 침범 ${edge}px`);
  if (cnt) centers.push([sx / cnt, sy / cnt]);
  notes.push(`f${f}: 픽셀 ${cnt} · 색 ${colors.size}`);
}
if (badAlpha) fails.push(`alpha: 0/255 외 값 ${badAlpha}개`);
if (badPal) fails.push(`palette: 팔레트 밖 색 픽셀 ${badPal}개`);
if (lone) notes.push(`참고: 1픽셀 고립점 ${lone}개 (파편은 2픽셀 이상 권장)`);
if (centers.length > 1) {
  const d = Math.max(...centers.map(([x, y]) => Math.hypot(x - cell / 2, y - cell / 2)));
  notes.push(`참고: 프레임 무게중심의 셀 중앙 대비 최대 거리 ${d.toFixed(1)}px`);
}

// ── 비교 미리보기 ──
if (flag("preview") && flag("hero")) {
  const K = 3, hero = decodePNG(readFileSync(flag("hero"))), old = flag("old") ? decodePNG(readFileSync(flag("old"))) : null;
  const heroCell = hero.h, gap = 16;
  const W = (heroCell + gap + (old ? old.w + gap : 0) + img.w) * K, H = Math.max(heroCell, cell, old?.h ?? 0) * K;
  const out = new Uint8Array(W * H * 4);
  for (let i = 0; i < W * H; i++) out.set([42, 40, 45, 255], i * 4);
  const blit = (S, sw, sh, dx) => {
    const dy = Math.floor((H / K - sh) / 2);
    for (let y = 0; y < sh; y++) for (let x = 0; x < sw; x++) {
      const i = (y * S.w + x) * 4; if (S.px[i + 3] < 128) continue;
      for (let a = 0; a < K; a++) for (let b = 0; b < K; b++) out.set(S.px.subarray(i, i + 4), (((dy + y) * K + a) * W + (dx + x) * K + b) * 4);
    }
  };
  let x = 0;
  blit(hero, heroCell, heroCell, x); x += heroCell + gap;
  if (old) { blit(old, old.w, old.h, x); x += old.w + gap; }
  blit(img, img.w, img.h, x);
  writeFileSync(flag("preview"), encodePNG(W, H, out));
  notes.push(`미리보기: ${flag("preview")}`);
}

for (const l of notes) console.log(l);
for (const l of fails) console.log("FAIL " + l);
console.log(`RESULT=${fails.length ? "FAIL" : "PASS"}  (${cell}px × ${n}프레임)`);
process.exit(fails.length ? 1 : 0);
