from process import *

def make(selected):
    records=json.loads((ROOT/'grid_report.json').read_text(encoding='utf-8'))
    attempts=json.loads((ROOT/'attempts.json').read_text(encoding='utf-8'))
    (ROOT/'_1x').mkdir(exist_ok=True);(ROOT/'Sources').mkdir(exist_ok=True)
    sprites={};checks={}
    for name,num in selected.items():
        a=next(a for a in attempts if a['name']==name and a['attempt']==num)
        r=next(r for r in records if r['name']==name and r['attempt']==num)
        assert r['fits_canvas']
        shutil.copyfile(a['path'],ROOT/'Sources'/f'Hero_{name}_imagegen.png')
        im=read(ROOT/'Candidates'/f'Hero_{name}_attempt{num}_1x.png')
        sprites[name]=im
        save(ROOT/'_1x'/f'Hero_{name}.png',im)
        save(ROOT/f'Hero_{name}_6x.png',zoom(im,6))
        b=bbox(im);colors={p[:3] for row in im for p in row if p[3]}
        checks[name]=dict(attempts=4,selected_attempt=num,canvas=[len(im[0]),len(im)],bbox_exclusive=b,bbox_size=[b[2]-b[0],b[3]-b[1]],k=r['grid_x']['k'],palette_outside=sum(p[3]!=0 and p[:3] not in P for row in im for p in row),alpha_values=sorted({p[3] for row in im for p in row}),colors_used=len(colors),bottom_row=b[3]==len(im),horizontal_center_error=(b[0]+b[2]-len(im[0]))/2,exact_grid=False)
        assert checks[name]['palette_outside']==0 and checks[name]['alpha_values']==[0,255] and checks[name]['bottom_row']
    base=REPO/'Assets/_Project/ResourcceEX/Sprites/Px32'
    tile=read(base/'Tiles/Tile_Play_Floor_A.png')
    hero=crop(read(base/'Player/Hero_Idle.png'),[0,0,64,64])
    gob=crop(read(base/'Enemies/Goblin_Raider_Idle.png'),[0,0,48,48])
    def floor(w,h): return [[tile[y%len(tile)][x%len(tile[0])] for x in range(w)] for y in range(h)]
    def ground(dst,im,cx,baseline):
        b=bbox(im);sp=crop(im,b);paste(dst,sp,cx-len(sp[0])//2,baseline-len(sp)+1)
    comparison=floor(640,192)
    for im,x in [(hero,113),(sprites['A'],221),(sprites['B'],332),(sprites['C'],462),(gob,579)]: ground(comparison,im,x,151)
    save(ROOT/'Compare_base.png',comparison)
    for name,im in sprites.items():
        screen=floor(640,360)
        ground(screen,im,280,224)
        ground(screen,gob,400,224)
        ground(screen,gob,458,268)
        save(ROOT/f'Screen_{name}_3x.png',zoom(screen,3))
    checks['existing_hero_bbox']=bbox(hero);checks['goblin_bbox']=bbox(gob)
    (ROOT/'validation.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
    shutil.copyfile(REPO/'NEWIMAG/StylePilot/palette_v2.txt',ROOT/'palette_used.txt')
    print(json.dumps(checks,indent=2))

if __name__=='__main__':
    import sys
    make(dict(zip('ABC',map(int,sys.argv[1:]))))
