% =============================================================================
%  PLACA RECTANGULAR ORTOTROPICA - Test 4 de awatif-v2
% =============================================================================
%
%  Mismo geometria que el test 3 pero con material ORTOTROPICO:
%    E_x != E_y, G_xy independiente.
%  Distintos modulos elasticos en x e y producen flexion asimetrica.
%
%  awatif test:  C:\Users\j-b-j\Documents\awatif-v2\awatif-fem\src\deform.test.ts
%                test("Plate: Rectangular pin-supported plate with orthotropic material")
% =============================================================================

%% Parametros del problema
a   = 10.0     % longitud en x [m]
b   = 10.0     % longitud en y [m]
h   = 0.15     % espesor [m]
p0  = -1000    % carga distribuida [N/m2]
Ex  = 1.0e10   % modulo elastico en x [Pa]
Ey  = 0.5e10   % modulo elastico en y [Pa]   <- MITAD que Ex (ortotropico)
nu  = 0.25     % coeficiente de Poisson
Gxy = 0.4e10   % modulo de corte [Pa]

%% Generacion del mesh - grid regular 10x10
nPts = 100;
x = zeros(1, nPts);
y = zeros(1, nPts);
for k = 1:nPts
    i = mod(k - 1, 10);
    j = (k - i - 1) / 10;
    x(k) = i * a / 9;
    y(k) = j * b / 9;
end

%% Triangulacion Delaunay automatica
tri = delaunay(x, y);

%% Solucion analitica (Lekhnitskii) - placa ortotropica con bordes simples apoyados
% w(x, y) ~= (16*p0 / pi6*D_eq) * sin(pix/a) * sin(piy/b)
%
% Con rigidez equivalente para placa ortotropica:
%   D_x  = Ex*h3 / [12(1-nux*nuy)]
%   D_y  = Ey*h3 / [12(1-nux*nuy)]
%   D_xy = Gxy*h3 / 12
%
% Para el primer modo, denominador efectivo:
%   D_eq = D_x/a4 + 2*H/(a2*b2) + D_y/b4
%   donde H = D_x*nuy + 2*D_xy
nu_y = nu * Ey / Ex   % Poisson efectivo (Maxwell-Betti)
Dx   = Ex * h^3 / (12 * (1 - nu * nu_y))
Dy   = Ey * h^3 / (12 * (1 - nu * nu_y))
Dxy  = Gxy * h^3 / 12
H    = Dx * nu_y + 2 * Dxy

D_eq = Dx / a^4 + 2 * H / (a^2 * b^2) + Dy / b^4
coef = 16 * p0 / (pi^6 * D_eq)

%% Evaluacion del campo de deflexiones
w = zeros(1, nPts);
for k = 1:nPts
    w(k) = coef * sin(pi * x(k) / a) * sin(pi * y(k) / b);
end

%% Deflexion maxima en milimetros
w_max_mm = abs(min(w)) * 1000

%% Visualizacion 2D
% triplot(tri, x, y)

%% Visualizacion 3D - superficie deformada con colormap rainbow
trisurf(tri, x, y, w)
