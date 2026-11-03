#!/usr/bin/env node
// ═══════════════════════════════════════════════════════════════
//  pixelfx-convert.mjs — pixelfx 격자 시트 → 화풍 v2(Px32) 이펙트 시트
// ═══════════════════════════════════════════════════════════════
// Unity 없이 돈다(자동화 worktree 용). 규칙은 ArtInboxImporter.ImportPx32 · PixelArtSheetValidator 와 같다:
//   알파 0/255(임계 128) → 팔레트 최근접(RGB 제곱거리) → 프레임당 16색 초과분은 남은 색 중 최근접으로
//   → 끝의 빈 프레임 제거 → 전 프레임 합집합 + 여백 1px 만 남기고 pixelfx 원점이 셀 중앙에 오는 정사각(짝수) 셀 가로 1줄
//   (Px32ImportPostprocessor 가 셀=높이 슬라이스 · 중앙 피벗을 자동 적용)
//
//   node tools/vfx/pixelfx-convert.mjs <raw.json> <raw.png> <palette.txt> <out.png> <out.json>
//
// 출력 마지막 줄: CELL=<px> FRAMES=<n> FPS=<f> LOOP=<bool> COLORS=<최대 프레임 색 수>

import { readFileSync, writeFileSync } from "node:fs";
import { decodePNG, encodePNG, loadPalette } from "./png.mjs";

const MAX_COLORS = 16; // PixelArtSheetValidator: Effects 카테고리 프레임당 색 상한

const [jsonPath, pngPath, palPath, outPng, outJson] = process.argv.slice(2);
if (!outJson) { console.error("usage: pixelfx-convert.mjs <raw.json> <raw.png> <palette.txt> <out.png> <out.json>"); process.exit(1); }

const palette = loadPalette(palPath);
if (!palette.length) throw new Error("팔레트 없음: " + palPath);
const nearest = (r, g, b, set) => { let best = 0, bd = Infinity; set.forEach((p, i) => { const d = (r - p[0]) ** 2 + (g - p[1]) ** 2 + (b - p[2]) ** 2; if (d < bd) { bd = d; best = i; } }); return best; };

// ── 변환 ──
const sheet = JSON.parse(readFileSync(jsonPath, "utf8")), meta = sheet.meta?.pixelfx;
if (!meta?.pivot || !sheet.frames?.length) throw new Error("JSON 형식 오류(frames · meta.pixelfx.pivot)");
const img = decodePNG(readFileSync(pngPath));
const fw = sheet.frames[0].frame.w, fh = sheet.frames[0].frame.h;

// 프레임별 팔레트 인덱스(-1 = 투명) + 합집합 영역
let minX = Infinity, minY = Infinity, maxX = -1, maxY = -1, worst = 0;
const frames = sheet.frames.map(({ frame: r }) => {
  const idx = new Int16Array(fw * fh).fill(-1), count = new Map();
  for (let y = 0; y < fh; y++) for (let x = 0; x < fw; x++) {
    const o = ((r.y + y) * img.w + r.x + x) * 4;
    if (img.px[o + 3] < 128) continue;
    const i = nearest(img.px[o], img.px[o + 1], img.px[o + 2], palette);
    idx[y * fw + x] = i; count.set(i, (count.get(i) ?? 0) + 1);
    minX = Math.min(minX, x); maxX = Math.max(maxX, x); minY = Math.min(minY, y); maxY = Math.max(maxY, y);
  }
  if (count.size > MAX_COLORS) {
    const keep = [...count.entries()].sort((a, b) => b[1] - a[1]).slice(0, MAX_COLORS).map(([i]) => i), kept = new Set(keep);
    const remap = new Map([...count.keys()].filter((i) => !kept.has(i)).map((i) => [i, keep[nearest(...palette[i], keep.map((k) => palette[k]))]]));
    for (let k = 0; k < idx.length; k++) if (remap.has(idx[k])) idx[k] = remap.get(idx[k]);
  }
  worst = Math.max(worst, Math.min(count.size, MAX_COLORS));
  return idx;
});
if (maxX < 0) throw new Error("보이는 픽셀이 없음(알파 128 미만뿐)");

// 끝의 빈 프레임(알파 임계로 사라진 꼬리)은 버린다 — Validator baseline 은 빈 프레임을 FAIL 로 본다
while (frames.length > 1 && frames[frames.length - 1].every((c) => c < 0)) frames.pop();
const n = frames.length;

// 셀 상·좌·우 1px 테두리는 비운다(Validator clip 규칙) → 여백 1
const ox = Math.round(meta.pivot[0]), oy = Math.round(meta.pivot[1]);
const half = Math.max(ox - minX, maxX + 1 - ox, oy - minY, maxY + 1 - oy) + 1, cell = half * 2, OW = cell * n;
const out = new Uint8Array(OW * cell * 4);
frames.forEach((idx, i) => {
  for (let y = minY; y <= maxY; y++) for (let x = minX; x <= maxX; x++) {
    const c = idx[y * fw + x]; if (c < 0) continue;
    const o = ((y - oy + half) * OW + i * cell + x - ox + half) * 4, p = palette[c];
    out[o] = p[0]; out[o + 1] = p[1]; out[o + 2] = p[2]; out[o + 3] = 255;
  }
});

writeFileSync(outPng, encodePNG(OW, cell, out));
writeFileSync(outJson, JSON.stringify({ effect: meta.id, cell, frames: n, fps: meta.fps, loop: !!meta.loop, maxColors: worst }, null, 1));
console.log(`CELL=${cell} FRAMES=${n} FPS=${meta.fps} LOOP=${!!meta.loop} COLORS=${worst}`);
