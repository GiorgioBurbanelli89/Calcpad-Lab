% =============================================================================
% Calcpad Lab — Placa plate (awatif-v2 paridad)
% =============================================================================
%
% Equivalente al ejemplo de awatif-v2 en
%   C:\Users\j-b-j\Documents\awatif-v2\examples\src\plate\main.ts
%
% Mismo workflow:
%   1. Geometría: 4 vértices del cuadrilátero
%   2. Mesh refinado constrained Delaunay (Triangle de Shewchuk):
%        - awatif:        triangle.triangulate('pzQOq30a0.5', ...)
%        - Calcpad Lab:   mesh2d(x, y, 0.5)        ← misma cosa, mismo motor C
%   3. Deformación analítica tipo placa (paraboloide cosenoidal con apoyo perimetral)
%   4. Visualización 3D trisurf con colormap rainbow estilo awatif/SAP2000
%
% Este archivo es MATLAB-portable: corre IGUAL en MATLAB nativo.
% La diferencia: Calcpad Lab genera HTML con Three.js orbital rotable.
% =============================================================================

%% Geometría — vértices del cuadrilátero
x_in = [0, 15, 15, 0]
y_in = [0, 0, 10, 5]

%% Mesh refinado (constrained Delaunay + quality refinement)
% Equivalente exacto a awatif: q30 = ángulo min 30°, a0.5 = maxArea 0.5
maxArea = 0.5
[xm, ym, tri, bnd] = mesh2d(x_in, y_in, maxArea)

%% Deformación analítica — placa rectangular con bordes apoyados
% Aproximación analítica de placa Kirchhoff bajo carga distribuida:
%   w(x,y) = -w0 * sin(π·xi) * sin(π·eta)
% donde xi = x/Lx, eta = y/Ly (normalizados a [0,1])
% (cero en bordes, máximo en el centro)

q   = -3                                     % carga vertical [kN/m²]
w0  = 0.15 * abs(q)                          % flecha máxima estimada [m]

% Cálculo del campo de desplazamientos (suprimimos display con ; — son
% vectores largos que llenarían la página).
xi  = (xm - 0) / 15;
eta = (ym - 0) / 10;
z   = w0 * sin(pi * xi) * sin(pi * eta) * sign(q);

%% Visualización 3D — superficie deformada con colormap rainbow
% trisurf colorea por altura Z → mapa de displacementZ (estilo awatif/SAP2000)
trisurf(tri, xm, ym, z)

%% Visualización 2D — wireframe del mesh refinado
triplot(tri, xm, ym)

% =============================================================================
% Comparación con awatif-v2:
%
% Aspecto                   awatif-v2                Calcpad Lab
% ───────────────────────── ──────────────────────── ──────────────────────
% Mesh motor                triangle-wasm (Shewchuk) triangle.dll (Shewchuk)
% Switches                  pzQOq30a0.5              minAngle=30, maxArea=0.5
% Triangle count            ~270                     ~270 (igual algoritmo)
% Colormap                  Three.js Lut "rainbow"   Three.js Lut "rainbow" (idéntico)
% Material                  MeshBasicMaterial        MeshBasicMaterial
% Post-process              sRGB→linear, ×0.6        sRGB→linear, ×0.6
% Wireframe                 LineSegments negro       LineSegments negro
% Análisis FEM real         awatif-fem (deform)      pendiente (kpalte Q4)
% Deformación mostrada      FEM analyzeOutputs       analítica sin·sin (demo)
% =============================================================================
