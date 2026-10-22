// Packaging only: normalize generated atlases and split into existing 42px slots.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require(process.env.NEWIMAG_SHARP);
const root=__dirname,project=path.resolve(root,'../..'),environment=path.join(project,'Assets/_Project/ResourcceEX/Sprites/Environment');
const cfg=JSON.parse(fs.readFileSync(path.join(root,'generation.json'),'utf8'));
const originals=JSON.parse(fs.readFileSync(path.join(root,'original-inventory.json'),'utf8'));
const manifest={tileSize:42,columns:12,rows:11,mode:'generated redraw; source grid retained',sets:[],files:[]};
const guid=()=>crypto.randomBytes(16).toString('hex'),mkdir=p=>fs.mkdirSync(p,{recursive:true}),rel=p=>path.relative(root,p).replaceAll('\\','/');
function cloneMeta(old,dest){
 const t=fs.readFileSync(old+'.meta','utf8');
 const g=fs.existsSync(dest+'.meta')?fs.readFileSync(dest+'.meta','utf8').match(/^guid: (\w+)/m)[1]:guid();
 fs.writeFileSync(dest+'.meta',t.replace(/^guid: \w+/m,'guid: '+g).replace(/spriteID: \w+/g,()=> 'spriteID: '+guid()));
 return {ppu:+t.match(/spritePixelsToUnits: ([\d.]+)/)[1],pivot:t.match(/spritePivot: (\{.+\})/)[1]};
}
function atlasMeta(file,names){
 const g=fs.existsSync(file+'.meta')?fs.readFileSync(file+'.meta','utf8').match(/^guid: (\w+)/m)[1]:guid();
 const ids=names.map((_,i)=>21300000+i+1);
 const table=names.map((n,i)=>`  - first:\n      213: ${ids[i]}\n    second: ${n}`).join('\n');
 const sprites=names.map((n,i)=>`    - serializedVersion: 2\n      name: ${n}\n      rect:\n        serializedVersion: 2\n        x: ${(i%12)*42}\n        y: ${462-(Math.floor(i/12)+1)*42}\n        width: 42\n        height: 42\n      alignment: 0\n      pivot: {x: 0.5, y: 0.5}\n      border: {x: 0, y: 0, z: 0, w: 0}\n      customData:\n      outline: []\n      physicsShape: []\n      tessellationDetail: 0\n      bones: []\n      spriteID: ${guid()}\n      internalID: ${ids[i]}\n      vertices: []\n      indices:\n      edges: []\n      weights: []`).join('\n');
 fs.writeFileSync(file+'.meta',`fileFormatVersion: 2\nguid: ${g}\nTextureImporter:\n  internalIDToNameTable:\n${table}\n  externalObjects: {}\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n  textureSettings:\n    filterMode: 0\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  maxTextureSize: 2048\n  spriteMode: 2\n  spriteExtrude: 1\n  spriteMeshType: 0\n  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}\n  spritePixelsToUnits: 100\n  alphaUsage: 1\n  alphaIsTransparency: 1\n  textureType: 8\n  textureShape: 1\n  textureCompression: 0\n  platformSettings: []\n  spriteSheet:\n    serializedVersion: 2\n    sprites:\n${sprites}\n    outline: []\n    customData:\n    physicsShape: []\n    bones: []\n    spriteID: ${guid()}\n    internalID: 0\n    vertices: []\n    indices:\n    edges: []\n    weights: []\n    secondaryTextures: []\n    nameFileIdTable:\n${names.map((n,i)=>`      ${n}: ${ids[i]}`).join('\n')}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n`);
}
(async()=>{
mkdir(path.join(root,'Sources'));mkdir(path.join(root,'Atlases'));
for(const spec of cfg.sets){
 const original=originals.find(o=>o.phase===spec.phase&&o.chapter===spec.chapter),key=spec.phase+'_'+spec.chapter;
 const source=path.join(root,'Sources',key+'.png');fs.copyFileSync(spec.source,source);
 const atlas=path.join(root,'Atlases',key+'.png');
 // Original atlas cells are opaque. Export RGB intact with an opaque alpha channel.
 await sharp(source).resize(original.atlasWidth,original.atlasHeight,{fit:'fill',kernel:'nearest'}).removeAlpha().ensureAlpha().png().toFile(atlas);
 const names=original.cells.map(c=>path.basename(c.name,'.png'));atlasMeta(atlas,names);
 for(const cell of original.cells){
  const old=path.join(environment,spec.phase,spec.chapter,cell.name),dest=path.join(root,'Replacement/Environment',spec.phase,spec.chapter,cell.name);mkdir(path.dirname(dest));
  await sharp(atlas).extract({left:cell.col*42,top:cell.row*42,width:42,height:42}).png().toFile(dest);
  const settings=cloneMeta(old,dest),m=await sharp(dest).metadata();if(m.width!==42||m.height!==42||!m.hasAlpha)throw Error('Invalid tile: '+dest);
  manifest.files.push({original:path.relative(project,old).replaceAll('\\','/'),replacement:rel(dest),row:cell.row+1,col:cell.col+1,width:42,height:42,...settings});
 }
 const view=path.join(root,'Atlases',key+'_3x.png');await sharp(atlas).resize(1512,1386,{kernel:'nearest'}).png().toFile(view);
 manifest.sets.push({phase:spec.phase,chapter:spec.chapter,count:132,atlas:rel(atlas),source:rel(source),original:rel(original.reference),preview:rel(view),columns:12,rows:11});
}
const old=path.join(environment,'CorruptedCastleTile.png'),dest=path.join(root,'Replacement/Environment/CorruptedCastleTile.png');mkdir(path.dirname(dest));const m=await sharp(old).metadata();
fs.copyFileSync(cfg.legacy.source,path.join(root,'Sources/CorruptedCastleTile.png'));
await sharp(cfg.legacy.source).resize(m.width,m.height,{kernel:'nearest'}).png().toFile(dest);const settings=cloneMeta(old,dest);
manifest.files.push({original:path.relative(project,old).replaceAll('\\','/'),replacement:rel(dest),width:m.width,height:m.height,...settings});
// Verify that every split tile reconstructs the normalized atlas exactly.
for(const s of manifest.sets){const parts=manifest.files.filter(f=>f.original.includes('/'+s.phase+'/'+s.chapter+'/')).map(f=>({input:path.join(root,f.replacement),left:(f.col-1)*42,top:(f.row-1)*42}));
 const rebuilt=await sharp({create:{width:504,height:462,channels:4,background:'#00000000'}}).composite(parts).raw().toBuffer();const expected=await sharp(path.join(root,s.atlas)).ensureAlpha().raw().toBuffer();if(!rebuilt.equals(expected))throw Error('Reconstruction mismatch: '+s.atlas);
}
fs.writeFileSync(path.join(root,'manifest.json'),JSON.stringify(manifest,null,2));fs.writeFileSync(path.join(root,'manifest.js'),'const TILE_PACK = '+JSON.stringify(manifest)+';');
console.log('Verified',manifest.files.length,'replacement PNGs, four132-cell atlases,42x42 cuts and lossless atlas reconstruction.');
})().catch(e=>{console.error(e);process.exit(1)});
