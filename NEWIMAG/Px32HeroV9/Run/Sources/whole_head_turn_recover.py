from pathlib import Path
R=Path(__file__).resolve().parents[1]
s=(R/'Sources/whole_head_recover.py').read_text()
s=s.replace("R=Path(__file__).resolve().parents[1]", "R=Path(__file__).resolve().parents[1].parent/'Move'")
s=s.replace("for arg in sys.argv[1:]:", "for arg in ['0']:")
s=s.replace("read(R/'Sources/prev4/Hero_Run_10.png')[:,128*i:128*(i+1)]", "read(R/'Sources/prev3/Turn_F0.png')")
s=s.replace("f'WholeHead_{i:02}_generated*.png'", "'WholeHead_Turn_generated*.png'")
s=s.replace("mask[:y+ds[i],x0:x1]=True", "mask[:y-4,max(0,x0-20):max(0,x1-20)]=True")
s=s.replace("WholeHead_{i:02}", "WholeHead_Turn")
s=s.replace("a[18:88,15:112],out[18:88,15:112]", "a[14:84,0:96],out[14:84,0:96]")
exec(compile(s,str(__file__),'exec'))
