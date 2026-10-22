const fs=require('fs'),path=require('path'),sharp=require(process.env.NEWIMAG_SHARP);
const root=__dirname,env=path.resolve(root,'../../Assets/_Project/ResourcceEX/Sprites/Environment');
(async()=>{const result=[];fs.mkdirSync(path.join(root,'References'),{recursive:true});
for(const phase of ['BuildPhase','PlayPhase'])for(const chapter of ['Chapter1','Chapter2']){
 const dir=path.join(env,phase,chapter),files=fs.readdirSync(dir).filter(f=>f.endsWith('.png')).sort(),parts=[];const cells=[];
 const sample=await sharp(path.join(dir,files[0])).metadata();
 for(const name of files){const m=name.match(/r(\d+)_c(\d+)/),r=+m[1]-1,c=+m[2]-1;parts.push({input:path.join(dir,name),left:c*sample.width,top:r*sample.height});cells.push({name,row:r,col:c});}
 const width=12*sample.width,height=11*sample.height,out=path.join(root,'References',phase+'_'+chapter+'.png');
 await sharp({create:{width,height,channels:4,background:'#00000000'}}).composite(parts).png().toFile(out);
 const view=out.replace('.png','_3x.png');await sharp(out).resize(width*3,height*3,{kernel:'nearest'}).png().toFile(view);
 result.push({phase,chapter,count:files.length,width:sample.width,height:sample.height,atlasWidth:width,atlasHeight:height,reference:out,view,cells});
}
fs.writeFileSync(path.join(root,'original-inventory.json'),JSON.stringify(result,null,2));console.log(result.map(({cells,...r})=>r));
})().catch(e=>{console.error(e);process.exit(1)});
