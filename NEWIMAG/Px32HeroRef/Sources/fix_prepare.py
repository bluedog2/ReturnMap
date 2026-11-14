from build import *
import shutil
for folder in ['Turnaround','Bag','Arms','Masks']:
    for p in (ROOT/folder).glob('*'):
        target=SRC/'prev'/folder/p.name
        target.parent.mkdir(parents=True,exist_ok=True)
        if not target.exists():
            shutil.move(str(p),str(target));shutil.copy2(target,p)
for p in list(SRC.glob('*.py'))+list(SRC.glob('*.json'))+list(SRC.glob('*layer.png')):
    if p.name.startswith('fix_'):continue
    target=SRC/'prev'/'Sources'/p.name;target.parent.mkdir(parents=True,exist_ok=True)
    if not target.exists():shutil.copy2(p,target)
for name in ['Front','ThreeQuarter','Side','Back']:
    a=np.array(Image.open(ROOT/f'Turnaround/Hero_{name}.png').convert('RGBA'))
    save(f'Sources/V9Fix_{name}_target_8x.png',enlarge(a,8))
for p in (ROOT/'Bag').glob('Bag_*_*.png'):
    save('Sources/V9Fix_'+p.stem+'_target_32x.png',enlarge(np.array(Image.open(p).convert('RGBA')),32))
