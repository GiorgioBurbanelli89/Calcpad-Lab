%% Inline texto + simbolico en Hekatan Lab
% Mismo ejemplo que el test de MATLAB R2017a, para comparar la renderizacion.

syms x q L E I

%% Forma 1: fprintf con char(expr) — texto y formula en una linea
fprintf('Forma 1: fprintf con char(expr)\n');
V = q*L/2 - q*x;
fprintf('  Cortante: V(x) = %s\n', char(V));

M = int(V, x);
fprintf('  Momento:  M(x) = %s\n', char(M));

M_max = subs(M, x, L/2);
fprintf('  Momento maximo en x=L/2: M_max = %s\n', char(M_max));

M_simp = simplify(M_max);
fprintf('  Simplificado: M_max = %s\n', char(M_simp));

%% Forma 2: disp puro (cada simbolico en su linea)
fprintf('\nForma 2: disp puro\n');
disp('  La formula clasica del momento maximo es:');
disp(q*L^2/8);

disp('  Y la pendiente theta(x) se obtiene de int(-M)/(E*I):');
theta = int(-M, x) / (E*I);
disp(theta);

%% Forma 3: latex() para reportes externos
fprintf('\nForma 3: latex() para reportes externos\n');
fprintf('  V en LaTeX: %s\n', latex(V));
fprintf('  M en LaTeX: %s\n', latex(M));
fprintf('  theta en LaTeX: %s\n', latex(theta));

%% Aplicacion numerica
fprintf('\n--- Aplicacion numerica ---\n');
q_n = 10000;  L_n = 6;  E_n = 210e9;  I_n = 869e-8;
M_max_num = subs(q*L^2/8, {q, L}, {q_n, L_n});
fprintf('  Para q=%g N/m, L=%g m:  M_max = %s = %g N*m\n', ...
        q_n, L_n, char(q*L^2/8), double(M_max_num));

y_max = 5*q*L^4/(384*E*I);
y_max_num = subs(y_max, {q, L, E, I}, {q_n, L_n, E_n, I_n});
fprintf('  Deflexion teorica: y_max = %s\n', char(y_max));
fprintf('    Sustituyendo:    y_max = %g m = %g mm\n', ...
        double(y_max_num), double(y_max_num)*1000);
