// ═══════════════════════════════════════════════════════════════
//  png.mjs — tools/vfx 공용: 8bit RGBA PNG 읽기·쓰기 + 팔레트 읽기 (의존성 없음)
// ═══════════════════════════════════════════════════════════════

import { readFileSync } from "node:fs";
import { inflateSync, deflateSync } from "node:zlib";

/** 8bit RGBA 비인터레이스 PNG → { w, h, px(Uint8Array RGBA, 위→아래) } */
export function decodePNG(buf) {
  let p = 8, w = 0, h = 0, idat = [];
  while (p < buf.length) {
    const len = buf.readUInt32BE(p), type = buf.toString("ascii", p + 4, p + 8), data = buf.subarray(p + 8, p + 8 + len);
    if (type === "IHDR") {
      w = data.readUInt32BE(0); h = data.readUInt32BE(4);
      if (data[8] !== 8 || data[9] !== 6 || data[12] !== 0) throw new Error("8bit RGBA 비인터레이스 PNG 만 지원");
    } else if (type === "IDAT") idat.push(data);
    p += 12 + len;
  }
  const raw = inflateSync(Buffer.concat(idat)), out = new Uint8Array(w * h * 4), stride = w * 4;
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)], src = y * (stride + 1) + 1, row = y * stride;
    for (let x = 0; x < stride; x++) {
      const a = x >= 4 ? out[row + x - 4] : 0, b = y ? out[row - stride + x] : 0, c = x >= 4 && y ? out[row - stride + x - 4] : 0;
      let v = raw[src + x];
      if (f === 1) v += a; else if (f === 2) v += b; else if (f === 3) v += (a + b) >> 1;
      else if (f === 4) { const pp = a + b - c, pa = Math.abs(pp - a), pb = Math.abs(pp - b), pc = Math.abs(pp - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
      out[row + x] = v & 255;
    }
  }
  return { w, h, px: out };
}

const CRC = new Int32Array(256).map((_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c; });
const crc32 = (b) => { let c = -1; for (const x of b) c = CRC[(c ^ x) & 255] ^ (c >>> 8); return (c ^ -1) >>> 0; };
function chunk(type, data) {
  const len = Buffer.alloc(4), crc = Buffer.alloc(4), td = Buffer.concat([Buffer.from(type, "ascii"), data]);
  len.writeUInt32BE(data.length); crc.writeUInt32BE(crc32(td));
  return Buffer.concat([len, td, crc]);
}
export function encodePNG(w, h, px) {
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr.set([8, 6, 0, 0, 0], 8);
  const raw = Buffer.alloc(h * (w * 4 + 1));
  for (let y = 0; y < h; y++) raw.set(px.subarray(y * w * 4, (y + 1) * w * 4), y * (w * 4 + 1) + 1);
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk("IHDR", ihdr), chunk("IDAT", deflateSync(raw)), chunk("IEND", Buffer.alloc(0))]);
}

/** Px32Rules.ReadPaletteFile 와 같은 규칙: 정확히 #RRGGBB 인 줄만, 중복 제거 → [[r,g,b], ...] */
export function loadPalette(path) {
  return [...new Set(readFileSync(path, "utf8").split(/\r?\n/).map((l) => l.trim()).filter((l) => /^#[0-9A-Fa-f]{6}$/.test(l)).map((l) => l.toUpperCase()))]
    .map((l) => parseInt(l.slice(1), 16)).map((v) => [v >> 16, (v >> 8) & 255, v & 255]);
}
