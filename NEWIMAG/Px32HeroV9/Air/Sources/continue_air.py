from build_air import *
RISE_MASK=polygon([(29,46),(55,46),(58,56),(58,64),(64,68),(67,77),(65,87),(64,95),(54,96),(45,91),(29,87)]) | polygon([(79,73),(88,69),(97,70),(97,88),(78,89)])
APEX_MASK=polygon([(23,46),(55,46),(58,56),(58,64),(64,68),(67,77),(65,87),(64,95),(54,96),(35,93),(23,86)]) | polygon([(79,73),(86,66),(102,66),(103,91),(78,91)])
if __name__=='__main__':
 name=sys.argv[1]
 if name.startswith('Rise_'):
  i=int(name[-1]);process(name,S/'Rise_target.png',RISE_MASK,[[54,86],[54,87],[54,86]][i],top=29,bottom=45)
 if name=='Apex_F0':process(name,S/'Rise_F2_bagless.png',APEX_MASK,[54,85],top=29,bottom=45)
 if name=='Apex_F1':
  m=APEX_MASK.copy();m[86:128,35:115]=True
  process(name,S/'Rise_F2_bagless.png',m,[54,85],top=29,bottom=45)
 if name=='Apex_F2':
  m=FALL_MASK|polygon([(18,46),(44,46),(47,63),(50,70),(40,75),(18,75)])
  process(name,S/'Fall_F0_bagless.png',m,[53,85],top=29,bottom=45)
