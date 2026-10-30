"""Превью data.json картинкой — быстрая проверка, что карта собрана правильно."""
import json, sys
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Polygon as MplPoly
from matplotlib.collections import PatchCollection

d = json.load(open(sys.argv[1] if len(sys.argv) > 1 else "data/data.json"))
out = sys.argv[2] if len(sys.argv) > 2 else "data/preview.png"

fig, ax = plt.subplots(figsize=(16, 14), dpi=110)
ax.set_facecolor("#0d1b2a")

sea = d["sea"]
ax.add_patch(MplPoly([(sea[i], -sea[i + 1]) for i in range(0, len(sea), 2)], closed=True, fc="#1b3a57", ec="none", zorder=1))

for be in d.get("beach", []):
    ax.add_patch(MplPoly([(be[i], -be[i + 1]) for i in range(0, len(be), 2)], closed=True, fc="#d9c89b", ec="none", zorder=2))

col_g = {0: "#2f6b3a", 1: "#1f4d2a", 2: "#3f7d4a"}
for k, contour in d["g"]:
    ax.add_patch(MplPoly([(contour[i], -contour[i + 1]) for i in range(0, len(contour), 2)], closed=True, fc=col_g.get(k, "#2f6b3a"), ec="none", alpha=.7, zorder=3))

for poly in d["w"]:
    ax.add_patch(MplPoly([(poly[0][i], -poly[0][i + 1]) for i in range(0, len(poly[0]), 2)], closed=True, fc="#2a7fb8", ec="none", zorder=4))

col_r = {0: "#5a5a5a", 1: "#4a4a4a", 2: "#6d6d6d", 3: "#7d7d7d", 4: "#2a7fb8"}
for cls, width, pts in d["r"]:
    ax.plot([pts[i] for i in range(0, len(pts), 2)], [-pts[i + 1] for i in range(0, len(pts), 2)],
            lw=max(0.6, width / 4), color=col_r.get(cls, "#555"), solid_capstyle="round", zorder=5)

patches, colors = [], []
col_b = {0: "#c9b39a", 1: "#9aa7b4", 2: "#8d8578", 3: "#d8c9a8", 4: "#c98f7a"}
for h, kind, roof, ishome, pts in d["b"]:
    patches.append(MplPoly([(pts[i], -pts[i + 1]) for i in range(0, len(pts), 2)], closed=True))
    colors.append("#ffd400" if ishome else col_b.get(kind, "#bbb"))
ax.add_collection(PatchCollection(patches, fc=colors, ec="#3a3a3a", lw=0.2, zorder=6))

for name, x, z, kind in d.get("lab", []):
    ax.plot(x, -z, "o", ms=4, color="#ff5d5d", zorder=8)
    ax.annotate(name, (x, -z), fontsize=6, color="#ffd9a0", zorder=9)

for i, (name, x, z, where) in enumerate(d["cats"]):
    ax.plot(x, -z, "*", ms=9, color="#ffa726", zorder=10, markeredgecolor="#4a2a00")
    ax.annotate(name, (x, -z), fontsize=6, color="#ffe0b2", xytext=(4, 4), textcoords="offset points", zorder=11)

if d.get("pier"):
    p = d["pier"]
    ax.plot([p[i] for i in range(0, len(p), 2)], [-p[i + 1] for i in range(0, len(p), 2)], "-", color="#c8a97e", lw=2, zorder=7)

hx, hz, hh = d["home"]
ax.plot(hx, -hz, "s", ms=10, color="#ff2d55", zorder=12)
ax.annotate("Наш дом", (hx, -hz), fontsize=8, color="#ff2d55", xytext=(6, -10), textcoords="offset points", zorder=12)

ax.set_aspect("equal")
ax.set_xlim(-2100, 2100)
ax.set_ylim(-2100, 2100)
ax.set_title("Котики Зеленоградска — data.json: %d домов, %d линий, %d водоёмов, %d котиков"
             % (len(d["b"]), len(d["r"]), len(d["w"]), len(d["cats"])), color="#eee")
ax.tick_params(colors="#666")
for s in ax.spines.values():
    s.set_color("#333")
fig.tight_layout()
fig.savefig(out, facecolor="#101418")
print("saved", out)
