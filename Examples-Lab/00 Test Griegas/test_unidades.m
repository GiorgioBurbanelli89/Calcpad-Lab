%% Test de UNIDADES: dentro de % (Calcpad) vs print/display
L = 6;
q = 10;
M = 62.75;

%% 1) Dentro de % con Calcpad (#equ / #val / #noc) — unidades nativas Calcpad
% #equ L = 6*m
% #equ q = 10*kN/m^2
% #noc M_x = q*L^2/8

%% 2) fprintf por defecto (PLANO, paridad MATLAB Command Window)
fprintf('L = %.1f m\n', L);
fprintf('q = %.1f kN/m^2\n', q);
fprintf('M_x = %.2f kNm/m\n', M);

%% 3) fprintf con #render
% #render
fprintf('L = %.1f m\n', L);
fprintf('q = %.1f kN/m^2\n', q);
fprintf('E = 35000 MPa, I = 1.2 m^4\n');
fprintf('M_x = %.2f kN*m/m\n', M);
% #plain
