%% Elemento finito T6 para talud — procedimiento académico (simbólico → numérico)
%' <h3>Elemento triangular T6 (6 nodos) — deducción simbólica para el talud</h3>
%' <hr/>
%' Igual que en la losa (rectangular slab FEA) pero para el TALUD: elemento
%' triangular T6 en deformación plana. Deducimos las funciones de forma, sus
%' derivadas y la matriz constitutiva con álgebra simbólica, y luego evaluamos
%' la rigidez de un elemento con números. Sin disp/fprintf.

%' <h4>1. Coordenadas de área y funciones de forma</h4>
syms xi eta real
%' Coordenadas de área:  L_1 = 1 − ξ − η ,  L_2 = ξ ,  L_3 = η.
L1 = 1 - xi - eta;
L2 = xi;
L3 = eta;
%' Nodos: 3 esquinas  L_i·(2·L_i − 1)  y 3 puntos medios  4·L_i·L_j.
N1 = L1*(2*L1 - 1);   N4 = 4*L1*L2;
N2 = L2*(2*L2 - 1);   N5 = 4*L2*L3;
N3 = L3*(2*L3 - 1);   N6 = 4*L3*L1;
N = simplify([N1, N2, N3, N4, N5, N6]);
%' Funciones de forma T6  N = [N_1  N_2  N_3  N_4  N_5  N_6]:
N

%' <h4>2. Derivadas en las coordenadas naturales</h4>
%' Las derivadas se deducen con `diff` (Hekatan las calcula, no se escriben a mano):
N_xi  = simplify(diff(N, xi))
N_eta = simplify(diff(N, eta))

%' <h4>3. Matriz constitutiva D (deformación plana, elástica)</h4>
syms E nu real
%' Deformación plana:  D = E/((1+ν)(1−2ν)) · [ 1−ν  ν  0 ; ν  1−ν  0 ; 0  0  (1−2ν)/2 ].
D = E/((1+nu)*(1-2*nu)) * [1-nu, nu, 0; nu, 1-nu, 0; 0, 0, (1-2*nu)/2];
D

%' <h4>4. Ahora un elemento CON números (Jacobiano, B y rigidez)</h4>
% Elemento de muestra (nodos del talud, en m): esquinas + puntos medios.
xy = [0 0; 2 0; 1 2; 1 0; 1.5 1; 0.5 1];    % (oculto) coord de los 6 nodos
Emod = 21000;  nuv = 0.30;                    % E [kPa], ν  (suelo tipo talud)
%' Material del suelo:  E = @Emod kPa ,  ν = @nuv .  Puntos de Gauss T6: 3.
gp = [1/6 1/6; 2/3 1/6; 1/6 2/3];  gw = [1/6 1/6 1/6];
Dn = double(subs(D, {E, nu}, {Emod, nuv}));
Ke = zeros(12, 12);
for q = 1:3
    dNx_n = double(subs(N_xi,  {xi, eta}, {gp(q,1), gp(q,2)}));
    dNy_n = double(subs(N_eta, {xi, eta}, {gp(q,1), gp(q,2)}));
    J = [dNx_n*xy(:,1), dNx_n*xy(:,2); dNy_n*xy(:,1), dNy_n*xy(:,2)];
    dN = J \ [dNx_n; dNy_n];      % ∂N/∂x , ∂N/∂y (2×6)
    B = zeros(3, 12);
    for a = 1:6
        B(1, 2*a-1) = dN(1,a);
        B(2, 2*a)   = dN(2,a);
        B(3, 2*a-1) = dN(2,a);  B(3, 2*a) = dN(1,a);
    end
    Ke = Ke + B' * Dn * B * det(J) * gw(q);
end
%' Rigidez del elemento T6 (12×12), diagonal (rigideces de cada gdl):
diagKe = diag(Ke)
%" El T6 simbólico se vuelve una rigidez numérica lista para ensamblar el talud.
