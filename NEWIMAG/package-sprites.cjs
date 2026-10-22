// Packaging only: crop frames and add transparent padding; keep artwork pixels.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sharp=require(process.env.NEWIMAG_SHARP),root=__dirname,S=384;
const specs=JSON.parse(fs.readFileSync(path.join(root,'generation.json'),'utf8'));
const manifest={frameSize:S,pixelsPerUnit:256,facing:'right',characters:[]};
function meta(f){fs.writeFileSync(f+'.meta',`fileFormatVersion: 2\nguid: ${crypto.randomBytes(16).toString('hex')}\nTextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n  textureSettings:\n    filterMode: 0\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  maxTextureSize: 4096\n  spriteMode: 1\n  spriteMeshType: 0\n  spritePivot: {x: 0.5, y: 0.125}\n  spritePixelsToUnits: 256\n  alphaUsage: 1\n  alphaIsTransparency: 1\n  textureType: 8\n  textureShape: 1\n  textureCompression: 0\n  platformSettings: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n`)}
(async()=>{for(const spec of specs){
 const dir=path.join(root,spec.id);fs.mkdirSync(dir,{recursive:true});
 const src=path.join(dir,'source.png');fs.copyFileSync(spec.source,src);
 const {data,info}=await sharp(src).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const ys=spec.id==='hero'?[0,164,326,489,664,824,960,1110,info.height]:Array.from({length:5},(_,i)=>Math.round(i*info.height/4));
 const animations=[],sheetParts=[];
 for(let r=0;r<spec.states.length;r++){
  const xs=[0];
  // Locate transparent gutters near each nominal column boundary.
  for(let col=1;col<4;col++){const expected=Math.round(col*info.width/4);let best=expected,score=Infinity;
   for(let x=expected-70;x<=expected+70;x++){let count=0;for(let y=ys[r];y<ys[r+1];y++)if(data[(y*info.width+x)*4+3]>0)count++;const s=count*1000+Math.abs(x-expected);if(s<score){score=s;best=x}}
   xs.push(best);
  }xs.push(info.width);
  const state=spec.states[r],frames=[];let bottom=0;
  for(let y=ys[r];y<ys[r+1];y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>16)bottom=Math.max(bottom,y-ys[r]);
  for(let c=0;c<4;c++){
   const crop=await sharp(src).extract({left:xs[c],top:ys[r],width:xs[c+1]-xs[c],height:ys[r+1]-ys[r]}).png().toBuffer();
   const name=`${state}_${c.toString().padStart(2,'0')}.png`,file=path.join(dir,name);
   await sharp({create:{width:S,height:S,channels:4,background:'#00000000'}}).composite([{input:crop,left:Math.floor((S-(xs[c+1]-xs[c]))/2),top:Math.min(S-(ys[r+1]-ys[r]),S-49-bottom)}]).png().toFile(file);
   meta(file);frames.push(`${spec.id}/${name}`);sheetParts.push({input:file,left:c*S,top:r*S});
  }
  const strip=`${spec.id}/${state}_strip.png`;
  await sharp({create:{width:S*4,height:S,channels:4,background:'#00000000'}}).composite(frames.map((f,c)=>({input:path.join(root,f),left:c*S,top:0}))).png().toFile(path.join(root,strip));
  animations.push({state,fps:['Run','Dash'].includes(state)?10:state==='Idle'?5:8,loop:['Idle','Run','Jump_Loop'].includes(state),frames,strip});
 }
 const sheet=`${spec.id}/spritesheet.png`;
 await sharp({create:{width:S*4,height:S*spec.states.length,channels:4,background:'#00000000'}}).composite(sheetParts).png().toFile(path.join(root,sheet));
 manifest.characters.push({id:spec.id,name:spec.name,sheet,columns:4,rows:spec.states.length,animations});
}
fs.writeFileSync(path.join(root,'manifest.json'),JSON.stringify(manifest,null,2));
fs.writeFileSync(path.join(root,'manifest.js'),'const SPRITES = '+JSON.stringify(manifest)+';');
await sharp({create:{width:S*3,height:S*2,channels:4,background:'#202333'}}).composite(manifest.characters.map((c,i)=>({input:path.join(root,c.animations[0].frames[0]),left:(i%3)*S,top:Math.floor(i/3)*S}))).png().toFile(path.join(root,'overview.png'));
let checked=0;
for(const c of manifest.characters)for(const a of c.animations)for(const f of a.frames){const p=path.join(root,f);const m=await sharp(p).metadata();if(m.width!==S||m.height!==S||!m.hasAlpha)throw Error('Invalid frame '+f);const stats=await sharp(p).stats();if(stats.channels[3].min!==0||stats.channels[3].max===0)throw Error('Invalid alpha '+f);checked++;}
console.log('Verified dimensions and transparency:',checked,'frames');
console.log('Packaged',manifest.characters.length,'characters;',manifest.characters.reduce((n,c)=>n+c.animations.length*4,0),'frames');
})().catch(e=>{console.error(e);process.exit(1)});
