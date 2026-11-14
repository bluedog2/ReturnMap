from pathlib import Path
import shutil,sys
src=Path(sys.argv[1]);dst=Path(__file__).parent/sys.argv[2]
shutil.copy2(src,dst)
