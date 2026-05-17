% Sistema_Lineal_3x3.m — Resolver sistema A*x = b
clear; clc;

A = [2 1 -1;
    -3 -1  2;
    -2  1  2];
b = [8; -11; -3];

fprintf('Sistema A*x = b\n');
fprintf('A =\n'); disp(A);
fprintf('b = '); disp(b');

% Backslash operator (Gauss eliminacion)
x = A \ b;
fprintf('Solucion x = A\\b:\n'); disp(x');

% Verificacion
fprintf('Verificacion A*x:\n'); disp((A*x)');
fprintf('Norma del residuo: ||A*x - b|| = %.2e\n', norm(A*x - b));

% Inversa explicita (mas costoso, menos numericamente estable)
fprintf('\nUsando inversa: x = inv(A)*b:\n');
disp((inv(A)*b)');
