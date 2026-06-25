%% Medir tiempo de ejecucion con tic / toc
% tic arranca el cronometro, toc lo lee (en segundos).

%-- 1) Resolver un sistema lineal grande  A x = b   (usa Intel MKL)
n = 800;
A = rand(n, n) + n*eye(n);   % bien condicionada
b = rand(n, 1);

tic
x = A \ b;
t_solve = toc;
fprintf('Solve %dx%d:  %.4f s\n', n, n, t_solve)

%-- 2) Multiplicacion de matrices  C = A*A   (BLAS dgemm)
tic
C = A * A;
t_mul = toc;
fprintf('Mult  %dx%d:  %.4f s\n', n, n, t_mul)

%-- 3) Un bucle escalar (motor managed, sin MKL)
tic
s = 0;
for k = 1:1e6
    s = s + sqrt(k);
end
t_loop = toc;
fprintf('Loop  1e6 iter: %.4f s\n', t_loop)

%-- 4) Medir un tramo concreto con handle:  id = tic; ... ; toc(id)
id = tic;
r = sort(rand(1, 200000));
t_sort = toc(id);
fprintf('Sort  2e5 elem: %.4f s\n', t_sort)
