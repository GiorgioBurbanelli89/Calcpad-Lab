# -*- coding: utf-8 -*-
#" Demo de HOVER interactivo — igual en Python real y en Calcpad Suite Py
#' Tres gráficas básicas: líneas, mapa de calor 2D y superficie 3D.
#' En Suite Py el motor las convierte en canvas interactivos con hover (automático).
#' En Python real, el helper _hover() agrega el datatip que sigue al cursor.
import numpy as np
import matplotlib.pyplot as plt


# ── helper de hover para Python real (en Suite Py es automático) ──────────────
def _hover(fig):
    from mpl_toolkits.mplot3d import proj3d
    ann = fig.text(0, 0, "", fontsize=8, visible=False, zorder=30,
                   bbox=dict(boxstyle="round", fc="#ffffe0", ec="#888", alpha=.95))
    def mv(e):
        ax = e.inaxes
        if ax is None:
            if ann.get_visible(): ann.set_visible(False); fig.canvas.draw_idle()
            return
        s = getattr(ax, "_surf3d", None); f = getattr(ax, "_field", None); t = None
        if s is not None:                                   # superficie 3D
            X, Y, Z, W = s
            x2, y2, _ = proj3d.proj_transform(X.ravel(), Y.ravel(), Z.ravel(), ax.get_proj())
            p = ax.transData.transform(np.column_stack([x2, y2]))
            d = (p[:, 0] - e.x)**2 + (p[:, 1] - e.y)**2; k = int(np.argmin(d))
            if d[k] < 900: t = "x=%.2f y=%.2f\nz=%.4g" % (X.ravel()[k], Y.ravel()[k], W.ravel()[k])
        elif f is not None and e.xdata is not None:         # mapa de calor 2D
            X, Y, Z = f
            i, j = np.unravel_index(int(np.argmin((X - e.xdata)**2 + (Y - e.ydata)**2)), Z.shape)
            t = "x=%.2f y=%.2f\nvalor=%.4g" % (e.xdata, e.ydata, Z[i, j])
        elif e.xdata is not None:                           # líneas (interpolación continua)
            best, bd = None, 1e30
            for ln in ax.get_lines():
                xs = np.asarray(ln.get_xdata(), float); ys = np.asarray(ln.get_ydata(), float)
                if xs.size < 2 or e.xdata < xs.min() or e.xdata > xs.max(): continue
                yi = float(np.interp(e.xdata, xs, ys)); px, py = ax.transData.transform((e.xdata, yi))
                if (py - e.y)**2 < bd:
                    bd = (py - e.y)**2; lab = ln.get_label(); lab = "" if lab.startswith("_") else lab + "\n"
                    best = "%sx=%.3g\ny=%.4g" % (lab, e.xdata, yi)
            t = best
        if t is None:
            if ann.get_visible(): ann.set_visible(False); fig.canvas.draw_idle()
            return
        ann.set_text(t); ann.set_position((e.x / fig.bbox.width + .012, e.y / fig.bbox.height + .012))
        ann.set_visible(True); fig.canvas.draw_idle()
    fig.canvas.mpl_connect("motion_notify_event", mv)


#" 1) Líneas — y = sin(x), cos(x)
x = np.linspace(0, 10, 40)
f1, a1 = plt.subplots(figsize=(6, 3.5))
a1.plot(x, np.sin(x), marker='o', ms=3, label='sin(x)')
a1.plot(x, np.cos(x), marker='o', ms=3, label='cos(x)')
a1.set_title('Lineas'); a1.set_xlabel('x'); a1.legend(); a1.grid(alpha=.3)
_hover(f1); plt.show()

#" 2) Mapa de calor 2D — z = sin(x)·cos(y)
xs = np.linspace(0, 6, 60); ys = np.linspace(0, 6, 60)
X, Y = np.meshgrid(xs, ys, indexing='ij'); Z = np.sin(X) * np.cos(Y)
f2, a2 = plt.subplots(figsize=(5, 4))
a2.contourf(X, Y, Z, 40, cmap='jet_r'); a2._field = (X, Y, Z)
a2.set_title('Mapa de calor 2D'); a2.set_xlabel('x'); a2.set_ylabel('y')
_hover(f2); plt.show()

#" 3) Superficie 3D — z = exp(-(x²+y²))
X2, Y2 = np.meshgrid(np.linspace(-2, 2, 45), np.linspace(-2, 2, 45), indexing='ij')
Z2 = np.exp(-(X2**2 + Y2**2))
f3 = plt.figure(figsize=(6, 5)); a3 = f3.add_subplot(111, projection='3d')
a3.plot_surface(X2, Y2, Z2, cmap='jet_r'); a3._surf3d = (X2, Y2, Z2, Z2)
a3.set_title('Superficie 3D'); a3.set_xlabel('x'); a3.set_ylabel('y')
_hover(f3); plt.show()
