// Image packaging: split generated sheets, fit pixel sprites, preserve Unity layouts.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sharp=require(process.env.NEWIMAG_SHARP),root=__dirname;
const project=path.resolve(root,'..','..'),oldRoot=path.join(project,'Assets/_Project/ResourcceEX');
const cfg=JSON.parse(fs.readFileSync(path.join(root,'extra-generation.json'),'utf8'));
const catalog={traps:[],effects:[],replacements:[],animations:[]};
const guid=()=>crypto.randomBytes(16).toString('hex');
const mkdir=p=>fs.mkdirSync(p,{recursive:true});
const rel=p=>path.relative(root,p).replaceAll('\\','/');
const list=(dir,ext)=>fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?list(path.join(dir,e.name),ext):e.name.endsWith(ext)?[path.join(dir,e.name)]:[]);
function singleMeta(file,ppu=256,pivot={x:.5,y:.5}){
 if(fs.existsSync(file+'.meta'))return;
 fs.writeFileSync(file+'.meta',`fileFormatVersion: 2\nguid: ${guid()}\nTextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n  textureSettings:\n    filterMode: 0\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  maxTextureSize: 4096\n  spriteMode: 1\n  spriteMeshType: 0\n  spritePivot: {x: ${pivot.x}, y: ${pivot.y}}\n  spritePixelsToUnits: ${ppu}\n  alphaUsage: 1\n  alphaIsTransparency: 1\n  textureType: 8\n  textureShape: 1\n  textureCompression: 0\n  platformSettings: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n`);
}
async function bounds(file){
 const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let x0=info.width,y0=info.height,x1=-1,y1=-1;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>16){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y)}
 if(x1<0)throw Error('Empty image '+file);return {left:x0,top:y0,width:x1-x0+1,height:y1-y0+1};
}
async function fit(file,w,h,box){
 const b=box||await bounds(file);
 const obj=await sharp(file).extract(b).resize({width:Math.max(1,Math.floor(w*.88)),height:Math.max(1,Math.floor(h*.88)),fit:'inside',kernel:'nearest'}).png().toBuffer();
 const m=await sharp(obj).metadata();
 return sharp({create:{width:w,height:h,channels:4,background:'#00000000'}}).composite([{input:obj,left:Math.floor((w-m.width)/2),top:h-Math.round(h*.06)-m.height}]).png().toBuffer();
}
function sprites(meta){
 const section=meta.split('  spriteSheet:')[1]||'';
 return [...section.matchAll(/    - serializedVersion: 2([\s\S]*?)(?=\r?\n    - serializedVersion: 2|\r?\n    outline:|$)/g)].map(m=>{
  const t=m[1],name=t.match(/\n      name: (.+)/)?.[1]?.trim();
  if(!name||!t.includes('      rect:'))return null;
  const get=k=>Number(t.match(new RegExp('\\n        '+k+': ([0-9.]+)'))[1]);
  const piv=t.match(/pivot: \{x: ([^,]+), y: ([^}]+)\}/);
  return {name,x:get('x'),y:get('y'),width:get('width'),height:get('height'),pivot:{x:+piv[1],y:+piv[2]},id:t.match(/internalID: (-?\d+)/)?.[1]};
 }).filter(Boolean);
}
(async()=>{
 // Modular traps: rows=mutation skins, columns=parts. These are static skins.
 for(const spec of cfg.traps){
  const dir=path.join(root,'Traps',spec.id);mkdir(dir);const src=path.join(dir,'source.png');fs.copyFileSync(spec.source,src);
  const m=await sharp(src).metadata(),parts=[];
  for(let r=0;r<4;r++)for(let c=0;c<4;c++){
   const y=Math.round(r*m.height/4),x=Math.round(c*m.width/4);
   const f=path.join(dir,spec.states[r],spec.parts[c]+'.png');mkdir(path.dirname(f));
   const crop=await sharp(src).extract({left:x,top:y,width:Math.round((c+1)*m.width/4)-x,height:Math.round((r+1)*m.height/4)-y}).png().toBuffer();
   await sharp(await fit(crop,384,384)).png().toFile(f);singleMeta(f);
   parts.push({state:spec.states[r],part:spec.parts[c],file:rel(f)});
  }
  catalog.traps.push({id:spec.id,parts});
 }
 // Character VFX: each row is a four-frame animation, no character baked in.
 const effects=cfg.effects,ed=path.join(root,'Effects');mkdir(ed);fs.copyFileSync(effects.source,path.join(ed,'source.png'));
 const em=await sharp(effects.source).metadata();
 for(let r=0;r<4;r++){
  const parts=[],state=effects.states[r],dir=path.join(ed,state);mkdir(dir);
  for(let c=0;c<4;c++){const x=Math.round(c*em.width/4),y=Math.round(r*em.height/4),f=path.join(dir,String(c).padStart(2,'0')+'.png');
   const b=await sharp(effects.source).extract({left:x,top:y,width:Math.round((c+1)*em.width/4)-x,height:Math.round((r+1)*em.height/4)-y}).png().toBuffer();
   await sharp(b).resize(256,256,{kernel:'nearest'}).png().toFile(f);singleMeta(f);parts.push(rel(f));
  }
  const strip=path.join(dir,'strip.png');await sharp({create:{width:1024,height:256,channels:4,background:'#00000000'}}).composite(parts.map((p,i)=>({input:path.join(root,p),left:i*256,top:0}))).png().toFile(strip);
  catalog.effects.push({state,fps:12,loop:false,frames:parts,strip:rel(strip)});
 }
 const map={Char_Idle:['hero','Idle'],Char_Run:['hero','Run'],Char_Jump_Start:['hero','Jump_Start'],Char_Jump_Loop:['hero','Jump_Loop'],Char_Fall_Loop:['hero','Jump_Loop'],Char_Land:['hero','Land'],Char_Dash:['hero','Dash'],Lord_Hit:['hero','Lord_Hit'],Lord_Death:['hero','Death']};
 for(const [prefix,id] of [['Enemy_Goblin_Raider','goblin'],['Enemy_Orc_Brute','orc'],['Enemy_Skeleton_Archer','skeleton'],['Enemy_Imp_Flyer','imp'],['Enemy_Elite_Knight','knight']])for(const s of ['Idle','Run','Death'])map[prefix+'_'+s]=[id,s];
 const clips=list(path.join(oldRoot,'Animations'),'.anim').map(file=>({file,text:fs.readFileSync(file,'utf8')})),guids={};
 for(const file of list(path.join(oldRoot,'Sprites/Animation'),'.png')){
  const base=path.basename(file,'.png'),[id,state]=map[base]||[];if(!id)throw Error('Unmapped '+file);
  const oldMeta=fs.readFileSync(file+'.meta','utf8'),oldGuid=oldMeta.match(/^guid: (\w+)/m)[1];
  const rects=sprites(oldMeta),m=await sharp(file).metadata();if(!rects.length)throw Error('No rects '+file);
  const frames=Array.from({length:4},(_,i)=>path.join(root,id,state+'_'+String(i).padStart(2,'0')+'.png'));
  const bbs=await Promise.all(frames.map(bounds)),x0=Math.min(...bbs.map(b=>b.left)),y0=Math.min(...bbs.map(b=>b.top));
  const box={left:x0,top:y0,width:Math.max(...bbs.map(b=>b.left+b.width))-x0,height:Math.max(...bbs.map(b=>b.top+b.height))-y0};
  const clip=clips.find(c=>c.text.split('    attribute: m_Sprite')[0].includes('guid: '+oldGuid));
  const keys=clip?[...clip.text.split('    attribute: m_Sprite')[0].matchAll(/- time: ([0-9.]+)\r?\n\s+value: \{fileID: (-?\d+), guid: (\w+), type: 3\}/g)].filter(k=>k[3]===oldGuid):[];
  const sequence={};keys.forEach((k,j)=>sequence[k[2]]=state==='Run'?j%4:Math.min(3,Math.floor(j*4/keys.length)));
  const dest=path.join(root,'Replacement',path.relative(oldRoot,file));mkdir(path.dirname(dest));
  const fd=path.join(root,'Replacement/Frames',base);mkdir(fd);const composites=[],split=[];
  for(let i=0;i<rects.length;i++){
   const rect=rects[i],idx=sequence[rect.id]??(state==='Run'?i%4:Math.min(3,Math.floor(i*4/rects.length)));
   const buf=await fit(frames[idx],rect.width,rect.height,box),f=path.join(fd,String(i).padStart(2,'0')+'.png');
   await sharp(buf).png().toFile(f);singleMeta(f,+oldMeta.match(/spritePixelsToUnits: ([\d.]+)/)[1],rect.pivot);
   composites.push({input:buf,left:rect.x,top:m.height-rect.y-rect.height});split.push({file:rel(f),name:rect.name,sourceFrame:idx,pivot:rect.pivot});
  }
  await sharp({create:{width:m.width,height:m.height,channels:4,background:'#00000000'}}).composite(composites).png().toFile(dest);
  const ng=fs.existsSync(dest+'.meta')?fs.readFileSync(dest+'.meta','utf8').match(/^guid: (\w+)/m)[1]:guid();guids[oldGuid]=ng;
  fs.writeFileSync(dest+'.meta',oldMeta.replace(/^guid: \w+/m,'guid: '+ng).replace(/spriteID: \w+/g,()=> 'spriteID: '+guid()));
  catalog.replacements.push({original:path.relative(project,file).replaceAll('\\','/'),replacement:rel(dest),width:m.width,height:m.height,rectCount:rects.length,sourceUniqueFrames:4,frames:split,keyframeCount:keys.length,clip:clip?path.relative(project,clip.file).replaceAll('\\','/'):null});
 }
 const trapMap={SpikeTrap_Visual:'Traps/spike/Normal/spikes.png',ArrowShooter_Visual:'Traps/arrow_shooter/Normal/body.png',Arrow_Visual:'Traps/arrow_shooter/Normal/arrow.png',DropHammer_Visual:'Traps/drop_hammer/Normal/head.png'};
 for(const [name,src] of Object.entries(trapMap)){
  const old=path.join(oldRoot,'Sprites/Traps',name+'.png'),dest=path.join(root,'Replacement/Sprites/Traps',name+'.png');mkdir(path.dirname(dest));
  const m=await sharp(old).metadata();await sharp(await fit(path.join(root,src),m.width,m.height)).png().toFile(dest);
  const t=fs.readFileSync(old+'.meta','utf8');const ng=fs.existsSync(dest+'.meta')?fs.readFileSync(dest+'.meta','utf8').match(/^guid: (\w+)/m)[1]:guid();
  fs.writeFileSync(dest+'.meta',t.replace(/^guid: \w+/m,'guid: '+ng).replace(/spriteID: \w+/g,()=> 'spriteID: '+guid()));
  catalog.replacements.push({original:path.relative(project,old).replaceAll('\\','/'),replacement:rel(dest),width:m.width,height:m.height,component:src});
 }
 for(const c of clips){const dest=path.join(root,'Replacement',path.relative(oldRoot,c.file));mkdir(path.dirname(dest));
  const t=c.text.replace(/guid: (\w+)/g,(s,g)=>guids[g]?'guid: '+guids[g]:s);fs.writeFileSync(dest,t);
  if(!fs.existsSync(dest+'.meta'))fs.writeFileSync(dest+'.meta',`fileFormatVersion: 2\nguid: ${guid()}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 7400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n`);
  catalog.animations.push({original:path.relative(project,c.file).replaceAll('\\','/'),replacement:rel(dest),keyframes:c.text.split('    attribute: m_Sprite')[0].match(/- time:/g)?.length||0});
 }
 for(const r of catalog.replacements){const m=await sharp(path.join(root,r.replacement)).metadata();if(m.width!==r.width||m.height!==r.height||!m.hasAlpha)throw Error('Compatibility failed: '+r.replacement)}
 fs.writeFileSync(path.join(root,'replacement-manifest.json'),JSON.stringify(catalog,null,2));
 fs.writeFileSync(path.join(root,'replacement-manifest.js'),'const REPLACEMENTS = '+JSON.stringify(catalog)+';');
 console.log('Verified:',catalog.replacements.length,'replacement images;',catalog.animations.length,'compatible clips;',catalog.traps.reduce((n,t)=>n+t.parts.length,0),'trap parts;',catalog.effects.length*4,'VFX frames.');
})().catch(e=>{console.error(e);process.exit(1)});
