/* hekatan-plot.js — motor de graficas estilo MATLAB para Calcpad.
   Todo el dibujo canvas vive aqui. Los .cpd solo llaman HkPlot.* (sin <> ).
   2D: mapeo DATOS->pixeles via HkPlot.axis. 3D: HkPlot.axis3 + arrastrar para ORBITAR. Colormap = jet_r. */
(function (global) {
  var HkPlot = {
    figs: {}, cur: null,

    figure: function (id, w, h) {              // el .js CREA el canvas (no hay <canvas> en el .cpd)
      var wrap = document.createElement("div");
      wrap.style.cssText = "position:relative;display:inline-block;vertical-align:top;margin:4px";
      var cv = document.createElement("canvas"); cv.id = id;
      cv.style.cssText = "display:block;background:#fff;border:1px solid #ccc;width:" + w + "px;height:" + h + "px";
      var tip = document.createElement("div");   // datatip 2D (hover) que sigue al cursor
      tip.style.cssText = "position:absolute;pointer-events:none;display:none;background:rgba(25,28,32,0.9);color:#fff;font:11px Segoe UI;padding:3px 7px;border-radius:4px;white-space:nowrap;transform:translate(10px,-50%);z-index:5;box-shadow:0 1px 4px rgba(0,0,0,0.3)";
      wrap.appendChild(cv); wrap.appendChild(tip);
      var sc = document.currentScript;
      if (sc && sc.parentNode) sc.parentNode.insertBefore(wrap, sc); else document.body.appendChild(wrap);
      var dpr = Math.max(2, window.devicePixelRatio || 1);   // render a >=2x para que no pixele al acercar
      cv.width = w * dpr; cv.height = h * dpr;
      var ctx = cv.getContext("2d");
      ctx.scale(dpr, dpr);
      ctx.lineJoin = "round"; ctx.lineCap = "round";
      ctx.font = "11px Segoe UI";
      this.figs[id] = { ctx: ctx, cv: cv, tip: tip, dpr: dpr, w: w, h: h, xmin: 0, xmax: w, ymin: 0, ymax: h,
                        sc: 1, padx: 8, pady: 8, mode: "2d", az: 0.7, el: 0.45, prims: [], dpts: [], tipOn: false, tipLabel: "" };
      this.cur = id;
    },
    S: function () { return this.figs[this.cur]; },
    hold: function () { },

    // ===================== 2D =====================
    axis: function (x0, x1, y0, y1) {            // EQUAL-aspect (geometria / FEM)
      var s = this.S(); s.mode = "2d";
      s.xmin = x0; s.xmax = x1; s.ymin = y0; s.ymax = y1;
      s.mL = 38; s.mB = 26; s.mR = 84; s.mT = 14;   // margenes: izq(numeros y), abajo(numeros x), der(colorbar), arriba
      var aW = s.w - s.mL - s.mR, aH = s.h - s.mT - s.mB;
      s.sc = Math.min(aW / (x1 - x0), aH / (y1 - y0));
      s.scx = s.sc; s.scy = s.sc; s.bx0 = x0; s.by0 = y0; s.xlog = 0; s.ylog = 0;
    },
    axischart: function (x0, x1, y0, y1) {       // escala INDEPENDIENTE (datos: estira para llenar)
      var s = this.S(); s.mode = "2d";
      s.xmin = x0; s.xmax = x1; s.ymin = y0; s.ymax = y1; s.xlog = 0; s.ylog = 0;
      s.mL = 46; s.mB = 28; s.mR = 18; s.mT = 16;
      s.scx = (s.w - s.mL - s.mR) / (x1 - x0); s.scy = (s.h - s.mT - s.mB) / (y1 - y0);
      s.sc = Math.min(s.scx, s.scy); s.bx0 = x0; s.by0 = y0;
    },
    axislog: function (x0, x1, y0, y1, xl, yl) { // ejes log (semilogx/semilogy/loglog)
      this.axischart(x0, x1, y0, y1); var s = this.S(); s.xlog = xl ? 1 : 0; s.ylog = yl ? 1 : 0;
      s.bx0 = s.xlog ? Math.log10(x0) : x0; s.by0 = s.ylog ? Math.log10(y0) : y0;
      s.scx = (s.w - s.mL - s.mR) / ((s.xlog ? Math.log10(x1) : x1) - s.bx0);
      s.scy = (s.h - s.mT - s.mB) / ((s.ylog ? Math.log10(y1) : y1) - s.by0);
    },
    X: function (x) { var s = this.S(); return s.mL + ((s.xlog ? Math.log10(x) : x) - s.bx0) * s.scx; },
    Y: function (y) { var s = this.S(); return s.h - s.mB - ((s.ylog ? Math.log10(y) : y) - s.by0) * s.scy; },
    grid: function (ndx, ndy) {                  // plano cartesiano 2D: grilla + ticks + numeros + caja
      var s = this.S(), ctx = s.ctx, i;
      ctx.lineWidth = 1; ctx.font = "10px Segoe UI";
      for (i = 0; i <= ndx; i++) {
        var xv = s.xmin + i * (s.xmax - s.xmin) / ndx, px = this.X(xv);
        ctx.strokeStyle = "rgba(0,0,0,0.06)"; ctx.beginPath(); ctx.moveTo(px, this.Y(s.ymax)); ctx.lineTo(px, this.Y(s.ymin)); ctx.stroke();
        ctx.fillStyle = "#555"; ctx.textAlign = "center"; ctx.textBaseline = "top"; ctx.fillText(this._fmt(xv), px, this.Y(s.ymin) + 4);
      }
      for (i = 0; i <= ndy; i++) {
        var yv = s.ymin + i * (s.ymax - s.ymin) / ndy, py = this.Y(yv);
        ctx.strokeStyle = "rgba(0,0,0,0.06)"; ctx.beginPath(); ctx.moveTo(this.X(s.xmin), py); ctx.lineTo(this.X(s.xmax), py); ctx.stroke();
        ctx.fillStyle = "#555"; ctx.textAlign = "right"; ctx.textBaseline = "middle"; ctx.fillText(this._fmt(yv), this.X(s.xmin) - 5, py);
      }
      ctx.strokeStyle = "#888"; ctx.lineWidth = 1;
      ctx.strokeRect(this.X(s.xmin), this.Y(s.ymax), this.X(s.xmax) - this.X(s.xmin), this.Y(s.ymin) - this.Y(s.ymax));
    },
    _fmt: function (v) { return Math.abs(v) < 1e-9 ? "0" : (v === Math.round(v) ? v.toFixed(0) : v.toFixed(1)); },
    _nf: function (v) { var a = Math.abs(v); if (a < 1e-12) return "0"; if (a >= 1e4 || a < 1e-3) return v.toExponential(2); if (a < 1) return parseFloat(v.toPrecision(3)).toString(); return v.toFixed(2); },
    datatip: function (label) {                  // datatip 2D (hover) estilo MATLAB datacursormode
      var s = this.S(), HkPlot = this; s.tipOn = true; s.tipLabel = "" + label;
      if (s._tipBound) return; s._tipBound = true;
      s.cv.onmousemove = function (e) {
        if (!s.tipOn || !s.dpts.length) return;
        var r = s.cv.getBoundingClientRect(), mx = e.clientX - r.left, my = e.clientY - r.top, best = -1, bd = 289, i, px, py, dx, dy;
        for (i = 0; i < s.dpts.length; i++) { var d = s.dpts[i]; px = HkPlot.X(d.x); py = HkPlot.Y(d.y); dx = px - mx; dy = py - my; if (dx * dx + dy * dy < bd) { bd = dx * dx + dy * dy; best = i; } }
        if (best >= 0) { var d = s.dpts[best], qx = HkPlot.X(d.x), qy = HkPlot.Y(d.y); s.tip.style.display = "block"; s.tip.style.left = qx + "px"; s.tip.style.top = qy + "px"; s.tip.innerHTML = (s.tipLabel ? s.tipLabel + " = " : "") + HkPlot._nf(d.v); }
        else s.tip.style.display = "none";
      };
      s.cv.onmouseleave = function () { s.tip.style.display = "none"; };
    },
    datapoint: function (x, y, v) { this.S().dpts.push({ x: x, y: y, v: v }); },
    jet: function (t) {
      t = Math.max(0, Math.min(1, t)); var u = 1 - t;
      function c(z) { return Math.round(255 * Math.max(0, Math.min(1, 1.5 - Math.abs(4 * u - z)))); }
      return "rgb(" + c(3) + "," + c(2) + "," + c(1) + ")";
    },
    patch: function (p, t) {
      var ctx = this.S().ctx; ctx.beginPath(); ctx.moveTo(this.X(p[0]), this.Y(p[1]));
      for (var i = 2; i < p.length; i += 2) ctx.lineTo(this.X(p[i]), this.Y(p[i + 1]));
      ctx.closePath(); ctx.fillStyle = this.jet(t); ctx.fill();
      ctx.strokeStyle = "rgba(0,0,0,0.18)"; ctx.lineWidth = 0.5; ctx.stroke();
    },
    patchi: function (p, t1, t2, t3, t4) {   // cuadrilatero con color INTERPOLADO (bilineal) = surf 'FaceColor','interp' de MATLAB; sin borde -> mapa suave, no a bloques
      var ctx = this.S().ctx, N = 10, GL = this, iu, iv;
      function bx(u, v) { return (1 - u) * (1 - v) * p[0] + u * (1 - v) * p[2] + u * v * p[4] + (1 - u) * v * p[6]; }
      function by(u, v) { return (1 - u) * (1 - v) * p[1] + u * (1 - v) * p[3] + u * v * p[5] + (1 - u) * v * p[7]; }
      for (iu = 0; iu < N; iu++) for (iv = 0; iv < N; iv++) {
        var u0 = iu / N, u1 = (iu + 1) / N, v0 = iv / N, v1 = (iv + 1) / N, uc = (u0 + u1) / 2, vc = (v0 + v1) / 2;
        var tc = (1 - uc) * (1 - vc) * t1 + uc * (1 - vc) * t2 + uc * vc * t3 + (1 - uc) * vc * t4;
        ctx.beginPath();
        ctx.moveTo(GL.X(bx(u0, v0)), GL.Y(by(u0, v0)));
        ctx.lineTo(GL.X(bx(u1, v0)), GL.Y(by(u1, v0)));
        ctx.lineTo(GL.X(bx(u1, v1)), GL.Y(by(u1, v1)));
        ctx.lineTo(GL.X(bx(u0, v1)), GL.Y(by(u0, v1)));
        ctx.closePath();
        var col = GL.jet(tc); ctx.fillStyle = col; ctx.strokeStyle = col; ctx.lineWidth = 0.6; ctx.fill(); ctx.stroke();   // stroke del mismo color = sin costuras entre subceldas
      }
    },
    fill: function (p, col) {
      var ctx = this.S().ctx; ctx.beginPath(); ctx.moveTo(this.X(p[0]), this.Y(p[1]));
      for (var i = 2; i < p.length; i += 2) ctx.lineTo(this.X(p[i]), this.Y(p[i + 1]));
      ctx.closePath(); ctx.fillStyle = col; ctx.fill();
      ctx.strokeStyle = "#888"; ctx.lineWidth = 1; ctx.stroke();
    },
    line: function (x1, y1, x2, y2, col) {
      var ctx = this.S().ctx; ctx.strokeStyle = col; ctx.lineWidth = 1.5;
      ctx.beginPath(); ctx.moveTo(this.X(x1), this.Y(y1)); ctx.lineTo(this.X(x2), this.Y(y2)); ctx.stroke();
    },
    plot: function (x1, y1, x2, y2, col) {
      var ctx = this.S().ctx; ctx.strokeStyle = col; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.moveTo(this.X(x1), this.Y(y1)); ctx.lineTo(this.X(x2), this.Y(y2)); ctx.stroke();
    },
    scatter: function (x, y, col) {
      var ctx = this.S().ctx; ctx.fillStyle = col; ctx.beginPath(); ctx.arc(this.X(x), this.Y(y), 4, 0, 6.2832); ctx.fill();
    },
    rectangle: function (x1, y1, x2, y2, col) {
      var ctx = this.S().ctx; ctx.strokeStyle = col; ctx.lineWidth = 1.5;
      ctx.strokeRect(this.X(x1), this.Y(y1), this.X(x2) - this.X(x1), this.Y(y2) - this.Y(y1));
    },
    fixed: function (x1, y, x2) {                  // apoyo EMPOTRADO: linea + rayado de tierra
      var ctx = this.S().ctx, a = this.X(x1), b = this.X(x2), yy = this.Y(y), i, n, d = 8;
      ctx.strokeStyle = "#333"; ctx.lineWidth = 2.2;
      ctx.beginPath(); ctx.moveTo(a, yy); ctx.lineTo(b, yy); ctx.stroke();
      ctx.lineWidth = 1; n = Math.max(4, Math.round((b - a) / 11));
      for (i = 0; i <= n; i++) { var px = a + i * (b - a) / n; ctx.beginPath(); ctx.moveTo(px, yy); ctx.lineTo(px - d, yy + d); ctx.stroke(); }
    },
    pinned: function (x, y) {                       // apoyo ARTICULADO: triangulo + rayado
      var ctx = this.S().ctx, px = this.X(x), py = this.Y(y), w = 8, h = 11, i;
      ctx.strokeStyle = "#333"; ctx.fillStyle = "#fff"; ctx.lineWidth = 1.6;
      ctx.beginPath(); ctx.moveTo(px, py); ctx.lineTo(px - w, py + h); ctx.lineTo(px + w, py + h); ctx.closePath(); ctx.stroke();
      ctx.lineWidth = 1;
      for (i = 0; i <= 5; i++) { var qx = px - w + i * 2 * w / 5; ctx.beginPath(); ctx.moveTo(qx, py + h); ctx.lineTo(qx - 5, py + h + 5); ctx.stroke(); }
    },
    quiver: function (x, y, dx, dy, col) {
      var ctx = this.S().ctx, x1 = this.X(x), y1 = this.Y(y), x2 = this.X(x + dx), y2 = this.Y(y + dy);
      ctx.strokeStyle = col; ctx.fillStyle = col; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.moveTo(x1, y1); ctx.lineTo(x2, y2); ctx.stroke();
      var a = Math.atan2(y2 - y1, x2 - x1), h = 8;
      ctx.beginPath(); ctx.moveTo(x2, y2);
      ctx.lineTo(x2 - h * Math.cos(a - 0.4), y2 - h * Math.sin(a - 0.4));
      ctx.lineTo(x2 - h * Math.cos(a + 0.4), y2 - h * Math.sin(a + 0.4));
      ctx.closePath(); ctx.fill();
    },
    moment: function (x, y, col) {
      var ctx = this.S().ctx, cx = this.X(x), cy = this.Y(y), r = 16, a1 = -2.2, a2 = 2.2;
      ctx.strokeStyle = col; ctx.fillStyle = col; ctx.lineWidth = 2.4;
      ctx.beginPath(); ctx.arc(cx, cy, r, a1, a2, false); ctx.stroke();
      var ex = cx + r * Math.cos(a2), ey = cy + r * Math.sin(a2), tx = -Math.sin(a2), ty = Math.cos(a2), h = 8;
      ctx.beginPath(); ctx.moveTo(ex + h * tx, ey + h * ty);
      ctx.lineTo(ex - 0.5 * h * ty, ey + 0.5 * h * tx); ctx.lineTo(ex + 0.5 * h * ty, ey - 0.5 * h * tx);
      ctx.closePath(); ctx.fill();
    },
    text: function (x, y, s, col) {
      var ctx = this.S().ctx; ctx.fillStyle = col || "#333"; ctx.font = "12px Segoe UI";
      ctx.textAlign = "left"; ctx.textBaseline = "middle"; ctx.fillText(s, this.X(x), this.Y(y));
    },
    title: function (s) {
      var st = this.S(), ctx = st.ctx; ctx.fillStyle = "#222"; ctx.font = "bold 14px Segoe UI";
      ctx.textAlign = "center"; ctx.textBaseline = "top"; ctx.fillText(s, st.w / 2, 4); ctx.textAlign = "left";
    },
    xlabel: function (s) {
      var st = this.S(), ctx = st.ctx; ctx.fillStyle = "#333"; ctx.font = "12px Segoe UI";
      ctx.textAlign = "center"; ctx.fillText(s, st.w / 2, st.h - 4); ctx.textAlign = "left";
    },
    ylabel: function (s) {
      var st = this.S(), ctx = st.ctx; ctx.save(); ctx.translate(12, st.h / 2); ctx.rotate(-1.5708);
      ctx.fillStyle = "#333"; ctx.font = "12px Segoe UI"; ctx.textAlign = "center"; ctx.fillText(s, 0, 0); ctx.restore();
    },
    colorbar: function (vmin, vmax, rev) {
      var s = this.S();
      if (s.mode === "3d") { s.prims.push({ k: "cbar", a: vmin, b: vmax, rev: rev }); return; }
      this._cbar(s, vmin, vmax, rev);
    },
    _cbar: function (s, vmin, vmax, rev) {
      var ctx = s.ctx, n = 24, x0 = s.w - 56, y1 = s.mT, y0 = s.h - s.mB, w = 18, i, k;
      for (i = 0; i < n; i++) {
        var t = (i + 0.5) / n, ya = y0 - i / n * (y0 - y1), yb = y0 - (i + 1) / n * (y0 - y1);
        ctx.fillStyle = this.jet(rev ? 1 - t : t); ctx.fillRect(x0, yb, w, ya - yb + 0.6);   // rev=1: valor alto (arriba) = ROJO, como el mapa 3D
      }
      ctx.strokeStyle = "#444"; ctx.lineWidth = 1; ctx.strokeRect(x0, y1, w, y0 - y1);
      ctx.fillStyle = "#333"; ctx.font = "11px Segoe UI"; ctx.textAlign = "left"; ctx.textBaseline = "middle";
      for (k = 0; k <= 5; k++) {
        var v = vmin + k / 5 * (vmax - vmin), yy = y0 - k / 5 * (y0 - y1);
        ctx.fillText(this._nf(v), x0 + w + 6, yy);
        ctx.beginPath(); ctx.moveTo(x0 + w, yy); ctx.lineTo(x0 + w + 4, yy); ctx.stroke();
      }
    },
    colormap: function (name) { },

    // ===================== CHARTS MATLAB (elemento por elemento; el .cpd hace el #for) =====================
    bar: function (x, y, w, col) {                  // una barra (de 0 a y), centrada en x, ancho w
      var ctx = this.S().ctx, x1 = this.X(x - w / 2), x2 = this.X(x + w / 2), y0 = this.Y(0), y1 = this.Y(y);
      ctx.fillStyle = col || "#1f6feb"; ctx.fillRect(x1, Math.min(y0, y1), x2 - x1, Math.abs(y1 - y0));
      ctx.strokeStyle = "rgba(0,0,0,0.35)"; ctx.lineWidth = 1; ctx.strokeRect(x1, Math.min(y0, y1), x2 - x1, Math.abs(y1 - y0));
    },
    stem: function (x, y, col) {                    // tallo (stem): linea 0->y + circulo
      var ctx = this.S().ctx, c = col || "#1f6feb"; ctx.strokeStyle = c; ctx.lineWidth = 1.5;
      ctx.beginPath(); ctx.moveTo(this.X(x), this.Y(0)); ctx.lineTo(this.X(x), this.Y(y)); ctx.stroke();
      ctx.fillStyle = c; ctx.beginPath(); ctx.arc(this.X(x), this.Y(y), 3.5, 0, 6.2832); ctx.fill();
    },
    area: function (x1, y1, x2, y2, col) {          // relleno hasta y=0 (trapecio) + borde superior
      var ctx = this.S().ctx; ctx.beginPath();
      ctx.moveTo(this.X(x1), this.Y(0)); ctx.lineTo(this.X(x1), this.Y(y1)); ctx.lineTo(this.X(x2), this.Y(y2)); ctx.lineTo(this.X(x2), this.Y(0)); ctx.closePath();
      ctx.fillStyle = col || "rgba(31,111,235,0.35)"; ctx.fill();
      ctx.strokeStyle = "#1f6feb"; ctx.lineWidth = 1.6; ctx.beginPath(); ctx.moveTo(this.X(x1), this.Y(y1)); ctx.lineTo(this.X(x2), this.Y(y2)); ctx.stroke();
    },
    stairs: function (x1, y1, x2, y2, col) {        // escalon: horizontal en y1 hasta x2, luego sube a y2
      var ctx = this.S().ctx; ctx.strokeStyle = col || "#333"; ctx.lineWidth = 1.8;
      ctx.beginPath(); ctx.moveTo(this.X(x1), this.Y(y1)); ctx.lineTo(this.X(x2), this.Y(y1)); ctx.lineTo(this.X(x2), this.Y(y2)); ctx.stroke();
    },
    errorbar: function (x, y, e, col) {             // punto + barra vertical ±e con tapas
      var ctx = this.S().ctx, c = col || "#333", px = this.X(x), pa = this.Y(y - e), pb = this.Y(y + e), w = 4;
      ctx.strokeStyle = c; ctx.lineWidth = 1.4;
      ctx.beginPath(); ctx.moveTo(px, pa); ctx.lineTo(px, pb); ctx.moveTo(px - w, pa); ctx.lineTo(px + w, pa); ctx.moveTo(px - w, pb); ctx.lineTo(px + w, pb); ctx.stroke();
      ctx.fillStyle = c; ctx.beginPath(); ctx.arc(px, this.Y(y), 3, 0, 6.2832); ctx.fill();
    },
    marker: function (x, y, type, col) {            // marcadores MATLAB: o . * + x s d ^ v
      var ctx = this.S().ctx, px = this.X(x), py = this.Y(y), r = 4, c = col || "#1f6feb", t = "" + type;
      ctx.strokeStyle = c; ctx.fillStyle = c; ctx.lineWidth = 1.5;
      if (t === "o") { ctx.beginPath(); ctx.arc(px, py, r, 0, 6.2832); ctx.stroke(); }
      else if (t === ".") { ctx.beginPath(); ctx.arc(px, py, 2, 0, 6.2832); ctx.fill(); }
      else if (t === "*" || t === "+") { ctx.beginPath(); ctx.moveTo(px - r, py); ctx.lineTo(px + r, py); ctx.moveTo(px, py - r); ctx.lineTo(px, py + r); if (t === "*") { ctx.moveTo(px - r * 0.7, py - r * 0.7); ctx.lineTo(px + r * 0.7, py + r * 0.7); ctx.moveTo(px - r * 0.7, py + r * 0.7); ctx.lineTo(px + r * 0.7, py - r * 0.7); } ctx.stroke(); }
      else if (t === "x") { ctx.beginPath(); ctx.moveTo(px - r, py - r); ctx.lineTo(px + r, py + r); ctx.moveTo(px - r, py + r); ctx.lineTo(px + r, py - r); ctx.stroke(); }
      else if (t === "s") { ctx.strokeRect(px - r, py - r, 2 * r, 2 * r); }
      else if (t === "d") { ctx.beginPath(); ctx.moveTo(px, py - r); ctx.lineTo(px + r, py); ctx.lineTo(px, py + r); ctx.lineTo(px - r, py); ctx.closePath(); ctx.stroke(); }
      else if (t === "^") { ctx.beginPath(); ctx.moveTo(px, py - r); ctx.lineTo(px + r, py + r); ctx.lineTo(px - r, py + r); ctx.closePath(); ctx.stroke(); }
      else if (t === "v") { ctx.beginPath(); ctx.moveTo(px, py + r); ctx.lineTo(px + r, py - r); ctx.lineTo(px - r, py - r); ctx.closePath(); ctx.stroke(); }
    },
    imagesc: function (x, y, w, h, t) {             // una celda coloreada (imagesc/pcolor/heatmap) por valor t∈[0,1]
      var ctx = this.S().ctx, x1 = this.X(x), y1 = this.Y(y + h);
      ctx.fillStyle = this.jet(t); ctx.fillRect(x1, y1, this.X(x + w) - x1 + 0.6, this.Y(y) - y1 + 0.6);
    },
    contourCell: function (x0, y0, x1, y1, v00, v10, v11, v01, lv, col) {  // marching squares de 1 celda
      var c = [v00, v10, v11, v01], xx = [x0, x1, x1, x0], yy = [y0, y0, y1, y1], pts = [], i, j;
      for (i = 0; i < 4; i++) { j = (i + 1) % 4; var a = c[i], b = c[j]; if ((a < lv) !== (b < lv)) { var tt = (lv - a) / (b - a); pts.push(xx[i] + tt * (xx[j] - xx[i]), yy[i] + tt * (yy[j] - yy[i])); } }
      if (pts.length >= 4) { var ctx = this.S().ctx; ctx.strokeStyle = col || "#222"; ctx.lineWidth = 1.2; ctx.beginPath(); ctx.moveTo(this.X(pts[0]), this.Y(pts[1])); for (var k = 2; k < pts.length; k += 2) ctx.lineTo(this.X(pts[k]), this.Y(pts[k + 1])); ctx.stroke(); }
    },
    polar: function (theta, r, col) {               // un punto polar (theta en GRADOS); usar axis simetrico ±rmax
      var a = theta * 0.0174533, ctx = this.S().ctx;
      ctx.fillStyle = col || "#1f6feb"; ctx.beginPath(); ctx.arc(this.X(r * Math.cos(a)), this.Y(r * Math.sin(a)), 3, 0, 6.2832); ctx.fill();
    },
    legend: function (x, y, label, col) {           // una entrada de leyenda (swatch + texto) en coords de DATOS
      var ctx = this.S().ctx, px = this.X(x), py = this.Y(y);
      ctx.fillStyle = col || "#1f6feb"; ctx.fillRect(px, py - 5, 16, 3.5);
      ctx.fillStyle = "#222"; ctx.font = "11px Segoe UI"; ctx.textAlign = "left"; ctx.textBaseline = "middle"; ctx.fillText(label, px + 22, py - 3);
    },
    pie: function (cx, cy, r, a0, a1, col) {         // una porcion de pastel (angulos en GRADOS, a0>a1 = horario); el .cpd recorre las rebanadas
      var s = this.S(), ctx = s.ctx, pcx = this.X(cx), pcy = this.Y(cy), pr = r * s.scx;
      ctx.beginPath(); ctx.moveTo(pcx, pcy); ctx.arc(pcx, pcy, pr, -a0 * 0.0174533, -a1 * 0.0174533, false); ctx.closePath();
      ctx.fillStyle = col || "#1f6feb"; ctx.fill(); ctx.strokeStyle = "#fff"; ctx.lineWidth = 1.5; ctx.stroke();
    },
    compass: function (dx, dy, col) {               // flecha desde el ORIGEN (0,0) — campo polar/direccional
      this.quiver(0, 0, dx, dy, col || "#1f6feb");
    },

    // ===================== 3D (interactivo: arrastrar para orbitar) =====================
    view3: function (azd, eld) { var s = this.S(); s.az = azd * 0.0174533; s.el = eld * 0.0174533; },
    axis3: function (x0, x1, y0, y1, z0, z1) {
      var s = this.S(); s.mode = "3d"; s.prims = []; s.bb = [x0, x1, y0, y1, z0, z1];
    },
    fill3: function (p, t) { this.S().prims.push({ k: "face", p: p, c: this.jet(t) }); },
    line3: function (x1, y1, z1, x2, y2, z2, col) { this.S().prims.push({ k: "line3", p: [x1, y1, z1, x2, y2, z2], c: col }); },
    text3: function (x, y, z, str, col) { this.S().prims.push({ k: "text3", p: [x, y, z], s: str, c: col || "#333" }); },
    _pr: function (s, x, y, z) {                       // proyeccion ortografica con s.az, s.el
      var ca = Math.cos(s.az), sa = Math.sin(s.az), ce = Math.cos(s.el), se = Math.sin(s.el);
      var x1 = x * ca + y * sa, y1 = -x * sa + y * ca;
      return [x1, -(y1 * se) + z * ce, y1 * ce + z * se];
    },
    _fit: function (s) {                               // escala/centro segun bbox proyectada
      var b = s.bb, p = [], i, j, k;
      for (i = 0; i < 2; i++) for (j = 0; j < 2; j++) for (k = 0; k < 2; k++)
        p.push(this._pr(s, i ? b[1] : b[0], j ? b[3] : b[2], k ? b[5] : b[4]));
      var mnx = 1e9, mxx = -1e9, mny = 1e9, mxy = -1e9;
      for (i = 0; i < p.length; i++) { mnx = Math.min(mnx, p[i][0]); mxx = Math.max(mxx, p[i][0]); mny = Math.min(mny, p[i][1]); mxy = Math.max(mxy, p[i][1]); }
      var aW = s.w - 2 * s.padx - 80, aH = s.h - 2 * s.pady;
      s.sc3 = Math.min(aW / (mxx - mnx), aH / (mxy - mny));
      s.ox3 = s.padx + (aW - (mxx - mnx) * s.sc3) / 2 - mnx * s.sc3;
      s.oy3 = s.h - s.pady - (aH - (mxy - mny) * s.sc3) / 2 + mny * s.sc3;
    },
    _P: function (s, x, y, z) { var q = this._pr(s, x, y, z); return [s.ox3 + q[0] * s.sc3, s.oy3 - q[1] * s.sc3, q[2]]; },
    _draw3: function (s) {                             // re-dibuja toda la escena 3D
      var ctx = s.ctx, HkPlot = this, i, k, q;
      ctx.clearRect(0, 0, s.w, s.h);
      this._fit(s);
      var items = [];
      for (i = 0; i < s.prims.length; i++) {
        var o = s.prims[i];
        if (o.k === "face") {
          var poly = [], d = 0, n = o.p.length / 3;
          for (k = 0; k < n; k++) { q = HkPlot._P(s, o.p[3 * k], o.p[3 * k + 1], o.p[3 * k + 2]); poly.push(q[0], q[1]); d += q[2]; }
          items.push({ k: "face", poly: poly, depth: d / n, c: o.c });
        } else if (o.k === "line3") {
          var a = HkPlot._P(s, o.p[0], o.p[1], o.p[2]), b = HkPlot._P(s, o.p[3], o.p[4], o.p[5]);
          items.push({ k: "line", a: a, b: b, depth: (a[2] + b[2]) / 2, c: o.c });
        } else if (o.k === "text3") {
          q = HkPlot._P(s, o.p[0], o.p[1], o.p[2]); items.push({ k: "text", x: q[0], y: q[1], s: o.s, depth: q[2] + 1e6, c: o.c });
        }
      }
      items.sort(function (a, b) { return a.depth - b.depth; });
      for (i = 0; i < items.length; i++) {
        var t = items[i];
        if (t.k === "face") {
          ctx.beginPath(); ctx.moveTo(t.poly[0], t.poly[1]);
          for (k = 2; k < t.poly.length; k += 2) ctx.lineTo(t.poly[k], t.poly[k + 1]);
          ctx.closePath(); ctx.fillStyle = t.c; ctx.fill();
          ctx.strokeStyle = "rgba(0,0,0,0.22)"; ctx.lineWidth = 0.4; ctx.stroke();
        } else if (t.k === "line") {
          ctx.strokeStyle = t.c; ctx.lineWidth = 2.2; ctx.beginPath(); ctx.moveTo(t.a[0], t.a[1]); ctx.lineTo(t.b[0], t.b[1]); ctx.stroke();
        } else { ctx.fillStyle = t.c; ctx.font = "12px Segoe UI"; ctx.textAlign = "left"; ctx.textBaseline = "middle"; ctx.fillText(t.s, t.x + 4, t.y); }
      }
      for (i = 0; i < s.prims.length; i++) if (s.prims[i].k === "cbar") this._cbar(s, s.prims[i].a, s.prims[i].b);
      ctx.fillStyle = "#999"; ctx.font = "10px Segoe UI"; ctx.textAlign = "left"; ctx.textBaseline = "bottom";
      ctx.fillText("(arrastra para girar)", 6, s.h - 4);
    },
    render3: function () {                             // primer dibujo + handlers de rotacion
      var HkPlot = this, s = this.S(), cv = s.cv;
      HkPlot._draw3(s);
      cv.style.cursor = "grab";
      var drag = false, x0 = 0, y0 = 0, az0 = 0, el0 = 0;
      cv.onmousedown = function (e) { drag = true; x0 = e.clientX; y0 = e.clientY; az0 = s.az; el0 = s.el; cv.style.cursor = "grabbing"; e.preventDefault(); };
      cv.onmousemove = function (e) {
        if (!drag) return;
        s.az = az0 + (e.clientX - x0) * 0.01;
        s.el = el0 + (e.clientY - y0) * 0.01;
        if (s.el > 1.5) s.el = 1.5; if (s.el < -1.5) s.el = -1.5;
        HkPlot._draw3(s);
      };
      var up = function () { drag = false; cv.style.cursor = "grab"; };
      cv.onmouseup = up; cv.onmouseleave = up;
    },

    // ===================== STEPPER (botones "paso a paso") =====================
    // steps$("titulo") -> step$("titulo paso"; "texto") x N -> endsteps$
    // El usuario navega con los botones ◀ Anterior / Siguiente ▶. En el texto:
    //   *negrita*  y  //  = salto de linea. (sin " ; ' < > para no romper el .cpd)
    steps: function (title) {
      var wrap = document.createElement("div");
      wrap.style.cssText = "display:inline-block;vertical-align:top;width:560px;max-width:100%;margin:6px;border:1px solid #cfd6dd;border-radius:10px;background:#fff;font:13px Segoe UI;box-shadow:0 1px 4px rgba(0,0,0,0.08);overflow:hidden";
      var head = document.createElement("div");
      head.style.cssText = "background:#1f6feb;color:#fff;padding:8px 14px;font-weight:bold;font-size:14px";
      head.textContent = title || "Pasos";
      var body = document.createElement("div");
      body.style.cssText = "padding:14px 16px;min-height:96px;line-height:1.55;color:#222";
      var foot = document.createElement("div");
      foot.style.cssText = "display:flex;align-items:center;gap:10px;padding:8px 14px;border-top:1px solid #eee;background:#fafbfc";
      var bcss = "border:1px solid #1f6feb;border-radius:6px;padding:5px 14px;font:13px Segoe UI;cursor:pointer";
      var bprev = document.createElement("button"); bprev.style.cssText = bcss + ";background:#fff;color:#1f6feb"; bprev.textContent = "◀ Anterior";
      var bnext = document.createElement("button"); bnext.style.cssText = bcss + ";background:#1f6feb;color:#fff"; bnext.textContent = "Siguiente ▶";
      var dots = document.createElement("div"); dots.style.cssText = "flex:1;text-align:center;color:#9aa4ad;letter-spacing:2px";
      foot.appendChild(bprev); foot.appendChild(dots); foot.appendChild(bnext);
      wrap.appendChild(head); wrap.appendChild(body); wrap.appendChild(foot);
      var sc = document.currentScript;
      if (sc && sc.parentNode) sc.parentNode.insertBefore(wrap, sc); else document.body.appendChild(wrap);
      this._stp = { cards: [], cur: 0, body: body, dots: dots, prev: bprev, next: bnext };
    },
    step: function (t, html) { if (this._stp) this._stp.cards.push({ t: t, h: html }); },
    endsteps: function () {
      var st = this._stp; if (!st || !st.cards.length) return;
      function fmt(s) { return String(s).replace(/\*([^*]+)\*/g, "<b>$1</b>").replace(/\/\//g, "<br>"); }
      function show(i) {
        st.cur = Math.max(0, Math.min(st.cards.length - 1, i));
        var c = st.cards[st.cur];
        st.body.innerHTML = "<div style='font-weight:bold;color:#1f6feb;margin-bottom:7px;font-size:14px'>" + fmt(c.t) + "</div>" + fmt(c.h);
        var d = ""; for (var k = 0; k < st.cards.length; k++) d += (k === st.cur ? "●" : "○") + " ";
        st.dots.innerHTML = d + "<span style='margin-left:6px;font-size:12px;letter-spacing:0'>" + (st.cur + 1) + " / " + st.cards.length + "</span>";
        st.prev.style.opacity = st.cur === 0 ? 0.4 : 1;
        st.next.style.opacity = st.cur === st.cards.length - 1 ? 0.4 : 1;
      }
      st.prev.onclick = function () { show(st.cur - 1); };
      st.next.onclick = function () { show(st.cur + 1); };
      show(0);
      this._stp = null;
    }
  };
  global.HkPlot = HkPlot;
  global.ML = HkPlot;   // alias de compatibilidad (API anterior)
})(window);
