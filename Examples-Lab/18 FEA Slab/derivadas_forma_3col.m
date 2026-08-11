%% Funciones de forma BFS: Base | Φ' | Φ''  (DEDUCIDAS por Hekatan)
%' # Funciones de forma BFS — 3 columnas
%' Igual que en Calcpad (Base | 1ª derivada | 2ª derivada), pero aqui las derivadas NO se
%' escriben: Hekatan Lab las **deduce** con `diff`. La prima se escribe con letras
%' (`Phiprime`→Φ′, `Phipprime`→Φ″) y el motor la renderiza — todo MATLAB-valido.
syms xi eta a_1 b_1 real

%%
%' ## A lo largo de la dimension a
Phi_1a = 1 - xi^2*(3 - 2*xi);
Phi_2a = xi*a_1*(1 - xi*(2 - xi));
Phi_3a = xi^2*(3 - 2*xi);
Phi_4a = xi^2*a_1*(-1 + xi);
% Derivadas FISICAS d/dx = (1/a_1)·dΦ/dξ  y  d²/dx² = (1/a_1²)·d²Φ/dξ²  (= Calcpad).
% a_1 es un token simbolico (syms) que se renderiza como a₁; en las funciones de giro se cancela.
Phiprime_1a = simplify(diff(Phi_1a, xi)/a_1);   Phipprime_1a = simplify(diff(Phi_1a, xi, 2)/a_1^2);
Phiprime_2a = simplify(diff(Phi_2a, xi)/a_1);   Phipprime_2a = simplify(diff(Phi_2a, xi, 2)/a_1^2);
Phiprime_3a = simplify(diff(Phi_3a, xi)/a_1);   Phipprime_3a = simplify(diff(Phi_3a, xi, 2)/a_1^2);
Phiprime_4a = simplify(diff(Phi_4a, xi)/a_1);   Phipprime_4a = simplify(diff(Phi_4a, xi, 2)/a_1^2);
% #cols Funciones base | Primera derivada | Segunda derivada
Phi_1a ; Phiprime_1a ; Phipprime_1a
Phi_2a ; Phiprime_2a ; Phipprime_2a
Phi_3a ; Phiprime_3a ; Phipprime_3a
Phi_4a ; Phiprime_4a ; Phipprime_4a
% #endcols

%%
%' ## A lo largo de la dimension b
Phi_1b = 1 - eta^2*(3 - 2*eta);
Phi_2b = eta*b_1*(1 - eta*(2 - eta));
Phi_3b = eta^2*(3 - 2*eta);
Phi_4b = eta^2*b_1*(-1 + eta);
Phiprime_1b = simplify(diff(Phi_1b, eta)/b_1);   Phipprime_1b = simplify(diff(Phi_1b, eta, 2)/b_1^2);
Phiprime_2b = simplify(diff(Phi_2b, eta)/b_1);   Phipprime_2b = simplify(diff(Phi_2b, eta, 2)/b_1^2);
Phiprime_3b = simplify(diff(Phi_3b, eta)/b_1);   Phipprime_3b = simplify(diff(Phi_3b, eta, 2)/b_1^2);
Phiprime_4b = simplify(diff(Phi_4b, eta)/b_1);   Phipprime_4b = simplify(diff(Phi_4b, eta, 2)/b_1^2);
% #cols Funciones base | Primera derivada | Segunda derivada
Phi_1b ; Phiprime_1b ; Phipprime_1b
Phi_2b ; Phiprime_2b ; Phipprime_2b
Phi_3b ; Phiprime_3b ; Phipprime_3b
Phi_4b ; Phiprime_4b ; Phipprime_4b
% #endcols
