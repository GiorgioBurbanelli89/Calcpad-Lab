% Test simple de loop: suma de cuadrados 1^2 + 2^2 + ... + N^2
N = 100000;
tic
s = 0;
for i = 1:N
  s = s + i^2;
end
toc

% Comparacion contra formula cerrada N(N+1)(2N+1)/6
esperado = N * (N + 1) * (2*N + 1) / 6;
fprintf('Suma loop  = %.4e\n', s);
fprintf('Esperado   = %.4e  (formula N(N+1)(2N+1)/6)\n', esperado);
fprintf('Error      = %.2e  (debe ser cero)\n', s - esperado);

% Echo matematico de los valores principales
N
s
esperado
