import process as p
R=p.ROOT
f=p.nearest([r[:128] for r in p.readpng(R/'_1x/Hero_Idle.png')],4)
layouts={'Run':(1774,887,4,2,8),'JumpStart':(1774,887,2,1,2),'Fall':(1774,887,2,1,2),'Land':(2172,724,3,1,3),'Dash':(1254,1254,2,2,4),'Hit':(1774,887,2,1,2),'Death':(1536,1024,3,2,6)}
for n,(w,h,cols,rows,count) in layouts.items():
 a=[[p.ZERO]*w for _ in range(h)]
 for i in range(count):
  dx=round((i%cols+.5)*w/cols-256);dy=round((i//cols+1)*h/rows-20-512)
  for y,row in enumerate(f):
   for x,pixel in enumerate(row):
    if pixel[3] and 0<=x+dx<w and 0<=y+dy<h:a[y+dy][x+dx]=pixel
 p.writepng(R/'Sources'/('Hero_'+n+'_Idle_size_layout_4x.png'),a)
