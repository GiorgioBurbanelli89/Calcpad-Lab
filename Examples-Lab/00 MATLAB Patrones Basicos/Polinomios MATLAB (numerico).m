% TEST EXTENSO — Polinomios de MATLAB 2017a en Hekatan Lab
% Cada bloque compara el resultado contra el valor de MATLAB 2017a.
% Al final: cuenta de PASS. err<1e-9 = OK.

tol = 1e-9;

% 1) conv  — multiplicacion de polinomios
c1 = conv([1 2], [1 3]);                 % ref [1 5 6]
e01 = max(abs(c1 - [1 5 6]));

% 2) deconv (1 salida) — division, cociente
q1 = deconv([1 5 6], [1 3]);             % ref [1 2]
e02 = max(abs(q1 - [1 2]));

% 3) deconv (2 salidas) — cociente y resto
[q2, r2] = deconv([1 5 7], [1 3]);       % ref q=[1 2], r=[0 0 1]
e03 = max(abs(q2 - [1 2])) + max(abs(r2 - [0 0 1]));

% 4) poly desde raices reales
p1 = poly([2 3]);                        % ref [1 -5 6]
e04 = max(abs(p1 - [1 -5 6]));

% 5) poly desde raices (3)
p2 = poly([1 2 3]);                      % ref [1 -6 11 -6]
e05 = max(abs(p2 - [1 -6 11 -6]));

% 6) poly con raices complejas conjugadas -> coefs reales
p3 = poly([1+2i, 1-2i]);                 % ref [1 -2 5]
e06 = max(abs(p3 - [1 -2 5]));

% 7) poly de una matriz cuadrada -> polinomio caracteristico
A  = [1 2; 3 4];
p4 = poly(A);                            % ref [1 -5 -2]
e07 = max(abs(p4 - [1 -5 -2]));

% 8) polyval escalar
v1 = polyval([1 -5 6], 2);               % ref 0
e08 = abs(v1 - 0);

% 9) polyval vectorizado
v2 = polyval([1 0 -1], [1 2 3]);         % ref [0 3 8]
e09 = max(abs(v2 - [0 3 8]));

% 10) polyvalm — evaluacion matricial
X   = [1 2; 3 4];
Vm  = polyvalm([1 -5 6], X);             % ref [8 0; 0 8]
e10 = max(max(abs(Vm - [8 0; 0 8])));

% 11) polyder — derivada
d1 = polyder([1 -5 6]);                  % ref [2 -5]
e11 = max(abs(d1 - [2 -5]));

% 12) polyder — con ceros
d2 = polyder([3 0 2 0]);                 % ref [9 0 2]
e12 = max(abs(d2 - [9 0 2]));

% 13) polyint — integral (k=0)
i1 = polyint([2 -5]);                    % ref [1 -5 0]
e13 = max(abs(i1 - [1 -5 0]));

% 14) polyint — con constante k=3
i2 = polyint([3 -5], 3);                 % ref [1.5 -5 3]
e14 = max(abs(i2 - [1.5 -5 3]));

% 15) roots — reales
rr = sort(roots([1 -5 6]));              % ref [2; 3]
e15 = max(abs(rr - [2; 3]));

% 16) roots — complejos
rc = roots([1 0 1]);                     % ref +-i
e16 = abs(max(abs(rc)) - 1) + abs(min(abs(rc)) - 1);

% 17) residue — polos distintos
[R1, P1, K1] = residue([-4 8], [1 6 8]); % ref r=[-12;8], p=[-4;-2], k=[]
% comparo por conjuntos (el orden de polos puede variar)
sP1 = sort(P1);   sR1 = sort(R1);
e17 = max(abs(sP1 - [-4; -2])) + max(abs(sR1 - [-12; 8])) + numel(K1);

% 18) residue — polo repetido
[R2, P2, K2] = residue(1, [1 2 1]);      % ref r=[0;1], p=[-1;-1], k=[]
e18 = max(abs(sort(R2) - [0; 1])) + max(abs(P2 - [-1; -1])) + numel(K2);

% 19) residue — con termino directo k
[R3, P3, K3] = residue([1 1], [1 -1]);   % ref r=2, p=1, k=1
e19 = abs(R3 - 2) + abs(P3 - 1) + abs(K3 - 1);

% 20) poly2sym / sym2poly (ida y vuelta)
syms s
ps = poly2sym([1 -5 6], s);              % s^2 - 5 s + 6
back = sym2poly(ps);                     % ref [1 -5 6]
e20 = max(abs(back - [1 -5 6]));

% ---- Verdicto ----
errs = [e01 e02 e03 e04 e05 e06 e07 e08 e09 e10 e11 e12 e13 e14 e15 e16 e17 e18 e19 e20];
npass = sum(errs < tol);
ntot  = numel(errs);
maxerr = max(errs);

% #md
% ## Test polinomios MATLAB 2017a en Hekatan Lab
% Cada fila compara Hekatan contra el valor exacto de MATLAB 2017a. error < 1e-9 = PASS.
%
% | # | Funcion | Caso | error vs MATLAB |
% |---|---------|------|----------------:|
% | 1 | conv | (s+2)(s+3) | @{e01} |
% | 2 | deconv 1 salida | cociente | @{e02} |
% | 3 | deconv 2 salidas | cociente + resto | @{e03} |
% | 4 | poly | raices reales | @{e04} |
% | 5 | poly | 3 raices | @{e05} |
% | 6 | poly | conjugadas complejas | @{e06} |
% | 7 | poly | matriz (car.) | @{e07} |
% | 8 | polyval | escalar | @{e08} |
% | 9 | polyval | vector | @{e09} |
% | 10 | polyvalm | matricial | @{e10} |
% | 11 | polyder | derivada | @{e11} |
% | 12 | polyder | con ceros | @{e12} |
% | 13 | polyint | k=0 | @{e13} |
% | 14 | polyint | k=3 | @{e14} |
% | 15 | roots | reales | @{e15} |
% | 16 | roots | complejos | @{e16} |
% | 17 | residue | polos distintos | @{e17} |
% | 18 | residue | polo repetido | @{e18} |
% | 19 | residue | termino directo | @{e19} |
% | 20 | poly2sym / sym2poly | ida y vuelta | @{e20} |
%
% **RESULTADO: @{npass} / @{ntot} PASS**  ·  error maximo = @{maxerr}
% #endmd
