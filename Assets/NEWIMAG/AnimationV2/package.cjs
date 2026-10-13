// Split image_gen sheets into uniform PNG cells. Never redraw source artwork.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require(process.env.NEWIMAG_SHARP);
const root=__dirname,S=384,defs=JSON.parse(fs.readFileSync(path.join(root,'generation.json'),'utf8'));
const manifest={version:2,frameSize:S,pixelsPerUnit:256,facing:'right',characters:[]};
const timing={Idle:6,Run:12,Jump_Start:12,Jump_Loop:10,Land:12,Dash:16,Hit:12,Death:10};
async function mainBounds(buf){const {data,info}=await sharp(buf).ensureAlpha().raw().toBuffer({resolveWithObject:true}),w=info.width,h=info.height,seen=new Uint8Array(w*h);let best=null;
 for(let start=0;start<w*h;start++){if(seen[start]||data[start*4+3]===0)continue;const stack=[start];seen[start]=1;let count=0,x0=w,y0=h,x1=0,y1=0;
  while(stack.length){const p=stack.pop(),x=p%w,y=Math.floor(p/w);count++;x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;const q=ny*w+nx;if(!seen[q]&&data[q*4+3]>0){seen[q]=1;stack.push(q)}}}
  if(!best||count>best.count)best={left:x0,top:y0,width:x1-x0+1,height:y1-y0+1,count};
 }if(!best)throw Error('Empty input cell');delete best.count;return best;
}
function meta(f){if(fs.existsSync(f+'.meta'))return;fs.writeFileSync(f+'.meta',`fileFormatVersion: 2\nguid: ${crypto.randomBytes(16).toString('hex')}\nTextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n  textureSettings:\n    filterMode: 0\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  maxTextureSize: 4096\n  spriteMode: 1\n  spriteMeshType: 0\n  spritePivot: {x: 0.5, y: 0.125}\n  spritePixelsToUnits: 256\n  alphaUsage: 1\n  alphaIsTransparency: 1\n  textureType: 8\n  textureShape: 1\n  textureCompression: 0\n  platformSettings: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n`)}
(async()=>{
for(const d of defs){
 const dir=path.join(root,d.id);fs.mkdirSync(dir,{recursive:true});const source=path.join(dir,'source.png');fs.copyFileSync(d.source,source);
 const {data,info}=await sharp(source).ensureAlpha().raw().toBuffer({resolveWithObject:true}),rows=d.states.length;
 const rowCuts=[0];
 for(let r=1;r<rows;r++){const expected=Math.round(r*info.height/rows),range=Math.floor(info.height/rows*.35);let best=expected,score=Infinity;
  for(let y=expected-range;y<=expected+range;y++){let count=0;for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>0)count++;const val=count*10000+Math.abs(y-expected);if(val<score){best=y;score=val}}rowCuts.push(best);
 }rowCuts.push(info.height);
 const animations=[],sheet=[];
 for(let r=0;r<rows;r++){
  const y=rowCuts[r],h=rowCuts[r+1]-y,cuts=[0];
  for(let c=1;c<8;c++){const expected=Math.round(c*info.width/8),range=Math.floor(info.width/8*.2);let best=expected,score=Infinity;
   for(let x=expected-range;x<=expected+range;x++){let count=0;for(let yy=y;yy<y+h;yy++)if(data[(yy*info.width+x)*4+3]>0)count++;const val=count*10000+Math.abs(x-expected);if(val<score){score=val;best=x}}cuts.push(best);
  }cuts.push(info.width);
  let bottom=0;for(let yy=y;yy<y+h;yy++)for(let x=0;x<info.width;x++)if(data[(yy*info.width+x)*4+3]>16)bottom=Math.max(bottom,yy-y);
  const state=d.states[r],frames=[];
  for(let c=0;c<8;c++){
   const w=cuts[c+1]-cuts[c];let input=await sharp(source).extract({left:cuts[c],top:y,width:w,height:h}).png().toBuffer();
   let ratio=d.scale||1,cw=Math.round(w*ratio),ch=Math.round(h*ratio);if(ratio!==1)input=await sharp(input).resize(cw,ch,{kernel:'nearest'}).png().toBuffer();
   if(cw>S||ch>S){input=await sharp(input).resize({width:S-24,height:S-24,fit:'inside',kernel:'nearest'}).png().toBuffer();const m=await sharp(input).metadata();ratio*=m.height/ch;cw=m.width;ch=m.height;}
   const f=path.join(dir,state+'_'+String(c).padStart(2,'0')+'.png'),top=Math.min(S-ch,S-49-Math.round(bottom*ratio));
   let offsetX=0,offsetY=0;if(['Idle','Run','Jump_Start','Jump_Loop','Land'].includes(state)){const b=await mainBounds(input);input=await sharp(input).extract(b).png().toBuffer();offsetX=b.left;offsetY=b.top;}
   await sharp({create:{width:S,height:S,channels:4,background:'#00000000'}}).composite([{input,left:Math.floor((S-cw)/2)+offsetX,top:Math.max(0,top)+offsetY}]).png().toFile(f);meta(f);
   const stats=await sharp(f).stats();if(stats.channels[3].max===0)throw Error('Empty cell: '+f);
   const name=d.id+'/'+path.basename(f);frames.push(name);sheet.push({input:f,left:c*S,top:r*S});
  }
  const strip=d.id+'/'+state+'_strip.png';await sharp({create:{width:S*8,height:S,channels:4,background:'#00000000'}}).composite(frames.map((f,i)=>({input:path.join(root,f),left:i*S,top:0}))).png().toFile(path.join(root,strip));
  animations.push({state,fps:d.id==='orc'&&state==='Run'?10:timing[state],loop:['Idle','Run','Jump_Loop'].includes(state),frameCount:8,frames,strip});
 }
 const f=d.id+'/spritesheet.png';await sharp({create:{width:S*8,height:S*rows,channels:4,background:'#00000000'}}).composite(sheet).png().toFile(path.join(root,f));
 manifest.characters.push({id:d.id,name:d.name,columns:8,rows,sheet:f,animations});
}
const extra=manifest.characters.find(c=>c.id==='hero_air_land'),hero=manifest.characters.find(c=>c.id==='hero');
if(extra){for(const a of extra.animations){const frames=[];for(let i=0;i<8;i++){const name='hero/'+a.state+'_'+String(i).padStart(2,'0')+'.png',target=path.join(root,name);fs.copyFileSync(path.join(root,a.frames[i]),target);meta(target);frames.push(name)}const strip='hero/'+a.state+'_strip.png';fs.copyFileSync(path.join(root,a.strip),path.join(root,strip));const item={...a,frames,strip};hero.animations=hero.animations.filter(x=>x.state!==a.state);hero.animations.push(item)}
 const order=['Idle','Run','Jump_Start','Jump_Loop','Land','Dash','Hit','Death'];hero.animations.sort((a,b)=>order.indexOf(a.state)-order.indexOf(b.state));hero.rows=8;
 await sharp({create:{width:S*8,height:S*8,channels:4,background:'#00000000'}}).composite(hero.animations.flatMap((a,r)=>a.frames.map((f,c)=>({input:path.join(root,f),left:c*S,top:r*S})))).png().toFile(path.join(root,hero.sheet));
 manifest.characters=manifest.characters.filter(c=>c.id!=='hero_air_land');
}
fs.writeFileSync(path.join(root,'manifest.json'),JSON.stringify(manifest,null,2));fs.writeFileSync(path.join(root,'manifest.js'),'const ANIMATION_V2 = '+JSON.stringify(manifest)+';');
await sharp({create:{width:S*3,height:S*2,channels:4,background:'#202333'}}).composite(manifest.characters.map((c,i)=>({input:path.join(root,c.animations[1].frames[1]),left:i%3*S,top:Math.floor(i/3)*S}))).png().toFile(path.join(root,'overview.png'));
console.log('V2 verified nonempty alpha cells:',manifest.characters.reduce((n,c)=>n+c.animations.length*8,0),'frames.');
})().catch(e=>{console.error(e);process.exit(1)});
