%% Benchmark naive C# matmul (forzando bypass de BLAS via N < threshold por chunks)
clear; clc;

%-- Matrix 200x200 (sub-threshold no funciona, hacer naive simulado en loops)
N = 200
A = zeros(N, N);
B = zeros(N, N);
for i = 1:N
    for j = 1:N
        A(i, j) = (i + j) * 0.01;
        B(i, j) = (i * j) * 0.001;
    end
end

%-- Single matmul + 10 matmuls usando A*B (que ahora dispatchea a BLAS)
tic;
C = A * B;
t1 = toc;
fprintf('1 matmul %dx%d (con BLAS): %.4f s\n', N, N, t1);

tic;
for i = 1:10
    C = A * B;
end
t10 = toc;
fprintf('10 matmuls: %.4f s  (avg %.4f s/op)\n', t10, t10/10);

%-- Para comparar contra naive, hacemos matmul "manual" en MATLAB loop
%-- (la unica forma de evitar el dispatch BLAS)
fprintf('\nNaive matmul (loops manuales)...\n');
tic;
Cn = zeros(N, N);
for i = 1:N
    for k = 1:N
        a_ik = A(i, k);
        for j = 1:N
            Cn(i, j) = Cn(i, j) + a_ik * B(k, j);
        end
    end
end
t_naive = toc;
fprintf('Naive matmul: %.4f s\n', t_naive);

fprintf('\nSpeedup BLAS vs naive: %.1f×\n', t_naive / (t10/10));
