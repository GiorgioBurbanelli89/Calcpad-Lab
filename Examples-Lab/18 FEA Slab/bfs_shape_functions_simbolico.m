%% Funciones de forma del elemento BFS — DEDUCIDAS simbolicamente en Hekatan Lab
%' # Deduccion simbolica de las funciones de forma BFS (16 GDL)
%'
%' En Calcpad, Nedelcho **escribe a mano** las funciones base Phi_1a..Phi_4a, sus
%' derivadas, y las 16 funciones de forma. **No las calcula.** Hekatan Lab las **DEDUCE**
%' con `solve`/`diff`, y coinciden exacto. (Estilo Hekatan: una expresion sin `;` se
%' muestra sola — sin `disp` ni `fprintf`.)
tic;   % cronometro: lo SIMBOLICO es lento por naturaleza (MATLAB Symbolic Toolbox tambien)
syms xi eta a1 b1 real

%% 1) Las 4 funciones de Hermite — DEDUCIDAS (no escritas)
%' Cada base es la UNICA cubica que vale 1 en un grado de libertad de extremo (w o giro)
%' y 0 en los otros tres. Se parte de la cubica generica y se imponen las 4 condiciones.
s = sym('s');
syms A B C D real
w  = A + B*s + C*s^2 + D*s^3;      % cubica generica en s = coord local 0..1
dw = diff(w, s);
BC = [1 0 0 0; 0 1 0 0; 0 0 1 0; 0 0 0 1];   % [w(0) w'(0) w(1) w'(1)] por columna
H = sym(zeros(1, 4));
for k = 1 : 4
    sol = solve([subs(w, s, 0) == BC(k,1), subs(dw, s, 0) == BC(k,2), ...
                 subs(w, s, 1) == BC(k,3), subs(dw, s, 1) == BC(k,4)], [A B C D]);
    H(k) = simplify(subs(w, {A, B, C, D}, {sol.A, sol.B, sol.C, sol.D}));
end

%' Funciones base a lo largo de la dimension a (los giros van escalados por la longitud a1,
%' porque el giro fisico theta = dw/dx y xi = x/a1):
Phi_a = [subs(H(1), s, xi), a1*subs(H(2), s, xi), subs(H(3), s, xi), a1*subs(H(4), s, xi)]

%' Las que Calcpad (Ned) escribe a MANO, para comparar:
Ned_a = [1 - xi^2*(3 - 2*xi), xi*a1*(1 - xi*(2 - xi)), xi^2*(3 - 2*xi), xi^2*a1*(-1 + xi)];

%' Diferencia deducida (Hekatan) menos manual (Calcpad) — debe ser 0 0 0 0:
dif_base = simplify(Phi_a - Ned_a)

%% 2) Primeras y segundas derivadas — CALCULADAS con diff (estilo MATLAB), y simplificadas
dPhi_a  = simplify(diff(Phi_a, xi))
ddPhi_a = simplify(diff(Phi_a, xi, 2))

%' Las mismas a lo largo de b (por simetria: xi->eta, a1->b1):
Phi_b = subs(Phi_a, {xi, a1}, {eta, b1});

%% 3) Las 16 funciones de forma BFS = PRODUCTO TENSORIAL
%' N_i(xi,eta) = Phi_?a(xi) * Phi_?b(eta). Ned escribe las 16 lineas; aqui salen del producto.
%' Nodos 1=(0,0) 2=(1,0) 3=(1,1) 4=(0,1); por nodo: w, theta_x, theta_y, psi.
map = [1 1; 3 1; 3 3; 1 3];   % (indice a, indice b) del GDL w de cada nodo
N = sym(zeros(4, 4));
for node = 1 : 4
    pa = map(node,1); pb = map(node,2);
    N(node,1) = Phi_a(pa)   * Phi_b(pb);     % w
    N(node,2) = Phi_a(pa+1) * Phi_b(pb);     % theta_x  (giro en a)
    N(node,3) = Phi_a(pa)   * Phi_b(pb+1);   % theta_y  (giro en b)
    N(node,4) = Phi_a(pa+1) * Phi_b(pb+1);   % psi      (torsion)
end

%' Las 16 funciones de forma (fila = nodo; columna = w, theta_x, theta_y, psi):
N

%' Verificacion de una: N_1,w menos Phi_1a*Phi_1b (lo que Ned escribe) — debe ser 0:
chk = simplify(N(1,1) - Phi_a(1)*Phi_b(1))

%' Tiempo total de TODO el simbolico (solve + diff + simplify + producto tensorial):
t_simbolico_s = toc

%%
%' **Conclusion:** las funciones de forma, sus derivadas y el producto tensorial de 16 GDL
%' que en Calcpad se escriben a mano, en Hekatan Lab se **deducen** con `solve`/`diff`/`subs`
%' — y coinciden exacto. El motor simbolico hace el algebra, no el usuario.
