/* hekatan-3d.js — motor 3D REAL con WebGL crudo sobre <canvas> (sin three.js, sin CDN).
   GPU, depth-test, perspectiva, orbita con el mouse. fill3 = caras; line3 = lineas
   (malla sin deformar, apoyos, flecha de carga, caja de ejes). Embebible inline en figure3$. */
(function (global) {
  var Hk3D = {
    st: null,
    etabs: false,   // true = paleta de bandas DISCRETAS de ETABS; false = jet_r suave
    // paleta de contorno de ETABS — INGENIERIA INVERSA (15 bandas muestreadas de la
    // leyenda real, capture_20260624_174436): magenta(min) -> rojo -> amarillo -> verde -> azul(max)
    pal: [[0.784,0,0.784],[0.894,0,0.392],[1,0,0],[1,0.251,0],[1,0.502,0],[1,0.667,0],
          [1,0.831,0],[1,1,0],[0.502,1,0],[0,1,0],[0,1,0.502],[0,1,1],[0,0.667,1],[0,0.333,1],[0,0,1]],
    _ins: function (el) {                       // inserta un elemento donde esta el <script> actual
      var sc = document.currentScript;
      if (sc && sc.parentNode) sc.parentNode.insertBefore(el, sc); else document.body.appendChild(el);
    },
    figure3: function (id, w, h) {              // el .js CREA el canvas WebGL + un overlay 2D para el texto
      var wrap = document.createElement("div");
      wrap.style.cssText = "position:relative;display:inline-block;vertical-align:top;margin:4px";
      var cv = document.createElement("canvas"); cv.id = id; cv.style.cssText = "display:block;border:1px solid #ccc";
      var ov = document.createElement("canvas"); ov.style.cssText = "position:absolute;left:0;top:0;pointer-events:none";
      var tip = document.createElement("div");   // datatip tipo MATLAB (sigue al cursor, sobre la malla)
      tip.style.cssText = "position:absolute;pointer-events:none;display:none;background:rgba(25,28,32,0.9);color:#fff;font:11px Segoe UI;padding:3px 7px;border-radius:4px;white-space:nowrap;transform:translate(12px,-50%);z-index:5;box-shadow:0 1px 4px rgba(0,0,0,0.3)";
      wrap.appendChild(cv); wrap.appendChild(ov); wrap.appendChild(tip);
      var row = document.createElement("div");   // fila flex: canvas + colorbar SIEMPRE en la misma línea
      row.style.cssText = "display:flex;align-items:flex-start;flex-wrap:nowrap";
      this._ins(row); row.appendChild(wrap);
      var dpr = Math.max(2, window.devicePixelRatio || 1);   // render a >=2x para que no pixele al acercar
      cv.width = w * dpr; cv.height = h * dpr; cv.style.width = w + "px"; cv.style.height = h + "px";
      ov.width = w * dpr; ov.height = h * dpr; ov.style.width = w + "px"; ov.style.height = h + "px";
      var octx = ov.getContext("2d"); octx.scale(dpr, dpr);
      var opt = { preserveDrawingBuffer: true, antialias: true };
      var gl = cv.getContext("webgl", opt) || cv.getContext("experimental-webgl", opt);
      this.st = { gl: gl, cv: cv, octx: octx, w: w, h: h, row: row, verts: [], lverts: [], cverts: [], tverts: [], ticks: [], dpts: [], tip: tip, tipOn: false, tipLabel: "", az: 0.8, el: 0.5, bb: [0, 1, 0, 1, 0, 1], dist: 3 };
      gl.viewport(0, 0, cv.width, cv.height); gl.enable(gl.DEPTH_TEST); gl.clearColor(1, 1, 1, 1);
    },
    tick3: function (x, y, z, t) { this.st.ticks.push({ x: x, y: y, z: z, t: "" + t }); },   // texto 3D (numero/etiqueta) que sigue la rotacion
    datatip: function (label) { this.st.tipOn = true; this.st.tipLabel = "" + label; },      // activa el datatip (hover) tipo MATLAB datacursormode
    datapoint: function (x, y, z, v) { this.st.dpts.push({ x: x, y: y, z: z, v: v }); },      // registra un nudo y su valor para el hover
    cartesian3: function (x0, x1, y0, y1, z0, z1, ndx, ndz) {   // plano cartesiano 3D: caja + numeros X/Z (1 llamada)
      var L = this.st.lverts, c = this.hex("cfd4d8"), i, dx = x1 - x0, dz = z1 - z0;
      function ln(ax, ay, az, bx, by, bz) { L.push(ax, ay, az, c[0], c[1], c[2], bx, by, bz, c[0], c[1], c[2]); }
      ln(x0, y0, z0, x1, y0, z0); ln(x1, y0, z0, x1, y1, z0); ln(x1, y1, z0, x0, y1, z0); ln(x0, y1, z0, x0, y0, z0);
      ln(x0, y0, z1, x1, y0, z1); ln(x1, y0, z1, x1, y1, z1); ln(x1, y1, z1, x0, y1, z1); ln(x0, y1, z1, x0, y0, z1);
      ln(x0, y0, z0, x0, y0, z1); ln(x1, y0, z0, x1, y0, z1); ln(x1, y1, z0, x1, y1, z1); ln(x0, y1, z0, x0, y1, z1);
      for (i = 0; i <= ndx; i++) { var xv = x0 + i * dx / ndx; this.tick3(xv, y0, z0 - dz * 0.05, Math.round(xv)); }
      for (i = 0; i <= ndz; i++) { var zv = z0 + i * dz / ndz; this.tick3(x0 - dx * 0.06, y0, zv, Math.round(zv)); }
      this.tick3((x0 + x1) / 2, y0, z0 - dz * 0.13, "X (ancho, m)");
      this.tick3(x0 - dx * 0.15, y0, (z0 + z1) / 2, "Z (altura, m)");
    },
    _proj2: function (s, x, y, z) {              // proyecta un punto 3D a pixeles de pantalla (con la MVP actual)
      var m = s.mvp, cw = m[3] * x + m[7] * y + m[11] * z + m[15];
      var cx = m[0] * x + m[4] * y + m[8] * z + m[12], cy = m[1] * x + m[5] * y + m[9] * z + m[13];
      return [(cx / cw * 0.5 + 0.5) * s.w, (1 - (cy / cw * 0.5 + 0.5)) * s.h];
    },
    _nf: function (v) {                          // formato: notacion cientifica para valores muy chicos/grandes
      var a = Math.abs(v);
      if (a < 1e-12) return "0";
      if (a >= 1e4 || a < 1e-3) return v.toExponential(2);
      if (a < 1) return parseFloat(v.toPrecision(3)).toString();
      return v.toFixed(2);
    },
    colorbar3: function (vmin, vmax, h) {        // el .js CREA el <div> de la barra de color
      var d = document.createElement("div");
      d.style.cssText = "display:inline-flex;vertical-align:top;margin:6px 0 0 10px;font:11px Segoe UI;color:#333";
      function rgb(c){ return "rgb(" + Math.round(c[0]*255) + "," + Math.round(c[1]*255) + "," + Math.round(c[2]*255) + ")"; }
      var grad, lab = "", k, i;
      if (this.etabs) {                          // BANDAS DISCRETAS con la paleta ETABS (15 colores)
        var N = 15, stops = [];
        for (i = 0; i < N; i++) {                 // cada banda = color solido de la paleta, con corte duro
          var col = rgb(this.pal[i]), p0 = (i / N * 100).toFixed(2), p1 = ((i + 1) / N * 100).toFixed(2);
          stops.push(col + " " + p0 + "%", col + " " + p1 + "%");
        }
        grad = "linear-gradient(to top," + stops.join(",") + ")";
        for (k = N; k >= 0; k--) lab += "<span>" + this._nf(vmin + k / N * (vmax - vmin)) + "</span>";
      } else {                                    // jet suave (didactico)
        grad = "linear-gradient(to top,rgb(128,0,0),rgb(255,0,0),rgb(255,128,0),rgb(255,255,0),rgb(120,255,120),rgb(0,220,255),rgb(0,120,255),rgb(0,0,255),rgb(0,0,140))";
        for (k = 4; k >= 0; k--) lab += "<span>" + this._nf(vmin + k / 4 * (vmax - vmin)) + "</span>";
      }
      d.innerHTML = '<div style="width:18px;height:' + h + 'px;background:' + grad + ';border:1px solid #444"></div>'
        + '<div style="display:flex;flex-direction:column;justify-content:space-between;height:' + h + 'px;margin-left:5px">' + lab + '</div>';
      if (this.st && this.st.row) this.st.row.appendChild(d); else this._ins(d);   // misma fila que el canvas
    },
    view3: function (azd, eld) { var a = azd * 0.0174533, e = eld * 0.0174533; this.st.az = a; this.st.el = e; this.st.rot = this._mul(this._rotX(e - 1.5707963), this._rotZ(-a)); },   // azimut (rotZ) PRIMERO, luego elevacion (rotX): columna vertical sin ladeo, estilo MATLAB view(az,el)
    axis3: function (x0, x1, y0, y1, z0, z1) {
      this.st.bb = [x0, x1, y0, y1, z0, z1]; this.st.verts = []; this.st.lverts = []; this.st.cverts = []; this.st.tverts = []; this.st.ticks = []; this.st.dpts = []; this.st.tipOn = false;
      var dx = x1 - x0, dy = y1 - y0, dz = z1 - z0; this.st.dist = 1.7 * Math.sqrt(dx * dx + dy * dy + dz * dz);
    },
    jet: function (t) {
      t = Math.max(0, Math.min(1, t)); var u = 1 - t;
      function c(z) { return Math.max(0, Math.min(1, 1.5 - Math.abs(4 * u - z))); }
      return [c(3), c(2), c(1)];
    },
    hex: function (s) {
      if (s.charCodeAt(0) === 35) s = s.substr(1);
      if (s.length === 3) s = s[0] + s[0] + s[1] + s[1] + s[2] + s[2];
      return [parseInt(s.substr(0, 2), 16) / 255, parseInt(s.substr(2, 2), 16) / 255, parseInt(s.substr(4, 2), 16) / 255];
    },
    fill3: function (p, t1, t2, t3, t4) {        // guarda el ESCALAR t por vertice (el shader hace jet suave O bandas ETABS)
      if (t2 === undefined) { t2 = t1; t3 = t1; t4 = t1; }
      var tt = [t1, t2, t3, t4], idx = [0, 1, 2, 0, 2, 3], i, k, t, V = this.st.verts;
      for (i = 0; i < 6; i++) { k = idx[i]; t = tt[k]; V.push(p[3 * k], p[3 * k + 1], p[3 * k + 2], t, t, t); }
    },
    fill3c: function (p, col) {                   // cara cuadrilatera de COLOR SOLIDO (= patch 'FaceColor' de MATLAB)
      var c = this.hex(col), idx = [0, 1, 2, 0, 2, 3], i, k, V = this.st.cverts;
      for (i = 0; i < 6; i++) { k = idx[i]; V.push(p[3 * k], p[3 * k + 1], p[3 * k + 2], c[0], c[1], c[2]); }
    },
    cone3: function (cx, cy, z0, z1, r, col) {     // cono color solido (perfil de radios [r 0], como cylinder de MATLAB)
      var c = this.hex(col), V = this.st.cverts, n = 24, i;
      for (i = 0; i < n; i++) {
        var a0 = 2 * Math.PI * i / n, a1 = 2 * Math.PI * (i + 1) / n;
        var x0 = cx + r * Math.cos(a0), y0 = cy + r * Math.sin(a0);
        var x1 = cx + r * Math.cos(a1), y1 = cy + r * Math.sin(a1);
        V.push(x0, y0, z0, c[0], c[1], c[2], x1, y1, z0, c[0], c[1], c[2], cx, cy, z1, c[0], c[1], c[2]);
      }
    },
    fill3a: function (p, col) {                   // cara TRANSLUCIDA de color (= patch 'FaceAlpha' de MATLAB)
      var c = this.hex(col), idx = [0, 1, 2, 0, 2, 3], i, k, V = this.st.tverts;
      for (i = 0; i < 6; i++) { k = idx[i]; V.push(p[3 * k], p[3 * k + 1], p[3 * k + 2], c[0], c[1], c[2]); }
    },
    cone3a: function (cx, cy, z0, z1, r, col) {    // cono TRANSLUCIDO (cono de dano del concreto)
      var c = this.hex(col), V = this.st.tverts, n = 24, i;
      for (i = 0; i < n; i++) {
        var a0 = 2 * Math.PI * i / n, a1 = 2 * Math.PI * (i + 1) / n;
        var x0 = cx + r * Math.cos(a0), y0 = cy + r * Math.sin(a0);
        var x1 = cx + r * Math.cos(a1), y1 = cy + r * Math.sin(a1);
        V.push(x0, y0, z0, c[0], c[1], c[2], x1, y1, z0, c[0], c[1], c[2], cx, cy, z1, c[0], c[1], c[2]);
      }
    },
    line3: function (x1, y1, z1, x2, y2, z2, col) {
      var c = this.hex(col), L = this.st.lverts;
      L.push(x1, y1, z1, c[0], c[1], c[2], x2, y2, z2, c[0], c[1], c[2]);
    },
    point3: function (x, y, z, t, sz) {          // scatter3: marcador 3D = 3 quads cruzados (visible desde cualquier angulo)
      var b = this.st.bb, d = Math.max(b[1] - b[0], b[3] - b[2], b[5] - b[4]) * (sz || 0.015);
      this.fill3([x - d, y - d, z, x + d, y - d, z, x + d, y + d, z, x - d, y + d, z], t, t, t, t);  // plano XY
      this.fill3([x - d, y, z - d, x + d, y, z - d, x + d, y, z + d, x - d, y, z + d], t, t, t, t);  // plano XZ
      this.fill3([x, y - d, z - d, x, y + d, z - d, x, y + d, z + d, x, y - d, z + d], t, t, t, t);  // plano YZ
    },
    quiver3: function (x, y, z, dx, dy, dz, col) {  // vector 3D: tallo + cabeza de flecha (2 lineas)
      this.line3(x, y, z, x + dx, y + dy, z + dz, col);
      var L = Math.sqrt(dx * dx + dy * dy + dz * dz) || 1, ux = dx / L, uy = dy / L, uz = dz / L, h = L * 0.22;
      var ax = x + dx, ay = y + dy, az = z + dz, px = -uy, py = ux, pz = 0, pl = Math.sqrt(px * px + py * py + pz * pz);
      if (pl < 1e-6) { px = 0; py = -uz; pz = uy; pl = Math.sqrt(py * py + pz * pz) || 1; } px /= pl; py /= pl; pz /= pl;
      this.line3(ax, ay, az, ax - ux * h + px * h * 0.5, ay - uy * h + py * h * 0.5, az - uz * h + pz * h * 0.5, col);
      this.line3(ax, ay, az, ax - ux * h - px * h * 0.5, ay - uy * h - py * h * 0.5, az - uz * h - pz * h * 0.5, col);
    },
    stem3: function (x, y, z, t) {               // tallo 3D vertical (z=0 -> z) + marcador
      this.line3(x, y, 0, x, y, z, "808080"); this.point3(x, y, z, t);
    },
    tri3: function (x1, y1, z1, x2, y2, z2, x3, y3, z3, t1, t2, t3) {  // trisurf/trimesh: cara TRIANGULAR (4o vertice = 3o, degenerado)
      this.fill3([x1, y1, z1, x2, y2, z2, x3, y3, z3, x3, y3, z3], t1, t2, t3 === undefined ? t1 : t3, t3 === undefined ? t1 : t3);
    },
    sphere: function (cx, cy, cz, r, t, nu, nv) {  // esfera completa (loops lat/long -> fill3)
      nu = nu || 16; nv = nv || 10; var i, j, GL = this;
      function P(u, v) { var th = u / nu * 6.28318, ph = v / nv * 3.14159; return [cx + r * Math.sin(ph) * Math.cos(th), cy + r * Math.sin(ph) * Math.sin(th), cz + r * Math.cos(ph)]; }
      for (j = 0; j < nv; j++) for (i = 0; i < nu; i++) { var a = P(i, j), b = P(i + 1, j), c = P(i + 1, j + 1), d = P(i, j + 1); GL.fill3([a[0], a[1], a[2], b[0], b[1], b[2], c[0], c[1], c[2], d[0], d[1], d[2]], t, t, t, t); }
    },
    cylinder: function (cx, cy, z0, z1, r, t, nu) {  // cilindro (cara lateral) -> fill3
      nu = nu || 20; var i; for (i = 0; i < nu; i++) { var a0 = i / nu * 6.28318, a1 = (i + 1) / nu * 6.28318, x0 = cx + r * Math.cos(a0), y0 = cy + r * Math.sin(a0), x1 = cx + r * Math.cos(a1), y1 = cy + r * Math.sin(a1); this.fill3([x0, y0, z0, x1, y1, z0, x1, y1, z1, x0, y0, z1], t, t, t, t); }
    },
    cylinder3c: function (cx, cy, z0, z1, r, col, nu) {  // cilindro de COLOR SOLIDO (pernos/varillas grises, como surf 'FaceColor' de MATLAB)
      nu = nu || 18; for (var i = 0; i < nu; i++) { var a0 = i / nu * 6.28318, a1 = (i + 1) / nu * 6.28318, x0 = cx + r * Math.cos(a0), y0 = cy + r * Math.sin(a0), x1 = cx + r * Math.cos(a1), y1 = cy + r * Math.sin(a1); this.fill3c([x0, y0, z0, x1, y1, z0, x1, y1, z1, x0, y0, z1], col); }
    },
    light: false,                                          // iluminacion 3D (Hk3D.lighting / lighting$ en el .cpd)
    lighting: function (on) { this.light = !(on === 0 || on === "off" || on === "none" || on === false); },
    shading: function (m) { this.light = !(m === "none" || m === "off" || m === 0); },   // flat/interp -> con luz
    render3: function () {
      var s = this.st, gl = s.gl, Hk3D = this;
      gl.getExtension("OES_standard_derivatives");          // normal por derivadas de pantalla (para iluminar)
      var vs = "attribute vec3 a;attribute vec3 c;uniform mat4 m;varying vec3 v;varying vec3 vp;void main(){gl_Position=m*vec4(a,1.0);v=c;vp=a;}";
      // uMode: 0=jet suave · 1=bandas DISCRETAS con la paleta ETABS · 2=linea (rgb directo). uLight: 0/1 iluminacion difusa.
      var fs = "#extension GL_OES_standard_derivatives : enable\n"
        + "precision mediump float;varying vec3 v;varying vec3 vp;uniform float uMode;uniform float uLight;uniform float uAlpha;uniform vec3 pal[15];"
        + "void main(){"
        + " if(uMode>2.5){vec3 cc=v; if(uLight>0.5){vec3 Nn=normalize(cross(dFdx(vp),dFdy(vp)));vec3 Ll=normalize(vec3(0.4,0.5,0.85));cc=cc*(0.55+0.45*abs(dot(Nn,Ll)));} gl_FragColor=vec4(cc,uAlpha);return;}" // modo 3: rgb solido + luz + alpha
        + " if(uMode>1.5){gl_FragColor=vec4(v,1.0);return;}" // lineas: rgb directo, sin luz
        + " float t=clamp(v.r,0.0,1.0); vec3 col;"
        + " if(uMode>0.5){"                                  // BANDAS ETABS: indexar la paleta de 15 colores
        + "   int bi=int(clamp(floor(t*15.0),0.0,14.0)); col=pal[0];"
        + "   for(int i=0;i<15;i++){ if(i<=bi) col=pal[i]; } }"
        + " else { float u=1.0-t;"                           // jet suave (didactico)
        + "   col=vec3(clamp(1.5-abs(4.0*u-3.0),0.0,1.0),clamp(1.5-abs(4.0*u-2.0),0.0,1.0),clamp(1.5-abs(4.0*u-1.0),0.0,1.0)); }"
        + " if(uLight>0.5){"                                 // luz difusa: normal de la cara = cross de las derivadas
        + "   vec3 N=normalize(cross(dFdx(vp),dFdy(vp))); vec3 L=normalize(vec3(0.4,0.5,0.85));"
        + "   col=col*(0.55+0.45*abs(dot(N,L))); }"          // dos caras (abs): ambiente 0.55 + difuso 0.45
        + " gl_FragColor=vec4(col,1.0);}";
      function sh(t, src) { var o = gl.createShader(t); gl.shaderSource(o, src); gl.compileShader(o); return o; }
      var pr = gl.createProgram();
      gl.attachShader(pr, sh(gl.VERTEX_SHADER, vs)); gl.attachShader(pr, sh(gl.FRAGMENT_SHADER, fs));
      gl.linkProgram(pr); gl.useProgram(pr); s.pr = pr;
      s.um = gl.getUniformLocation(pr, "m"); s.la = gl.getAttribLocation(pr, "a"); s.lc = gl.getAttribLocation(pr, "c");
      s.umode = gl.getUniformLocation(pr, "uMode"); s.ulight = gl.getUniformLocation(pr, "uLight"); s.ualpha = gl.getUniformLocation(pr, "uAlpha");
      gl.enable(gl.BLEND); gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);   // para caras translucidas
      var P = this.pal, pf = []; for (var pi = 0; pi < 15; pi++) { pf.push(P[pi][0], P[pi][1], P[pi][2]); }
      gl.uniform3fv(gl.getUniformLocation(pr, "pal"), new Float32Array(pf));
      s.tb = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, s.tb); gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(s.verts), gl.STATIC_DRAW); s.nv = s.verts.length / 6;
      s.lb = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, s.lb); gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(s.lverts), gl.STATIC_DRAW); s.nl = s.lverts.length / 6;
      s.cb = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, s.cb); gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(s.cverts), gl.STATIC_DRAW); s.ncf = s.cverts.length / 6;
      s.tb2 = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, s.tb2); gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(s.tverts), gl.STATIC_DRAW); s.ntf = s.tverts.length / 6;
      var draw = function () { Hk3D._draw(s); }; s.draw = draw; draw();
      var cv = s.cv, drag = false, panning = false, px = 0, py = 0; cv.style.cursor = "grab";
      cv.oncontextmenu = function (e) { e.preventDefault(); };   // permitir arrastrar con boton derecho (pan)
      cv.onmousedown = function (e) { drag = true; panning = e.shiftKey || e.button === 2; px = e.clientX; py = e.clientY; cv.style.cursor = panning ? "move" : "grabbing"; e.preventDefault(); };
      cv.onmousemove = function (e) {
        if (drag && panning) {                        // PAN (shift o boton derecho): desplaza el centro en pantalla
          s.panx = (s.panx || 0) + (e.clientX - px) * s.dist * 0.0022; s.pany = (s.pany || 0) - (e.clientY - py) * s.dist * 0.0022;
          px = e.clientX; py = e.clientY; draw(); return;
        }
        if (drag) {                                   // TRACKBALL: rotación incremental en TODOS los sentidos
          var dx = (e.clientX - px) * 0.01, dy = (e.clientY - py) * 0.01; px = e.clientX; py = e.clientY;
          var dR = Hk3D._mul(Hk3D._rotX(dy), Hk3D._rotY(dx));
          s.rot = Hk3D._mul(dR, s.rot); draw(); return;
        }
        if (!s.tipOn || !s.dpts.length || !s.mvp) return;   // hover: nudo más cercano al cursor → datatip
        var r = cv.getBoundingClientRect(), mx = e.clientX - r.left, my = e.clientY - r.top, best = -1, bd = 324, i, p, dx, dy;
        for (i = 0; i < s.dpts.length; i++) { p = Hk3D._proj2(s, s.dpts[i].x, s.dpts[i].y, s.dpts[i].z); dx = p[0] - mx; dy = p[1] - my; if (dx * dx + dy * dy < bd) { bd = dx * dx + dy * dy; best = i; } }
        if (best >= 0) { var dp = s.dpts[best], pp = Hk3D._proj2(s, dp.x, dp.y, dp.z); s.tip.style.display = "block"; s.tip.style.left = pp[0] + "px"; s.tip.style.top = pp[1] + "px"; s.tip.innerHTML = (s.tipLabel ? s.tipLabel + " = " : "") + Hk3D._nf(dp.v); }
        else s.tip.style.display = "none";
      };
      var up = function () { drag = false; cv.style.cursor = "grab"; if (s.tip) s.tip.style.display = "none"; };
      cv.onmouseup = up; cv.onmouseleave = up;
      cv.onwheel = function (e) { s.dist *= e.deltaY > 0 ? 1.1 : 0.9; draw(); e.preventDefault(); };
    },
    _attr: function (s, b) {
      var gl = s.gl; gl.bindBuffer(gl.ARRAY_BUFFER, b);
      gl.enableVertexAttribArray(s.la); gl.vertexAttribPointer(s.la, 3, gl.FLOAT, false, 24, 0);
      gl.enableVertexAttribArray(s.lc); gl.vertexAttribPointer(s.lc, 3, gl.FLOAT, false, 24, 12);
    },
    _draw: function (s) {
      var gl = s.gl; gl.clearColor(0.07, 0.08, 0.10, 1.0); gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);   // fondo oscuro (los translucidos resaltan)
      var b = s.bb, cx = (b[0] + b[1]) / 2, cy = (b[2] + b[3]) / 2, cz = (b[4] + b[5]) / 2;
      // TRACKBALL: rotación libre acumulada en s.rot (todos los sentidos). V = pull-back · rot · centrar.
      if (!s.rot) s.rot = this._mul(this._rotX(-1.2566), this._rotZ(-0.6632));   // vista iso inicial (modelo Z-up): el=18, az=38 (rotX·rotZ, sin ladeo)
      var V = this._mul(this._trans(s.panx || 0, s.pany || 0, -s.dist), this._mul(s.rot, this._trans(-cx, -cy, -cz)));
      var P = this._persp(0.8, s.w / s.h, 0.01, s.dist * 12);
      s.mvp = this._mul(P, V);
      gl.uniformMatrix4fv(s.um, false, s.mvp);
      gl.uniform1f(s.umode, Hk3D.etabs ? 1 : 0);                 // caras: jet suave o bandas ETABS
      gl.uniform1f(s.ulight, Hk3D.light ? 1 : 0);                // iluminacion difusa on/off
      gl.uniform1f(s.ualpha, 1.0);                              // opaco por defecto
      this._attr(s, s.tb); gl.drawArrays(gl.TRIANGLES, 0, s.nv);
      if (s.ncf > 0) { gl.uniform1f(s.umode, 3); gl.uniform1f(s.ulight, 1); this._attr(s, s.cb); gl.drawArrays(gl.TRIANGLES, 0, s.ncf); }  // caras SOLIDAS rgb + luz (fill3c/cone3)
      if (s.nl > 0) { gl.uniform1f(s.umode, 2); this._attr(s, s.lb); gl.drawArrays(gl.LINES, 0, s.nl); }  // lineas: rgb directo
      if (s.ntf > 0) { gl.uniform1f(s.umode, 3); gl.uniform1f(s.ulight, 1); gl.uniform1f(s.ualpha, 0.13); gl.depthMask(false); this._attr(s, s.tb2); gl.drawArrays(gl.TRIANGLES, 0, s.ntf); gl.depthMask(true); gl.uniform1f(s.ualpha, 1.0); }  // caras TRANSLUCIDAS al final (blending)
      var oc = s.octx; oc.clearRect(0, 0, s.w, s.h);
      if (s.ticks.length) {
        oc.fillStyle = "#444"; oc.font = "11px Segoe UI"; oc.textAlign = "center"; oc.textBaseline = "middle";
        for (var i = 0; i < s.ticks.length; i++) { var k = s.ticks[i], p = this._proj2(s, k.x, k.y, k.z); oc.fillText(k.t, p[0], p[1]); }
      }
    },
    _persp: function (fy, a, n, f) {
      var t = 1 / Math.tan(fy / 2), nf = 1 / (n - f);
      return [t / a, 0, 0, 0, 0, t, 0, 0, 0, 0, (f + n) * nf, -1, 0, 0, 2 * f * n * nf, 0];
    },
    _look: function (e, c, u) {
      var z0 = e[0] - c[0], z1 = e[1] - c[1], z2 = e[2] - c[2], zl = 1 / Math.sqrt(z0 * z0 + z1 * z1 + z2 * z2);
      z0 *= zl; z1 *= zl; z2 *= zl;
      var x0 = u[1] * z2 - u[2] * z1, x1 = u[2] * z0 - u[0] * z2, x2 = u[0] * z1 - u[1] * z0, xl = Math.sqrt(x0 * x0 + x1 * x1 + x2 * x2);
      if (xl) { xl = 1 / xl; x0 *= xl; x1 *= xl; x2 *= xl; }
      var y0 = z1 * x2 - z2 * x1, y1 = z2 * x0 - z0 * x2, y2 = z0 * x1 - z1 * x0;
      return [x0, y0, z0, 0, x1, y1, z1, 0, x2, y2, z2, 0, -(x0 * e[0] + x1 * e[1] + x2 * e[2]), -(y0 * e[0] + y1 * e[1] + y2 * e[2]), -(z0 * e[0] + z1 * e[1] + z2 * e[2]), 1];
    },
    _mul: function (a, b) {
      var o = new Array(16), c, r, k;
      for (c = 0; c < 4; c++) for (r = 0; r < 4; r++) { var s = 0; for (k = 0; k < 4; k++) s += a[k * 4 + r] * b[c * 4 + k]; o[c * 4 + r] = s; }
      return o;
    },
    _trans: function (x, y, z) { return [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, x, y, z, 1]; },          // column-major
    _rotX: function (a) { var c = Math.cos(a), s = Math.sin(a); return [1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1]; },
    _rotY: function (a) { var c = Math.cos(a), s = Math.sin(a); return [c, 0, -s, 0, 0, 1, 0, 0, s, 0, c, 0, 0, 0, 0, 1]; },
    _rotZ: function (a) { var c = Math.cos(a), s = Math.sin(a); return [c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]; }
  };
  global.Hk3D = Hk3D;
  global.GL3 = Hk3D;   // alias de compatibilidad (API anterior)
})(window);
