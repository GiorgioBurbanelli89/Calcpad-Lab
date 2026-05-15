% =============================================================================
%  PLACA RECTANGULAR ORTOTRÓPICA — Test 4 de awatif-v2
% =============================================================================
%
%  Mismo geometría que el test 3 pero con material ORTOTRÓPICO:
%    E_x ≠ E_y, G_xy independiente.
%  Distintos módulos elásticos en x e y producen flexión asimétrica.
%
%  awatif test:  C:\Users\j-b-j\Documents\awatif-v2\awatif-fem\src\deform.test.ts
%                test("Plate: Rectangular pin-supported plate with orthotropic material")
% =============================================================================

%% Parámetros del problema
a   = 10.0     % longitud en x [m]
b   = 10.0     % longitud en y [m]
h   = 0.15     % espesor [m]
p0  = -1000    % carga distribuida [N/m²]
Ex  = 1.0e10   % módulo elástico en x [Pa]
Ey  = 0.5e10   % módulo elástico en y [Pa]   ← MITAD que Ex (ortotrópico)
nu  = 0.25     % coeficiente de Poisson
Gxy = 0.4e10   % módulo de corte [Pa]

%% Generación del mesh — grid regular 10×10
nPts = 100;
x = zeros(1, nPts);
y = zeros(1, nPts);
for k = 1:nPts
    i = mod(k - 1, 10);
    j = (k - i - 1) / 10;
    x(k) = i * a / 9;
    y(k) = j * b / 9;
end

%% Triangulación Delaunay automática
tri = delaunay(x, y);

%% Solución analítica (Lekhnitskii) — placa ortotrópica con bordes simples apoyados
% w(x, y) ≈ (16·p₀ / π⁶·D_eq) · sin(πx/a) · sin(πy/b)
%
% Con rigidez equivalente para placa ortotrópica:
%   D_x  = Ex·h³ / [12(1-νx·νy)]
%   D_y  = Ey·h³ / [12(1-νx·νy)]
%   D_xy = Gxy·h³ / 12
%
% Para el primer modo, denominador efectivo:
%   D_eq = D_x/a⁴ + 2·H/(a²·b²) + D_y/b⁴
%   donde H = D_x·νy + 2·D_xy
nu_y = nu * Ey / Ex   % Poisson efectivo (Maxwell-Betti)
Dx   = Ex * h^3 / (12 * (1 - nu * nu_y))
Dy   = Ey * h^3 / (12 * (1 - nu * nu_y))
Dxy  = Gxy * h^3 / 12
H    = Dx * nu_y + 2 * Dxy

D_eq = Dx / a^4 + 2 * H / (a^2 * b^2) + Dy / b^4
coef = 16 * p0 / (pi^6 * D_eq)

%% Evaluación del campo de deflexiones
w = zeros(1, nPts);
for k = 1:nPts
    w(k) = coef * sin(pi * x(k) / a) * sin(pi * y(k) / b);
end

%% Deflexión máxima en milímetros
w_max_mm = abs(min(w)) * 1000

%% Visualización 2D
triplot(tri, x, y)

%% Visualización 3D — superficie deformada con colormap rainbow
trisurf(tri, x, y, w)
