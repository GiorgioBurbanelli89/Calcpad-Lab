% =============================================================================
%  PLACA CUADRADA EMPOTRADA EN BORDES CON CARGA UNIFORME
% =============================================================================
%
%  Benchmark portado de awatif-v2:
%     C:\Users\j-b-j\Documents\awatif-v2\awatif-fem\src\deform.test.ts
%     test("Plate: Rectangular pin-supported plate with uniform load")
%
%  Este script es MATLAB 100% portable:
%     - Corre en MATLAB nativo  → muestra figuras con colormap jet (SAP2000-style)
%     - Corre en Calcpad Lab    → HTML con Three.js rainbow (mismo look)
%     - Corre en Octave         → figuras con colormap jet
%
%  Geometría:    placa cuadrada 10×10 m, espesor 0.15 m
%  Material:     E=10 GPa, ν=0.25, isotrópica
%  Carga:        p₀ = -1000 N/m² (uniforme transversal)
%  Apoyos:       fijos en los 4 bordes
%  Deflexión:    max ≈ 12.69 mm (FEM awatif)
%                       13.541 mm (solución analítica Timoshenko exacta)
% =============================================================================

%% Parámetros del problema
a  = 10.0    % longitud en x [m]
b  = 10.0    % longitud en y [m]
h  = 0.15    % espesor [m]
p0 = -1000   % carga distribuida [N/m²]
E  = 1.0e10  % módulo elástico [Pa]
nu = 0.25    % coeficiente de Poisson

%% Generación del mesh — grid regular 10×10 (100 nodos)
nPts = 100;
x = zeros(1, nPts);
y = zeros(1, nPts);
for k = 1:nPts
    i = mod(k - 1, 10);
    j = (k - i - 1) / 10;
    x(k) = i * a / 9;
    y(k) = j * b / 9;
end

%% Triangulación Delaunay automática (~162 triángulos)
tri = delaunay(x, y);

%% Solución analítica de Timoshenko — serie de Fourier completa
% w(x, y) = (16·p₀ / π⁶·D) · Σ_{m,n impares} sin(mπx/a)·sin(nπy/b) / [m·n·(m²/a² + n²/b²)²]
%
% Sumamos m, n ∈ {1, 3, 5, 7} (4×4 = 16 términos) — converge rápido a la
% solución exacta Timoshenko 13.541 mm.
D = E * h^3 / (12 * (1 - nu^2))

w = zeros(1, nPts);
for k = 1:nPts
    s = 0;
    for m = 1:2:7
        for n = 1:2:7
            num = sin(m*pi*x(k)/a) * sin(n*pi*y(k)/b);
            den = m * n * (m^2/a^2 + n^2/b^2)^2;
            s = s + num / den;
        end;
    end;
    w(k) = 16 * p0 / (pi^6 * D) * s;
end;

%% Deflexión máxima en milímetros
w_max_mm = abs(min(w)) * 1000

%% Visualización 2D — mesh wireframe
triplot(tri, x, y);

%% Visualización 3D — superficie deformada con colormap JET (estilo SAP2000)
trisurf(tri, x, y, w);
colormap('jet');            % azul → cian → verde → amarillo → rojo (SAP2000-style)
colorbar();                 % barra de color con valores Min/Max
shading('interp');          % suavizar gradiente entre triángulos
view(45, 30);               % vista oblicua orbital
axis('equal');              % escalas iguales en X, Y, Z
