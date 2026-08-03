%% Viga cantilever 2D — matriz de rigidez con UNIDADES MIXTAS
%-- Un solo elemento viga (flexion), empotrado en el nodo 1 y con carga en la punta.
%-- La matriz de rigidez K tiene unidades DISTINTAS por entrada:
%--   kN/m (traslacion-traslacion), kN (traslacion-giro), kN*m (giro-giro).
u = symunit;

%% Propiedades de la seccion y el material (unidades reales)
E = 2e8*u.kN/u.m^2;    % modulo de elasticidad (200 GPa)
I = 8.33e-6*u.m^4;     % inercia de la seccion
L = 3*u.m;             % longitud del elemento
P = 10*u.kN;           % carga vertical en la punta
EI = E*I;              % rigidez a flexion (kN*m^2)

%% Rigidez del elemento viga 2D — 4 GDL: [v1, th1, v2, th2]
K = [ 12*EI/L^3,  6*EI/L^2, -12*EI/L^3,  6*EI/L^2;
       6*EI/L^2,  4*EI/L,    -6*EI/L^2,  2*EI/L;
     -12*EI/L^3, -6*EI/L^2,  12*EI/L^3, -6*EI/L^2;
       6*EI/L^2,  2*EI/L,    -6*EI/L^2,  4*EI/L ]

%% Empotramiento en el nodo 1 (v1 = th1 = 0) -> GDL libres del nodo 2: [v2, th2]
Kff = K([3 4],[3 4]);
f = [P; 0*u.kN*u.m];      % vector de cargas en la punta: [fuerza; momento]

%% Solve: desplazamiento y giro en la punta (unidades derivadas solas)
d = Kff\f                 % [v2 (m); th2 (rad)]

%% Reacciones en el empotramiento: R = K(fijos, libres) * d
Krf = K([1 2],[3 4]);
R = Krf*d                 % [cortante (kN); momento (kN*m)]

%% Verificacion analitica: v2 = P*L^3/(3*EI),  th2 = P*L^2/(2*EI)
v2_teo = P*L^3/(3*EI)
th2_teo = P*L^2/(2*EI)
