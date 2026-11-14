from assemble import trim,put
from build import *
specs={'Bag_Hip_Front':(39,(0,0)), 'Bag_Held_Front':(56,(0,0)), 'Bag_Hip_Open':(52,(0,0)), 'Bag_Held_Open':(40,(0,0))}
for n,(pitch,origin) in specs.items():
    a=trim(recover(SRC/f'V9Fix_{n}_1_generated.png',pitch,origin,size=(40,40)))
    save(f'Sources/V9Fix_{n}_recovered.png',a)
    save(f'Sources/V9Fix_{n}_review.png',enlarge(a,16))
    print(n,a.shape)
for n in ['Front','Side','Back']:
    a=recover(SRC/f'V9Fix_Restore_{n}_1_generated.png',10,(-5,-5))
    save(f'Sources/V9Fix_Restore_{n}_recovered.png',a)
    save(f'Sources/V9Fix_Restore_{n}_review.png',enlarge(a,4))
