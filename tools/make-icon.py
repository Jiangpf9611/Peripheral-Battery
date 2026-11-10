# Rebuild the approved logo at icon sizes; only remove the white exterior.
from PIL import Image
from pathlib import Path
from collections import deque
im=Image.open('assets/PeriBatt_Logo_Approved.png').convert('RGBA');w,h=im.size;px=im.load();q=deque([(0,0)]);seen=set()
while q:
 x,y=q.popleft()
 if (x,y) in seen or not (0<=x<w and 0<=y<h):continue
 seen.add((x,y));r,g,b,a=px[x,y]
 if min(r,g,b)<230:continue
 px[x,y]=(r,g,b,0);q.extend(((x-1,y),(x+1,y),(x,y-1),(x,y+1)))
im=im.crop(im.getbbox());Path('src/assets').mkdir(exist_ok=True);im.save('src/assets/PeriBatt.ico',sizes=[(16,16),(24,24),(32,32),(48,48),(256,256)]);ui=im.copy();ui.thumbnail((128,128),Image.Resampling.LANCZOS);ui.save('src/assets/PeriBatt.png',optimize=True)

# The window requests only 32px; keep the full ICO solely in native EXE resources.
with Image.open('src/assets/PeriBatt.ico') as ico:
    window_icon=ico.ico.getimage((32,32))
    window_icon.save('src/assets/PeriBatt-window.ico',sizes=[(32,32)])
