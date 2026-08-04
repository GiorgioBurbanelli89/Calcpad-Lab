% =============================================================================
%  PLACA CUADRADA EMPOTRADA EN BORDES CON CARGA UNIFORME
% =============================================================================
%
%  Benchmark portado de awatif-v2:
%     C:\Users\j-b-j\Documents\awatif-v2\awatif-fem\src\deform.test.ts
%     test("Plate: Rectangular pin-supported plate with uniform load")
%
%  Este script es MATLAB 100% portable:
%     - Corre en MATLAB nativo  -> muestra figuras con colormap jet (SAP2000-style)
%     - Corre en Calcpad Lab    -> HTML con Three.js rainbow (mismo look)
%     - Corre en Octave         -> figuras con colormap jet
%
%  Geometria:    placa cuadrada 10x10 m, espesor 0.15 m
%  Material:     E=10 GPa, nu=0.25, isotropica
%  Carga:        p0 = -1000 N/m2 (uniforme transversal)
%  Apoyos:       fijos en los 4 bordes
%  Deflexion:    max ~= 12.69 mm (FEM awatif)
%                       13.541 mm (solucion analitica Timoshenko exacta)
% =============================================================================

%% Parametros del problema
a  = 10.0    % longitud en x [m]
b  = 10.0    % longitud en y [m]
h  = 0.15    % espesor [m]
p0 = -1000   % carga distribuida [N/m2]
E  = 1.0e10  % modulo elastico [Pa]
nu = 0.25    % coeficiente de Poisson

%% Generacion del mesh - grid regular 10x10 (100 nodos)
nPts = 100;
x = zeros(1, nPts);
y = zeros(1, nPts);
for k = 1:nPts
    i = mod(k - 1, 10);
    j = (k - i - 1) / 10;
    x(k) = i * a / 9;
    y(k) = j * b / 9;
end

%% Triangulacion Delaunay automatica (~162 triangulos)
tri = delaunay(x, y);

%% Solucion analitica de Timoshenko - serie de Fourier completa
% w(x, y) = (16*p0 / pi6*D) * Sum_{m,n impares} sin(mpix/a)*sin(npiy/b) / [m*n*(m2/a2 + n2/b2)2]
%
% Sumamos m, n in {1, 3, 5, 7} (4x4 = 16 terminos) - converge rapido a la
% solucion exacta Timoshenko 13.541 mm.
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

%% Deflexion maxima en milimetros
w_max_mm = abs(min(w)) * 1000

%% Visualizacion 2D - mesh wireframe
% triplot(tri, x, y);

%% Visualizacion 3D - superficie deformada
trisurf(tri, x, y, w);
colorbar();                 % barra de color con valores Min/Max
view(45, 30);               % vista oblicua orbital
axis('equal');              % escalas iguales en X, Y, Z
