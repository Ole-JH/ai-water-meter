# Hollowmere layout audit: the fixed placements in WorldGenerator.BuildTown, SeasonalTown, TownLife, StashChest, Waystone and
# the Rift Stone (copied here by hand: keep them in step), checked for overlaps (per season), things inside buildings and
# things blocking the cross streets. Prints the problems (none = clean) and draws hollowmere.png (top-down, north up).
#   python3 tools/layout/hollowmere_audit.py
from PIL import Image, ImageDraw
import itertools, math
houses = {"tavern":(121,153,8,7),"windmill":(120,162,8,8),"homeA_green":(131,163,6,6),"homeA_blue":(121,147,6,5),"church":(156,159,10,8),
 "homeB_n":(150,163,6,6),"homeB_e":(163,148,6,6),"market":(120,131,7,7),"smithy":(157,130,7,6)}
blocks = {"well":(143,143,3,3),"farm":(120,119,13,7),"pen":(163,121,6,5)}
# (x,z,radius,label,season) season: all|summer|autumn|winter|spring
P=[]
def add(x,z,r,l,s="all"): P.append((x,z,r,l,s))
for i in range(3): add(130.5+i*2,138.5,0.9,f"stall{i}")
for px in (150.5,153.5,156.5): add(px,122.5,0.3,"post")
add(155.5,130.5,0.8,"smith station"); add(134.5,151.0,0.8,"cooking station")
npcs={"Aldric":(148.5,168.5),"Wren":(139.5,167.5),"Gorrin":(154.5,133.5),"Lysa":(128.5,134.5),"Mae":(160.5,156.5),"Thomas":(134.5,123.5),
 "Jenkins":(152.5,154.5),"Brann":(162.5,138.5),"Hilda":(154.5,139.5),"Rosie":(130.5,155.5),"Vex":(135.5,134.5),"Orla":(166.5,128.5)}
for n,(x,z) in npcs.items(): add(x,z,0.4,"NPC "+n)
posts=[(135.6,135.6),(153.4,135.6),(135.6,153.4),(153.4,153.4),(141.6,141.6),(147.4,141.6),(141.6,147.4),(147.4,147.4)]
for d in (121.5,129.5,157.5,166.5): posts+= [(141.4,d),(147.6,d),(119.4 if d==121.5 else d,141.4),(119.4 if d==121.5 else d,147.6)]
for x,z in posts: add(x,z,0.25,"lantern")
for x,z,r,l in [(132.5,141.0,1.1,"cart"),(127.5,139.6,0.5,"barrels"),(137.4,139.0,0.5,"boxes"),(153.0,130.6,0.4,"barrel"),(155.0,136.6,0.6,"crates"),
  (158.5,120.0,0.4,"barrel2"),(160.0,125.0,0.4,"banner tr"),(129.5,152.0,0.5,"barrels2"),(155.5,157.5,0.6,"hedge"),
  (140.6,170.5,0.3,"banner N1"),(148.4,170.5,0.3,"banner N2"),(140.6,118.5,0.3,"banner S1"),(148.4,118.5,0.3,"banner S2")]: add(x,z,r,l)
add(149.5,139.5,0.6,"stash chest (likely)"); add(140.5,154.5,0.8,"waystone"); add(149.5,149.5,0.9,"rift stone")
add(144.5,142.4,0.3,"job well"); 
for x,z in [(140.5,140.6),(148.4,140.6),(147.2,149.8),(144.5,147.4)]: add(x,z,0.6,"meeting")
for x,z in [(131.5,137.4),(133.5,137.4)]: add(x,z,0.3,"job market")
add(138.5,166.5,0.3,"job woodpile"); add(161.5,157.6,0.3,"job church")
# seasonal
for y in (129.5,134.5,155.5,160.5):
  add(140.7,y,0.15,"bunting pole","summer"); add(148.3,y,0.15,"bunting pole","summer")
for x in (129.5,134.5,155.5,160.5):
  add(x,140.7,0.15,"bunting pole","summer"); add(x,148.3,0.15,"bunting pole","summer")
add(139.5,149.5,1.0,"maypole","summer"); add(139.5,138.8,1.0,"bonfire","summer")
for x,z in [(137.6,137.6),(150.4,150.4),(137.6,150.4)]: add(x,z,0.3,"candle lantern","summer")
for x,z in [(137.4,137.6),(150.6,137.4),(137.6,150.5),(131.5,127.6)]: add(x,z,0.6,"hay bale","autumn")
add(139.5,149.5,1.6,"xmas tree","winter")
for x,z in [(150.5,138.5),(133.5,149.5),(159.6,150.6),(127.6,141.2),(147.5,128.5),(139.4,157.5)]: add(x,z,0.6,"snowman","winter")
add(153.2,157.8,0.8,"sled","winter"); add(165.5,127.5,0.7,"reindeer","winter")
def inrect(x,z,r): return r[0]<=x<r[0]+r[2] and r[1]<=z<r[1]+r[3]
issues=[]
for x,z,rad,l,s in P:
  for n,r in {**houses,**blocks}.items():
    if inrect(x,z,r): issues.append(f"{l} ({x},{z}) inside {n}")
  if (142<=x<147 or 142<=z<147) and l not in ("job well",) and not l.startswith("meeting") and not l.startswith("lantern") and "pole" not in l and "banner" not in l:
    if not (143<=x<146 and 143<=z<146): issues.append(f"{l} ({x},{z}) in a cross street [{s}]")
for a,b in itertools.combinations(P,2):
  if a[4]!=b[4] and "all" not in (a[4],b[4]): continue
  d=math.hypot(a[0]-b[0],a[1]-b[1])
  if d < a[2]+b[2]: issues.append(f"overlap {d:.1f}m: {a[3]} [{a[4]}] / {b[3]} [{b[4]}]")
print("\n".join(issues))
S=12; img=Image.new("RGB",(57*S,57*S),(70,110,60)); g=ImageDraw.Draw(img)
T=lambda x,z:((x-116)*S,(173-z)*S)
for x in range(116,173):
  for z in range(116,173):
    if 142<=x<=146 or 142<=z<=146 or (136<=x<=152 and 136<=z<=152): g.rectangle([*T(x,z+1),*T(x+1,z)],fill=(150,140,120))
for n,r in houses.items(): g.rectangle([*T(r[0],r[1]+r[3]),*T(r[0]+r[2],r[1])],fill=(120,70,50),outline=(0,0,0)); g.text(T(r[0]+0.3,r[1]+r[3]-0.3),n,fill=(255,255,255))
for n,r in blocks.items(): g.rectangle([*T(r[0],r[1]+r[3]),*T(r[0]+r[2],r[1])],outline=(250,250,100))
col={"all":(255,255,255),"summer":(255,200,0),"autumn":(255,120,0),"winter":(120,200,255)}
for x,z,r,l,s in P:
  a,b=T(x,z); g.ellipse([a-r*S,b-r*S,a+r*S,b+r*S],outline=col[s]); 
  if l.startswith("NPC") or l in("waystone","rift stone","stash chest (likely)","cooking station","smith station"): g.text((a+4,b-6),l.replace("NPC ",""),fill=(255,255,180))
img.save("hollowmere.png")
