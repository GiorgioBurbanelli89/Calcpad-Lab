% =============================================================================
%  TIMOSHENKO §29 - PLACA RECTANGULAR SS 4 LADOS, CARGA UNIFORME
%  Validacion analitica Navier para hekatan-fem-py (safe_ex01_plate)
% =============================================================================
%
%  Este script es MATLAB 100% portable:
%     - Corre en MATLAB nativo  -> muestra figuras con trisurf colormap
%     - Corre en Calcpad Lab    -> HTML con Three.js rainbow (mismo look)
%     - Corre en Octave         -> figuras con colormap jet
%
%  Geometria:    placa rectangular 6 x 4 m, espesor 0.10 m
%  Material:     E=24.85 GPa (concreto fc'=210 kgf/cm2), nu=0.20
%  Carga:        q = 10 kN/m2 (uniforme, hacia -Z)
%  Apoyos:       Simply Supported en los 4 bordes (UZ=0 en perimetro)
%
%  Solucion analitica (Navier doble serie de Fourier):
%     w(x,y) = SUM_{m,n impares} (16*q / (pi^6 * D * m * n * (m^2/a^2 + n^2/b^2)^2))
%                                * sin(m*pi*x/a) * sin(n*pi*y/b)
%
%  Resultado esperado al centro (x=a/2, y=b/2):
%     w_max ~ 9.1666 mm  (Navier, 41 terminos)
%
%  hekatan-fem-py FEM Kirchhoff (mesh 12x8):  w_max = 9.0943 mm
%  Error FEM vs Navier: -0.79 % (excelente convergencia)
% =============================================================================

%% Backend de plot - gnuplot si Octave (para headless), default si MATLAB
if exist('OCTAVE_VERSION', 'builtin')
    graphics_toolkit('gnuplot');
end

%% Parametros del problema
a  = 6.0          % longitud en x [m]
b  = 4.0          % longitud en y [m]
t  = 0.10         % espesor [m]
q  = -10000       % carga distribuida [N/m^2] (hacia -Z)
E  = 24.85e9      % modulo elastico [Pa]
nu = 0.20         % coeficiente de Poisson

%% Rigidez flexional de la placa
D = E * t^3 / (12 * (1 - nu^2))

%% Mesh regular Nx x Ny para muestreo de la solucion
Nx = 30
Ny = 20
nPts = (Nx+1) * (Ny+1);
x = zeros(1, nPts);
y = zeros(1, nPts);
k = 0;
for j = 0:Ny
    for i = 0:Nx
        k = k + 1;
        x(k) = i * a / Nx;
        y(k) = j * b / Ny;
    end
end

%% Triangulacion para visualizacion
tri = delaunay(x, y);

%% Solucion analitica de Navier - 41 terminos (m, n impares)
%  Suma converge rapido para placas isotropas SS con carga uniforme.
nTerms = 41;
w = zeros(1, nPts);

for k = 1:nPts
    s = 0;
    for m = 1:2:nTerms
        for n = 1:2:nTerms
            num = sin(m*pi*x(k)/a) * sin(n*pi*y(k)/b);
            den = m * n * (m^2/a^2 + n^2/b^2)^2;
            s = s + num / den;
        end
    end
    w(k) = 16 * q / (pi^6 * D) * s;
end

%% Deflexion maxima en milimetros (centro de la placa)
w_max_mm = abs(min(w)) * 1000

%% Momentos al centro (formulas de la doble serie Navier)
%  M_xx = -D * (d2w/dx2 + nu*d2w/dy2)  evaluado en (a/2, b/2)
xc = a/2; yc = b/2;
Mxx_c = 0;
Myy_c = 0;
for m = 1:2:nTerms
    for n = 1:2:nTerms
        base = 16 * q / (pi^6 * m * n * (m^2/a^2 + n^2/b^2)^2);
        sm = sin(m*pi*xc/a);
        sn = sin(n*pi*yc/b);
        d2x = -(m*pi/a)^2;
        d2y = -(n*pi/b)^2;
        Mxx_c = Mxx_c - D * (d2x + nu*d2y) * (base/D) * sm * sn;
        Myy_c = Myy_c - D * (nu*d2x + d2y) * (base/D) * sm * sn;
    end
end

Mxx_max_kNm = Mxx_c / 1000
Myy_max_kNm = Myy_c / 1000

%% Visualizacion 3D - superficie deformada
%  En MATLAB / Calcpad-Lab: figura interactiva.
%  En Octave headless: intenta PNG, si falla por backend skippea sin abortar.
try
    fig = figure('visible', 'off');
    trisurf(tri, x, y, w);
    colorbar();
    view(45, 30);
    axis('equal');
    xlabel('x [m]'); ylabel('y [m]'); zlabel('w [m]');
    title('Timoshenko Navier - SS plate, q uniforme');
    try
        saveas(fig, 'timoshenko_ss_plate_navier.png');
    catch
        % Octave-CLI sin backend headless: skip
        fprintf('  [info] PNG save skipped (no headless backend)\n');
    end
catch
    fprintf('  [info] figure creation skipped\n');
end

%% Resumen comparativo vs hekatan-fem-py
fprintf('\n=== COMPARACION Navier vs hekatan-fem-py ===\n');
fprintf('  Geometria: %.1f x %.1f x %.3f m\n', a, b, t);
fprintf('  Material:  E=%.2f GPa, nu=%.2f\n', E/1e9, nu);
fprintf('  Carga:     q=%.1f kN/m2 uniforme\n', abs(q)/1000);
fprintf('  Mesh muestreo: %d x %d (visualizacion)\n', Nx, Ny);
fprintf('  Terminos Navier: %d (m, n impares)\n', nTerms);
fprintf('\n');
fprintf('  w_max al centro:\n');
fprintf('    Navier analitico:   %.4f mm\n', w_max_mm);
fprintf('    hekatan-fem-py FEM: 9.0943 mm  (mesh 12x8, Kirchhoff MZC)\n');
fprintf('    Error FEM vs Nav.:  -0.79 %%\n');
fprintf('\n');
fprintf('  Momentos al centro:\n');
fprintf('    Mxx_max = %.3f kN*m/m\n', Mxx_max_kNm);
fprintf('    Myy_max = %.3f kN*m/m\n', Myy_max_kNm);
