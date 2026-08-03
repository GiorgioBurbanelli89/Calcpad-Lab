%% symunit en MATLAB R2017a (Symbolic Math Toolbox) — 100% valido en R2017a
% MEDIDO en R2017a real:
%   - symunit, separateUnits, newUnit  -> SI existen
%   - unitConvert, checkUnits          -> NO (llegaron en R2017b)
%   - MATLAB NO auto-simplifica: 3*u.m + 200*u.cm queda sin sumar (usar simplify)
%   - double() de un valor con unidad DA ERROR -> primero separateUnits.
% (Hekatan Lab: mismo codigo, pero auto-simplifica, convierte y nombra kPa/kN.)

u = symunit;

%% Escalares con unidades
L = 6*u.m
b = 300*u.mm
q = 10*u.kN/u.m^2

%% Aritmetica (MATLAB deja simbolico; simplify lo combina)
suma_raw = 3*u.m + 200*u.cm         % 200*[cm] + 3*[m]  (sin sumar)
suma     = simplify(3*u.m + 200*u.cm)

%% Numero puro (forma correcta MATLAB): separateUnits + double
[Lval, Lunit] = separateUnits(L);
L_num = double(Lval)                 % 6   (Lunit = [m])

%% VECTORES y MATRICES con unidades (MATLAB los soporta: son objetos sym)
v = [10; 20; 30]*u.kN               % misma unidad por elemento
K = [2 1; 1 2]*u.kN/u.m             % matriz con unidad
sv = v + 5*u.kN                     % opera elemento a elemento

%% DISTINTA unidad por elemento (caso rigidez FEM mixta)
mix  = [5*u.m; 3*u.kN; 2*u.s]
Kmix = [3*u.kN/u.m, 2*u.kN; 2*u.kN, 5*u.kN*u.m]
[valv, untv] = separateUnits(mix);  % valv=[5;3;2], untv=[m;kN;s]
valv
