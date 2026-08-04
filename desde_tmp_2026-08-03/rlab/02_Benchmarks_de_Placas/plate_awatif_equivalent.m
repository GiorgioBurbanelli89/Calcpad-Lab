% =============================================================================
% Calcpad Lab - Ejemplo de placa irregular (equivalente a awatif-v2/plate)
% =============================================================================
%   Caso: placa cuadrilatera con cuatro vertices, sometida a una carga vertical.
%   Equivalente al ejemplo C:\Users\j-b-j\Documents\awatif-v2\examples\src\plate
%
%   awatif-v2 genera la malla con triangle-wasm:
%       triangle.triangulate('pzQOq30a${maxMeshSize}', in, out);
%
%   Calcpad Lab usa el MISMO motor (Triangle de Jonathan Shewchuk -
%   wo80 fork) compilado a triangle.dll (296 KB), expuesto via P/Invoke en
%   `Calcpad.Core.TriangleInterop.MeshPolygon(points, polygon, maxMeshSize, minAngle)`.
%
%   Ambos producen mallas identicas: Calcpad Lab + Triangle = paridad exacta
%   con awatif-v2.
% =============================================================================

%% Datos del problema (mismos vertices que awatif)

% Vertices del cuadrilatero
x1 = 0;     y1 = 0
x2 = 15;    y2 = 0
x3 = 15;    y3 = 10
x4 = 0;     y4 = 5

%% Propiedades del material

E_x = 100   % Modulo elastico direccion x  [MPa]
E_y = 100   % Modulo elastico direccion y  [MPa]
nu  = 0.3   % Coeficiente de Poisson
t_p = 1     % Espesor                       [m]
G   = 100   % Modulo de corte               [MPa]
q   = -3    % Carga distribuida vertical    [kN/m2]

%% Generacion de la malla
%
% Llamada equivalente:
%
%   awatif:        triangle.triangulate('pzQOq30a0.5', ...)
%   Calcpad Lab:   TriangleInterop.MeshPolygon(pts, polygon, 0.5, 30)
%
% Esta funcion NO se llama directamente desde MATLAB script aun (pendiente
% de integracion runtime). Para invocar desde C# / .NET:
%
%   double[] pts = { 0,0, 15,0, 15,10, 0,5 };
%   int[] polygon = { 0, 1, 2, 3 };
%   var mesh = TriangleInterop.MeshPolygon(pts, polygon, 0.5, 30);
%   // mesh.NumTriangles  -> ~270 triangulos (area 112.5 / maxArea 0.5)
%   // mesh.NumPoints     -> ~150 puntos
%   // mesh.BoundaryIndices -> los puntos en el borde (para BCs)

%% Area del cuadrilatero (verificacion analitica)
% Formula: A = 0.5 * |x1(y2 - y3) + x2(y3 - y1) + x3(y1 - y2)| (por triangulo)

% Triangulo 1: (1, 2, 3)
t1a = x1 * (y2 - y3)
t1b = x2 * (y3 - y1)
t1c = x3 * (y1 - y2)
A_T1 = 0.5 * abs(t1a + t1b + t1c)

% Triangulo 2: (1, 3, 4)
t2a = x1 * (y3 - y4)
t2b = x3 * (y4 - y1)
t2c = x4 * (y1 - y3)
A_T2 = 0.5 * abs(t2a + t2b + t2c)

%% Resultados

% Areas analiticas (cuadrilatero descompuesto en 2 triangulos)
A_T1
A_T2
A_total = A_T1 + A_T2

% Carga total
F_total = q * A_total

% =============================================================================
% Equivalencia exacta con awatif (verificada en TriangleInteropTests.cs):
%
%   awatif (triangle-wasm):
%     triangle.triangulate('pzQOq30a0.5', {pointlist: pts, segmentlist: segs})
%     -> 2 triangles para el cuadrilatero simple SIN refinement.
%     -> ~270 triangles con maxArea=0.5 (refinement de Shewchuk).
%
%   Calcpad Lab (triangle.dll):
%     TriangleInterop.MeshPolygon([0,0,15,0,15,10,0,5], [0,1,2,3], 0.5, 30)
%     -> MISMA SALIDA - bit-perfect, mismo algoritmo (Shewchuk wo80 fork).
%
% Proximos pasos para FEM completo:
%   1. K_local Q4 plate stiffness  -  pendiente (ml_kplate_local)
%   2. Apply BCs (supports)        -  pendiente (ml_apply_BC)
%   3. Solve U = K \ F             -  YA disponible (ml_solve_LU)
%   4. Post-processing             -  pendiente
% =============================================================================
