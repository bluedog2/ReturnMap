from process import *
for name,c,r,n,k in [('Run',4,2,8,5),('Land',3,1,3,8)]:
 fs,meta=recover(name,c,r,n,k,3)
 print(name,[(m['raw_size'], [x for x,p in enumerate(f[0]) if p[3]]) for f,m in zip(fs,meta)])
