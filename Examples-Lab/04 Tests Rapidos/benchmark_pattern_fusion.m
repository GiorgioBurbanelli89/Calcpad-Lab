%% Benchmark Pattern Fusion — btdb(B,D) vs B'*D*B
clear; clc;

%-- Setup: misma forma que assembly de FEM (B 3×16, D 3×3)
B = zeros(3, 16);
for r = 1:3
    for c = 1:16
        B(r, c) = (r + c) * 0.01;
    end
end
D = [1, 0.15, 0; 0.15, 1, 0; 0, 0, 0.425];

N_ITER = 5000
fprintf('Iteraciones: %d\n\n', N_ITER);

%-- A) NAIVE: B' * D * B (3 matmuls + 1 transpose + allocs)
tic;
for i = 1:N_ITER
    K1 = B' * D * B;
end
t_naive = toc;
fprintf('A) Naive  B'' * D * B : %.4f s (%.2f us/iter)\n', t_naive, t_naive/N_ITER*1e6);

%-- B) FUSION: btdb(B, D)
tic;
for i = 1:N_ITER
    K2 = btdb(B, D);
end
t_fusion = toc;
fprintf('B) Fusion btdb(B, D)  : %.4f s (%.2f us/iter)\n', t_fusion, t_fusion/N_ITER*1e6);

fprintf('\nSpeedup fusion vs naive: %.2f×\n', t_naive / t_fusion);

%-- Sanity: K1 y K2 deben ser iguales
err = 0;
for i = 1:16
    for j = 1:16
        diff = K1(i, j) - K2(i, j);
        err = err + diff*diff;
    end
end
err = sqrt(err);
fprintf('||K_naive - K_fusion|| = %.3e   (debe ser < 1e-10)\n', err);

%-- Test dbz también
z = zeros(16, 1);
for k = 1:16
    z(k) = k*0.5;
end

tic;
for i = 1:N_ITER
    m1 = -D * B * z;
end
t_naive2 = toc;
fprintf('\nC) Naive  -D * B * z  : %.4f s (%.2f us/iter)\n', t_naive2, t_naive2/N_ITER*1e6);

tic;
for i = 1:N_ITER
    m2 = -dbz(D, B, z);
end
t_fusion2 = toc;
fprintf('D) Fusion -dbz(D,B,z) : %.4f s (%.2f us/iter)\n', t_fusion2, t_fusion2/N_ITER*1e6);

fprintf('\nSpeedup dbz vs naive: %.2f×\n', t_naive2 / t_fusion2);
