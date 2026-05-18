%% Benchmark JIT — loop escalar de millón de iteraciones
clear; clc;

%-- Caso clásico de tight loop: suma de cuadrados.
%-- Sin JIT (intérprete C# puro): se espera varios segundos.
%-- Con JIT (Expression Trees → IL nativo): se espera < 50 ms.

N = 1000000

s = 0
tic;
for i = 1:N
    s = s + i*i
end
t = toc;

fprintf('N = %d\n', N);
fprintf('s = %g  (esperado: N*(N+1)*(2N+1)/6 = %g)\n', s, N*(N+1)*(2*N+1)/6);
fprintf('tiempo = %.3f s\n', t);
fprintf('iter/s = %.2e\n', N/t);
