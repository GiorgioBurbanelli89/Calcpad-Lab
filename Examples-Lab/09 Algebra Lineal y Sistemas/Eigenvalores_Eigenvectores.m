% Eigenvalores_Eigenvectores.m — Calculo de eigenvalores y vectores propios
clear; clc;

% Matriz simetrica (eigenvalores reales)
A = [4 1 0;
     1 3 1;
     0 1 2];

fprintf('Matriz A simetrica:\n'); disp(A);

% eig: solo eigenvalores
lambda = eig(A);
fprintf('Eigenvalores:\n'); disp(lambda');

% [V, D] = eig: vectores y matriz diagonal de eigenvalores
[V, D] = eig(A);
fprintf('Eigenvectores (columnas de V):\n'); disp(V);
fprintf('Eigenvalores (diagonal de D):\n'); disp(diag(D)');

% Verificacion: A*v = lambda*v
fprintf('Verificacion A*v_1 = lambda_1 * v_1:\n');
v1 = V(:,1); l1 = D(1,1);
fprintf('  ||A*v_1 - l_1*v_1|| = %.2e\n', norm(A*v1 - l1*v1));
